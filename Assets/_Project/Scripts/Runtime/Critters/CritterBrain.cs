using System;

namespace CarWash.Critters
{
    /// <summary>TECH S07 states (+ Carried: in a net or someone's arms on the way to the cage).</summary>
    public enum CritterState : byte { Hidden, Reveal, Idle, Wander, Chase, Flee, Steal, Act, Stunned, Carried, Captured, Escaped }

    /// <summary>
    /// Pure critter state machine (TECH S07, EditMode tested). Owns wetness (HP), stun and carry rules; species
    /// behaviour (CritterBase subclasses) only picks between the free-roaming states via TrySet.
    /// </summary>
    [Serializable]
    public class CritterBrain
    {
        public const float RevealTime = 1f;

        public CritterState State { get; private set; } = CritterState.Hidden;
        public float StateTime { get; private set; }
        public float Wetness { get; private set; }
        public float MaxWetness { get; }
        public float StunTime { get; }
        public float WetnessDecay { get; }
        public bool NeedsStun { get; }
        public bool Capturable { get; }
        /// <summary>Carried in a net (secure) or by hand (escapes when the stun would have worn off).</summary>
        public bool CarrySecure { get; private set; }
        float _carryStunLeft;

        public event Action<CritterState, CritterState> Changed;

        public CritterBrain(float maxWetness, float stunTime, float wetnessDecay, bool needsStun, bool capturable, bool startHidden = true)
        {
            MaxWetness = Math.Max(1f, maxWetness);
            StunTime = stunTime;
            WetnessDecay = wetnessDecay;
            NeedsStun = needsStun;
            Capturable = capturable;
            State = startHidden ? CritterState.Hidden : CritterState.Idle;
        }

        /// <summary>hp_mult = 1 + perPlayer·(players−1) (GDD §7).</summary>
        public static float HpScale(int players, float perPlayer) => 1f + perPlayer * Math.Max(0, Math.Min(4, players) - 1);

        public bool IsFree => State == CritterState.Idle || State == CritterState.Wander || State == CritterState.Chase ||
                              State == CritterState.Flee || State == CritterState.Steal || State == CritterState.Act;
        public bool IsGone => State == CritterState.Captured || State == CritterState.Escaped;

        public bool CanCapture => Capturable && (State == CritterState.Stunned || (!NeedsStun && IsFree));

        void Enter(CritterState s)
        {
            if (s == State) return;
            var from = State;
            State = s;
            StateTime = 0f;
            Changed?.Invoke(from, s);
        }

        /// <summary>Hidden → Reveal (1 s sting) → Idle.</summary>
        public bool Reveal()
        {
            if (State != CritterState.Hidden) return false;
            Enter(CritterState.Reveal);
            return true;
        }

        /// <summary>Behaviour change between the free states (ignored while hidden/revealing/stunned/carried/gone).</summary>
        public bool TrySet(CritterState s)
        {
            if (!IsFree) return false;
            if (s != CritterState.Idle && s != CritterState.Wander && s != CritterState.Chase && s != CritterState.Flee &&
                s != CritterState.Steal && s != CritterState.Act) return false;
            Enter(s);
            return true;
        }

        /// <summary>Water hits. Returns true when this hit stunned the critter.</summary>
        public bool AddWetness(float amount)
        {
            if (amount <= 0f || State == CritterState.Hidden || State == CritterState.Carried || IsGone || State == CritterState.Stunned) return false;
            Wetness = Math.Min(MaxWetness, Wetness + amount);
            if (Wetness >= MaxWetness && StunTime > 0f) { Enter(CritterState.Stunned); return true; }
            return false;
        }

        public void Tick(float dt)
        {
            StateTime += dt;
            switch (State)
            {
                case CritterState.Reveal:
                    if (StateTime >= RevealTime) Enter(CritterState.Idle);
                    break;
                case CritterState.Stunned:
                    if (StateTime >= StunTime) { Wetness = 0f; Enter(CritterState.Flee); }
                    break;
                case CritterState.Carried:
                    if (!CarrySecure)
                    {
                        _carryStunLeft -= dt;
                        if (_carryStunLeft <= 0f) { Wetness = 0f; Enter(CritterState.Flee); } // wriggles free
                    }
                    break;
                default:
                    if (IsFree) Wetness = Math.Max(0f, Wetness - WetnessDecay * dt);
                    break;
            }
        }

        /// <summary>Pick up: net = secure until released; hand = only for the rest of the stun (or a few seconds when no stun is needed).</summary>
        public bool TryCarry(bool secure, float handTimeWithoutStun = 6f)
        {
            if (!CanCapture) return false;
            _carryStunLeft = State == CritterState.Stunned ? Math.Max(0.5f, StunTime - StateTime) : handTimeWithoutStun;
            CarrySecure = secure;
            Enter(CritterState.Carried);
            return true;
        }

        /// <summary>Dropped / net released → runs off.</summary>
        public void Release()
        {
            if (State != CritterState.Carried) return;
            Wetness = 0f;
            Enter(CritterState.Flee);
        }

        /// <summary>M4.8 F5: thrown by a player → dizzy (stunned) for the flight + the usual stun time after it.</summary>
        public void Stun()
        {
            if (IsGone || State == CritterState.Hidden) return;
            Wetness = MaxWetness;
            Enter(CritterState.Stunned);
        }

        /// <summary>Put in the Animal Control cage.</summary>
        public bool Capture()
        {
            if (State != CritterState.Carried && !CanCapture) return false;
            Enter(CritterState.Captured);
            return true;
        }

        /// <summary>Still loose at day end.</summary>
        public void Escape() { if (!IsGone) Enter(CritterState.Escaped); }
    }
}
