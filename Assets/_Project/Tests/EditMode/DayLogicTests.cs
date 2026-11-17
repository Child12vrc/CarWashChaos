using System.Linq;
using CarWash.Core;
using CarWash.Day;
using CarWash.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>T2.4 pure day rules against the real data (weeks.json / vehicles.json).</summary>
    public class DayLogicTests
    {
        DataBuilder.Loaded D;
        [OneTimeSetUp] public void Load() => D = DataBuilder.LoadSources();
        Data.WeekData W(int n) => D.weeks.First(w => w.week == n);

        [Test] public void Week1_Day1_Pool_IsTier1Only()
            => CollectionAssert.AreEquivalent(new[] { "V01", "V02", "V03" }, DayLogic.Candidates(D.vehicles, W(1), 1));

        [Test] public void Week1_LateVehicle_V04_FromDay4()
        {
            Assert.IsFalse(DayLogic.Candidates(D.vehicles, W(1), 3).Contains("V04"));
            Assert.IsTrue(DayLogic.Candidates(D.vehicles, W(1), 4).Contains("V04"));
        }

        [Test] public void Pool_NeverContainsBosses()
        {
            foreach (var w in D.weeks)
                for (int d = 1; d <= 5; d++)
                    Assert.IsFalse(DayLogic.Candidates(D.vehicles, w, d).Any(id => id.StartsWith("B")), $"W{w.week}D{d}");
        }

        [Test] public void Pool_RespectsSpawnableFilter()
            => CollectionAssert.AreEquivalent(new[] { "V01" }, DayLogic.Candidates(D.vehicles, W(1), 1, id => id == "V01"));

        [Test] public void Pick_EmptyPool_IsNull_AndPickIsFromPool()
        {
            Assert.IsNull(DayLogic.Pick(new System.Collections.Generic.List<string>(), new System.Random(1)));
            var pool = DayLogic.Candidates(D.vehicles, W(1), 1);
            var rng = new System.Random(5);
            for (int i = 0; i < 50; i++) CollectionAssert.Contains(pool, DayLogic.Pick(pool, rng));
        }

        [Test] public void Bankrupt_WhenMoneyBelowRent()
        {
            Assert.IsTrue(DayLogic.IsBankrupt(59, 60));
            Assert.IsFalse(DayLogic.IsBankrupt(60, 60));
        }

        [Test] public void AfterSummary_Routes()
        {
            Assert.AreEqual(GameState.GameOver, DayLogic.AfterSummary(true, 2));
            Assert.AreEqual(GameState.GameOver, DayLogic.AfterSummary(true, 5));
            Assert.AreEqual(GameState.Shop, DayLogic.AfterSummary(false, 4));
            Assert.AreEqual(GameState.WeekEnd, DayLogic.AfterSummary(false, 5));
        }

        [Test] public void AfterSummary_TargetsAreAllowedTransitions()
        {
            foreach (var s in new[] { GameState.GameOver, GameState.Shop, GameState.WeekEnd })
                Assert.IsTrue(GameStateMachine.IsAllowed(GameState.Summary, s), s.ToString());
            Assert.IsTrue(GameStateMachine.IsAllowed(GameState.GameOver, GameState.Day));
            Assert.IsTrue(GameStateMachine.IsAllowed(GameState.WeekEnd, GameState.Day));
            Assert.IsTrue(GameStateMachine.IsAllowed(GameState.Shop, GameState.Day));
        }

        [Test] public void NextDay_WrapsToNextWeek()
        {
            DayLogic.NextDay(1, 4, out int w, out int d); Assert.AreEqual((1, 5), (w, d));
            DayLogic.NextDay(1, 5, out w, out d); Assert.AreEqual((2, 1), (w, d));
        }

        [Test] public void Phase_OpenThenOvertimeWhileBusyThenClosing()
        {
            Assert.AreEqual(DayPhase.Open, DayLogic.PhaseAt(10f, 240f, 60f, 2));
            Assert.AreEqual(DayPhase.Overtime, DayLogic.PhaseAt(250f, 240f, 60f, 1));
            Assert.AreEqual(DayPhase.Closing, DayLogic.PhaseAt(250f, 240f, 60f, 0));
            Assert.AreEqual(DayPhase.Closing, DayLogic.PhaseAt(300f, 240f, 60f, 3)); // overtime capped
        }

        [Test] public void Boss_OnlyOnLastDay_AndEveryWeekHasOne()
        {
            Assert.IsTrue(DayLogic.BossDay(5)); Assert.IsFalse(DayLogic.BossDay(4));
            foreach (var w in D.weeks) Assert.IsTrue(D.vehicles.Any(v => v.id == w.bossVehicleId && v.isBoss), w.id);
        }

        [Test] public void Week1_StartMoney_CannotCoverDay1RentWithoutCustomers()
        {
            // GDD tension: $50 start < $60 day-1 rent → you must wash at least one car on day 1
            Assert.IsTrue(DayLogic.IsBankrupt(D.balance.economy.startMoney, W(1).rentPerDay[0]));
        }

        /// <summary>
        /// T3.7 AC "Week 1 completable solo in ~20-25 min": a solo player who washes one tier-1 car every ~75 s to
        /// ~82% (normal band, on time) survives every day's rent, the day-5 boss is worth its time, and five days of
        /// play (day + average half overtime + ~20 s summary) land in 20-25 minutes.
        /// </summary>
        [Test] public void Week1_SoloPlaythrough_SurvivesAndFitsTime()
        {
            var e = D.balance.economy;
            var w1 = W(1);
            float carTime = 75f;                                   // walk + foam/wash + DONE, solo
            int carsPerDay = (int)((e.dayLength + e.maxOvertime * 0.5f) / carTime);
            int money = e.startMoney; float rep = e.repStart;
            for (int day = 1; day <= 5; day++)
            {
                var pool = DayLogic.Candidates(D.vehicles, w1, day);
                int earned = 0;
                for (int i = 0; i < carsPerDay; i++)
                {
                    var v = D.vehicles.First(x => x.id == pool[i % pool.Count]);
                    if (day == 5 && i == 1) v = D.vehicles.First(x => x.id == w1.bossVehicleId); // boss replaces a car
                    var r = Economy.EconomyCalc.CalcPay(new Economy.PayContext { basePay = v.basePay, cleanPct = 0.82f, elapsed = Mathf.Min(carTime * (v.isBoss ? 3f : 1f), v.timeLimit), timeLimit = v.timeLimit }, e, D.balance.dirt.trashPenalty);
                    earned += r.pay;
                    rep = Economy.EconomyCalc.ApplyRep(rep, Economy.EconomyCalc.RepDelta(r, v.basePay, e), e);
                }
                int rent = Economy.EconomyCalc.Rent(1, day, w1, w1.rentPerDay, e);
                money += earned;
                Assert.IsFalse(DayLogic.IsBankrupt(money, rent), $"day {day}: money {money} < rent {rent}");
                money -= rent;
            }
            float minutes = 5f * (e.dayLength + e.maxOvertime * 0.5f + 20f) / 60f;
            Assert.That(minutes, Is.InRange(20f, 25f), $"week length {minutes:0.0} min");
            Assert.Greater(money, 0);
        }
    }
}
