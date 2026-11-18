using System;
using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using CarWash.Data;
using CarWash.Economy;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Day
{
    /// <summary>Client-side notification of one customer's result (toast + coin burst).</summary>
    public struct PayNotice
    {
        public Vector3 position;
        public string vehicleId;
        public int amount;
        /// <summary>M4.8 F3: part of the pay that was the towel-drying tip (0 = not dried).</summary>
        public int dryTip;
        public float clean;
        public bool refused, angry;
    }

    /// <summary>
    /// Server day loop (TECH S06): Lobby → Day (arrivals on EconomyCalc.ArrivalInterval, boss on day 5, auto-finish at
    /// 100% clean) → closing time (queue sent away, overtime while bays are busy, max 60 s, then remaining cars are
    /// auto-finished) → Summary (rent; bankrupt → GameOver → retry from Day 1) → Shop (pass-through until T5.1) → next day.
    /// Replicates state/week/day/clock/summary; money + rep live in EconomyState on the same GameDirector object.
    /// </summary>
    [RequireComponent(typeof(EconomyState))]
    public class DayManager : NetworkBehaviour
    {
        public static DayManager Instance { get; private set; }

        // dev/test overrides (0 / negative = off)
        public static float DayLengthOverride, MaxOvertimeOverride;
        public static float DevCleanOverride = -1f;

        [SerializeField] private DataRegistry _data;
        [SerializeField] private Material _coinFx;

        public readonly NetworkVariable<GameState> State = new NetworkVariable<GameState>(GameState.Lobby);
        public readonly NetworkVariable<int> Week = new NetworkVariable<int>(1);
        public readonly NetworkVariable<int> DayNum = new NetworkVariable<int>(1);
        public readonly NetworkVariable<float> DayStart = new NetworkVariable<float>();
        public readonly NetworkVariable<DayPhase> Phase = new NetworkVariable<DayPhase>(DayPhase.Closed);
        public readonly NetworkVariable<float> DayLengthNet = new NetworkVariable<float>(240f);
        public readonly NetworkVariable<DaySummary> Summary = new NetworkVariable<DaySummary>();
        /// <summary>Queue patience in seconds (replicated for the patience bars).</summary>
        public readonly NetworkVariable<float> PatienceNet = new NetworkVariable<float>(45f);

        /// <summary>Every peer: a customer paid / was refused / left angry.</summary>
        public event Action<PayNotice> Paid;

        public EconomyState Economy { get; private set; }
        BalanceData.EconomyTuning E => _data.balance.economy;
        public float DayLength => DayLengthNet.Value;
        float MaxOvertime => MaxOvertimeOverride > 0f ? MaxOvertimeOverride : E.maxOvertime;
        float Now => NetworkManager != null ? (float)NetworkManager.ServerTime.Time : 0f;
        public float Elapsed => State.Value == GameState.Day ? Now - DayStart.Value : 0f;
        public float TimeLeft => Mathf.Max(0f, DayLength - Elapsed);
        public float OvertimeLeft => Mathf.Max(0f, DayLength + MaxOvertime - Elapsed);
        public int CurrentRent => EconomyCalc.Rent(Week.Value, DayNum.Value, WeekDataFor(Week.Value), Week1Rent, E);
        /// <summary>This week's chance that a customer car hides a surprise (GDD §6: 0.35 W1 → 0.8 W4).</summary>
        public float SurpriseChance => PoolWeek(Week.Value) != null ? PoolWeek(Week.Value).surpriseChance : 0.35f;

        // server bookkeeping
        System.Random _rng;
        QueueLane _lane;
        float _nextArrival, _closingStart;
        bool _bossDone, _dismissed, _forcedFinish;
        DaySummary _run;
        readonly Dictionary<Vehicle, float> _washStart = new Dictionary<Vehicle, float>();

        void Awake() => Economy = GetComponent<EconomyState>();

        public override void OnNetworkSpawn()
        {
            Instance = this;
            State.OnValueChanged += (_, s) => MirrorState(s);
            MirrorState(State.Value);
            if (IsServer)
            {
                _rng = new System.Random(Environment.TickCount);
                DayLengthNet.Value = DayLengthOverride > 0f ? DayLengthOverride : E.dayLength;
                State.Value = GameState.Lobby;
                GameEvents.VehicleArrived += OnVehicleArrived;
            }
        }

        /// <summary>Every peer: the week's boss customer pulled in (T3.7 intro).</summary>
        public event Action<string> BossArrived;

        void OnVehicleArrived(Component c)
        {
            if (c is Vehicle v && v.Data != null && v.Data.isBoss) BossRpc(v.DataId);
        }

        [Rpc(SendTo.Everyone)]
        void BossRpc(string vehicleId) => BossArrived?.Invoke(vehicleId);

        public override void OnNetworkDespawn()
        {
            GameEvents.VehicleArrived -= OnVehicleArrived;
            Unhook();
            if (Instance == this) Instance = null;
        }

        static void MirrorState(GameState s)
        {
            Services.Get<GameStateMachine>()?.Force(s);
        }

        WeekData WeekDataFor(int week)
        {
            WeekData best = null;
            foreach (var w in _data.weeks) if (w != null && w.week <= week && (best == null || w.week > best.week)) best = w;
            return best != null && best.week == week ? best : null;
        }

        WeekData PoolWeek(int week)
        {
            WeekData best = null;
            foreach (var w in _data.weeks) if (w != null && w.week <= week && (best == null || w.week > best.week)) best = w;
            return best ?? _data.weeks.FirstOrDefault();
        }

        int[] Week1Rent => _data.weeks.FirstOrDefault(w => w != null && w.week == 1)?.rentPerDay ?? new[] { 60, 80, 100, 120, 150 };

        // ---------------------------------------------------------------- lane hooks
        void Hook()
        {
            if (_lane != null || QueueLane.Instance == null) return;
            _lane = QueueLane.Instance;
            _lane.VehicleParked += OnParked;
            _lane.VehicleFinished += OnFinished;
            _lane.VehicleAngry += OnAngry;
        }

        void Unhook()
        {
            if (_lane == null) return;
            _lane.VehicleParked -= OnParked;
            _lane.VehicleFinished -= OnFinished;
            _lane.VehicleAngry -= OnAngry;
            _lane = null;
        }

        void OnParked(Vehicle v) => _washStart[v] = Now;

        void OnFinished(Vehicle v)
        {
            if (State.Value != GameState.Day || v == null) return;
            var d = v.Data;
            float finishedAt = v.PhaseStart.Value; // Inspecting started when DONE was pressed
            float start = _washStart.TryGetValue(v, out var s) ? s : finishedAt;
            _washStart.Remove(v);
            var ctx = new PayContext
            {
                basePay = d != null ? d.basePay : 0,
                cleanPct = DevCleanOverride >= 0f ? DevCleanOverride : v.Dirt.CleanPct.Value,
                trashLeft = v.TrashRemaining,
                elapsed = finishedAt - start,
                timeLimit = d != null ? d.timeLimit : 0f,
                surpriseHandledClean = v.GetComponent<SurpriseTrigger>() is SurpriseTrigger st && st.HandledClean,
                dried = IsDried(v),
                // waxed (wax tool later) / damageHits (thrown-prop hits, later) default to none
            };
            var r = EconomyCalc.CalcPay(ctx, E, _data.balance.dirt.trashPenalty);
            Economy.ServerAddMoney(r.pay);
            Economy.ServerAddRep(EconomyCalc.RepDelta(r, ctx.basePay, E));
            if (r.refused) _run.refused++; else { _run.served++; _run.earned += r.pay; _run.tips += r.tip; }
            Debug.Log($"[DAY] {v.DataId} W{Week.Value}D{DayNum.Value} {r} wash {ctx.elapsed:0}s/{ctx.timeLimit:0}s → money {Economy.Money.Value} rep {Economy.Rep.Value:0.00}");
            GameEvents.RaiseVehiclePaid(v, r.pay);
            PaidRpc(v.transform.position, v.DataId, r.pay, r.effectiveClean, r.refused, false, ctx.dried && !r.refused ? (int)System.Math.Round(ctx.basePay * E.dryTip) : 0);
        }

        /// <summary>M4.8 F3: clean enough and the water spots towelled off (server readback of the dirt map).</summary>
        bool IsDried(Vehicle v)
        {
            var p = v.Dirt != null ? v.Dirt.Painter : null;
            return p != null && v.Dirt.CleanPct.Value >= E.dryMinClean && p.SpotsPct <= E.dryMaxSpots;
        }

        void OnAngry(Vehicle v)
        {
            if (State.Value != GameState.Day || v == null) return;
            _run.angry++;
            Economy.ServerAddRep(EconomyCalc.RepAngry(E));
            Debug.Log($"[DAY] {v.DataId} left the queue angry → rep {Economy.Rep.Value:0.00}");
            PaidRpc(v.transform.position, v.DataId, 0, 0f, false, true, 0);
        }

        /// <summary>Server: a critter went into the Animal Control cage (+captureBonus, any game state).</summary>
        public void ServerCritterCaged(Critters.CritterBase c, int bonus)
        {
            if (!IsServer) return;
            Economy.ServerAddMoney(bonus);
            if (State.Value == GameState.Day) { _run.captured++; _run.bonus += bonus; }
            if (bonus > 0) CoinBurstRpc(c.transform.position + Vector3.up, bonus);
        }

        [Rpc(SendTo.Everyone)]
        void CoinBurstRpc(Vector3 pos, int amount) => CoinBurst(pos, amount);

        [Rpc(SendTo.Everyone)]
        void PaidRpc(Vector3 pos, string vehicleId, int amount, float clean, bool refused, bool angry, int dryTip)
        {
            if (amount > 0) CoinBurst(pos + Vector3.up * 1.6f, amount);
            Paid?.Invoke(new PayNotice { position = pos, vehicleId = vehicleId, amount = amount, clean = clean, refused = refused, angry = angry, dryTip = dryTip });
        }

        void CoinBurst(Vector3 pos, int amount)
        {
            if (_coinFx == null) return;
            var go = new GameObject("CoinBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 1.3f; main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.32f); main.gravityModifier = 1.1f;
            main.startColor = Color.white; main.maxParticles = 64;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(amount / 4, 6, 40)) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 30f; sh.radius = 0.3f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _coinFx;
            ps.Play();
            Destroy(go, 2.5f);
        }

        // ---------------------------------------------------------------- flow
        /// <summary>Any player: continue (start day / next day / retry / next week) — Enter on the summary or E on the day board.</summary>
        public void RequestAdvance() => AdvanceRpc();

        [Rpc(SendTo.Server)] // default InvokePermission = Everyone
        void AdvanceRpc() => ServerAdvance();

        // T4.1 lobby: the first day starts once every connected player is ready (PlayerNet.Ready, R key)
        public int PlayerCount => FindObjectsByType<Player.PlayerNet>(FindObjectsSortMode.None).Count(p => p.IsSpawned);
        public int ReadyCount => FindObjectsByType<Player.PlayerNet>(FindObjectsSortMode.None).Count(p => p.IsSpawned && p.Ready.Value);
        public bool AllReady => PlayerCount > 0 && ReadyCount == PlayerCount;

        public string AdvancePrompt
        {
            get
            {
                switch (State.Value)
                {
                    case GameState.Lobby: return AllReady ? $"영업 시작 ({Week.Value}주차 {DayNum.Value}일차)" : $"모두 준비해야 시작 ({ReadyCount}/{PlayerCount} 준비) — [R] 준비";
                    case GameState.Summary:
                        return Summary.Value.bankrupt ? "결과 확인" : DayNum.Value >= DayLogic.DaysPerWeek ? "주간 결산 보기" : "다음 날 시작";
                    case GameState.GameOver: return "1일차부터 다시 도전";
                    case GameState.WeekEnd: return $"{Week.Value + 1}주차 시작";
                    default: return null;
                }
            }
        }

        public void ServerAdvance()
        {
            if (!IsServer) return;
            switch (State.Value)
            {
                case GameState.Lobby:
                    if (AllReady) StartDay(); else Debug.Log($"[DAY] start refused: {ReadyCount}/{PlayerCount} ready");
                    break;
                case GameState.Summary:
                {
                    var next = DayLogic.AfterSummary(Summary.Value.bankrupt, DayNum.Value);
                    if (next == GameState.Shop)
                    {
                        State.Value = GameState.Shop; // DESIGN-GAP: shop terminal arrives with T5.1 → pass straight through
                        DayLogic.NextDay(Week.Value, DayNum.Value, out int w, out int d);
                        Week.Value = w; DayNum.Value = d;
                        StartDay();
                    }
                    else State.Value = next;
                    break;
                }
                case GameState.GameOver:
                    // retry the current week from Day 1 (unlocks persist: open bays stay as they are)
                    DayNum.Value = 1;
                    Economy.ServerReset();
                    StartDay();
                    break;
                case GameState.WeekEnd:
                    Week.Value = Week.Value + 1;
                    DayNum.Value = 1;
                    StartDay();
                    break;
            }
        }

        void StartDay()
        {
            Hook();
            _lane?.ServerClearAll();
            _washStart.Clear();
            _run = new DaySummary { week = Week.Value, day = DayNum.Value, moneyStart = Economy.Money.Value, repStart = Economy.Rep.Value };
            DayLengthNet.Value = DayLengthOverride > 0f ? DayLengthOverride : E.dayLength;
            DayStart.Value = Now;
            _nextArrival = Now + 2f;
            _bossDone = _dismissed = _forcedFinish = false;
            Phase.Value = DayPhase.Open;
            State.Value = GameState.Day;
            Debug.Log($"[DAY] start W{Week.Value}D{DayNum.Value} money {Economy.Money.Value} rep {Economy.Rep.Value:0.00} length {DayLength:0}s rent {CurrentRent}");
            GameEvents.RaiseDayStarted(DayNum.Value);
        }

        void EndDay()
        {
            Phase.Value = DayPhase.Closed;
            // critters from today's surprises that are still loose escape (GDD §7)
            foreach (var c in Critters.CritterBase.All.ToArray())
                if (c != null && c.FromSurprise) c.ServerEscape();
            int rent = CurrentRent;
            int money = Economy.Money.Value;
            bool bankrupt = DayLogic.IsBankrupt(money, rent);
            if (!bankrupt) Economy.ServerAddMoney(-rent);
            _run.rent = rent;
            _run.bankrupt = bankrupt;
            _run.moneyEnd = Economy.Money.Value;
            _run.repEnd = Economy.Rep.Value;
            Summary.Value = _run;
            State.Value = GameState.Summary;
            Debug.Log($"[DAY] end W{_run.week}D{_run.day}: served {_run.served} refused {_run.refused} angry {_run.angry} sentAway {_run.sentAway} earned {_run.earned} rent {rent} money {_run.moneyStart}→{_run.moneyEnd} rep {_run.repStart:0.00}→{_run.repEnd:0.00} bankrupt={bankrupt}");
            GameEvents.RaiseDayEnded(_run);
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            Hook();
            if (_lane != null && !Mathf.Approximately(PatienceNet.Value, _lane.Patience)) PatienceNet.Value = _lane.Patience;
            if (State.Value != GameState.Day || _lane == null) return;

            float t = Elapsed;
            var phase = DayLogic.PhaseAt(t, DayLength, MaxOvertime, _lane.BusyBays);

            if (phase == DayPhase.Open) TickArrivals(t);
            else if (!_dismissed)
            {
                _dismissed = true;
                _run.sentAway += _lane.ServerDismissQueue();
            }

            // a car that reaches 100% finishes by itself (TECH S06)
            foreach (var v in _lane.Active)
                if (v != null && v.State.Value == VehicleState.Washing && v.Dirt.CleanPct.Value >= 0.999f) { _lane.ServerFinish(v); break; }

            if (phase == DayPhase.Closing)
            {
                if (!_forcedFinish)
                {
                    _forcedFinish = true;
                    _closingStart = Now;
                    foreach (var v in _lane.Active.ToArray()) if (v != null && v.State.Value == VehicleState.Washing) _lane.ServerFinish(v);
                }
                // wait for the last cars to drive off the lot (safety cap 25 s, then whatever is left is removed)
                if (_lane.Active.Count == 0 || Now - _closingStart > 25f)
                {
                    _lane.ServerClearAll();
                    EndDay();
                    return;
                }
            }
            if (Phase.Value != phase) Phase.Value = phase;
        }

        void TickArrivals(float t)
        {
            if (Now < _nextArrival) return;
            string id = null;
            var week = PoolWeek(Week.Value);
            if (DayLogic.BossDay(DayNum.Value) && !_bossDone && t >= DayLength * DayLogic.BossArrivalFraction && week != null
                && !string.IsNullOrEmpty(week.bossVehicleId) && VehicleSpawner.Instance != null && VehicleSpawner.Instance.Has(week.bossVehicleId))
                id = week.bossVehicleId;
            if (id == null)
                id = DayLogic.Pick(DayLogic.Candidates(_data.vehicles, week, DayNum.Value, x => VehicleSpawner.Instance != null && VehicleSpawner.Instance.Has(x)), _rng);
            if (id == null) { _nextArrival = Now + 5f; return; }
            // DESIGN-GAP: grease needs the foam cannon (shop U05, T5.1) → customers come without grease until then
            var v = _lane.ServerArrive(id, noGrease: true);
            if (v == null) { _nextArrival = Now + 1f; return; } // queue full / road blocked → try again shortly
            if (id == week?.bossVehicleId) _bossDone = true;
            int players = NetworkManager.ConnectedClientsIds.Count;
            _nextArrival = Now + EconomyCalc.ArrivalInterval(Economy.Rep.Value, players, E);
            Debug.Log($"[DAY] arrive {id} at {t:0}s, next in {_nextArrival - Now:0.0}s (players {players})");
        }
    }
}
