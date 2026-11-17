using System.Linq;
using CarWash.Data;
using CarWash.Economy;
using CarWash.Editor;
using NUnit.Framework;

namespace CarWash.Tests
{
    /// <summary>T2.3: GDD §8 pay / rep / arrival / rent rules against the real balance.json numbers.</summary>
    public class EconomyCalcTests
    {
        BalanceData.EconomyTuning E;
        float Trash;
        DataBuilder.Loaded D;

        [OneTimeSetUp]
        public void Load()
        {
            D = DataBuilder.LoadSources();
            E = D.balance.economy;
            Trash = D.balance.dirt.trashPenalty;
        }

        PayContext Ctx(float clean, float elapsed = 50f, float limit = 90f, int basePay = 40)
            => new PayContext { basePay = basePay, cleanPct = clean, elapsed = elapsed, timeLimit = limit };

        PayResult Pay(PayContext c) => EconomyCalc.CalcPay(c, E, Trash);

        // --- clean bands ---
        [Test] public void Clean59_IsRefused() { var r = Pay(Ctx(0.59f)); Assert.IsTrue(r.refused); Assert.AreEqual(0, r.pay); }
        [Test] public void Clean60_PaysPoint6() { var r = Pay(Ctx(0.60f)); Assert.IsFalse(r.refused); Assert.AreEqual(0.6f, r.cleanMult, 1e-4); Assert.AreEqual(24, r.pay); }
        [Test] public void Clean79_StillPoint6() => Assert.AreEqual(0.6f, Pay(Ctx(0.79f)).cleanMult, 1e-4);
        [Test] public void Clean80_IsFull() { var r = Pay(Ctx(0.80f)); Assert.AreEqual(1f, r.cleanMult, 1e-4); Assert.AreEqual(40, r.pay); }
        [Test] public void Clean95_Bonus() { var r = Pay(Ctx(0.95f)); Assert.AreEqual(1.3f, r.cleanMult, 1e-4); Assert.AreEqual(52, r.pay); }
        [Test] public void Clean100_Bonus() => Assert.AreEqual(1.3f, Pay(Ctx(1f)).cleanMult, 1e-4);
        [Test] public void CleanOutOfRange_IsClamped() { Assert.AreEqual(1f, Pay(Ctx(1.7f)).effectiveClean, 1e-4); Assert.IsTrue(Pay(Ctx(-0.3f)).refused); }

        // --- trash ---
        [Test] public void Trash_TwoPercentEach_CanDropBand()
        {
            var c = Ctx(0.96f); c.trashLeft = 1; // 0.94 → 1.0 band
            var r = Pay(c);
            Assert.AreEqual(0.94f, r.effectiveClean, 1e-4); Assert.AreEqual(1f, r.cleanMult, 1e-4);
        }
        [Test] public void Trash_CanCauseRefusal() { var c = Ctx(0.62f); c.trashLeft = 2; Assert.IsTrue(Pay(c).refused); }

        // --- speed ---
        [Test] public void Speed_HalfTimeOrLess_Is1Point2() { Assert.AreEqual(1.2f, EconomyCalc.SpeedMult(45f, 90f, E), 1e-4); Assert.AreEqual(1.2f, EconomyCalc.SpeedMult(0f, 90f, E), 1e-4); }
        [Test] public void Speed_WithinLimit_Is1() { Assert.AreEqual(1f, EconomyCalc.SpeedMult(45.1f, 90f, E), 1e-4); Assert.AreEqual(1f, EconomyCalc.SpeedMult(90f, 90f, E), 1e-4); }
        [Test] public void Speed_Overtime_OnePercentPerSecond() => Assert.AreEqual(0.8f, EconomyCalc.SpeedMult(110f, 90f, E), 1e-4);
        [Test] public void Speed_Overtime_FloorsAtHalf() => Assert.AreEqual(0.5f, EconomyCalc.SpeedMult(1000f, 90f, E), 1e-4);
        [Test] public void Speed_NoTimeLimit_Is1() => Assert.AreEqual(1f, EconomyCalc.SpeedMult(500f, 0f, E), 1e-4);
        [Test] public void Speed_FastAndSparkling_Stack() => Assert.AreEqual(62, Pay(Ctx(1f, elapsed: 20f)).pay); // 40*1.3*1.2=62.4

        // --- tips & damage ---
        [Test] public void Tips_WaxAndCleanSurprise() { var c = Ctx(0.85f); c.waxed = true; c.surpriseHandledClean = true; var r = Pay(c); Assert.AreEqual(12, r.tip); Assert.AreEqual(52, r.pay); }
        [Test] public void Tips_NotPaidWhenRefused() { var c = Ctx(0.3f); c.waxed = true; Assert.AreEqual(0, Pay(c).tip); }
        [Test] public void Damage_FivePerHit() { var c = Ctx(0.85f); c.damageHits = 2; var r = Pay(c); Assert.AreEqual(10, r.damage); Assert.AreEqual(30, r.pay); }
        [Test] public void Damage_CappedAt30PercentOfBase() { var c = Ctx(0.85f); c.damageHits = 50; var r = Pay(c); Assert.AreEqual(12, r.damage); Assert.AreEqual(28, r.pay); }
        [Test] public void Pay_NeverNegative() { var c = Ctx(0.6f, elapsed: 999f, basePay: 10); c.damageHits = 10; Assert.GreaterOrEqual(Pay(c).pay, 0); }

        // --- reputation ---
        [Test] public void Rep_GainWhenPaidAtLeastBase() => Assert.AreEqual(E.repGain, EconomyCalc.RepDelta(Pay(Ctx(0.8f)), 40, E), 1e-4);
        [Test] public void Rep_NoGainBelowBase() => Assert.AreEqual(0f, EconomyCalc.RepDelta(Pay(Ctx(0.7f)), 40, E), 1e-4);
        [Test] public void Rep_LossOnRefuseAndAngry()
        {
            Assert.AreEqual(-E.repLoss, EconomyCalc.RepDelta(Pay(Ctx(0.1f)), 40, E), 1e-4);
            Assert.AreEqual(-E.repLoss, EconomyCalc.RepAngry(E), 1e-4);
        }
        [Test] public void Rep_ClampedZeroToMax()
        {
            Assert.AreEqual(5f, EconomyCalc.ApplyRep(4.95f, 0.1f, E), 1e-4);
            Assert.AreEqual(0f, EconomyCalc.ApplyRep(0.1f, -0.3f, E), 1e-4);
        }

        // --- arrivals ---
        [Test] public void Arrival_RepEndpoints_Solo()
        {
            Assert.AreEqual(30f, EconomyCalc.ArrivalInterval(0f, 1, E), 1e-3);
            Assert.AreEqual(12f, EconomyCalc.ArrivalInterval(5f, 1, E), 1e-3);
            Assert.AreEqual(19.2f, EconomyCalc.ArrivalInterval(3f, 1, E), 1e-3); // start rep
        }
        [Test] public void Arrival_MorePlayers_Faster_AndCappedAt4()
        {
            float one = EconomyCalc.ArrivalInterval(3f, 1, E), four = EconomyCalc.ArrivalInterval(3f, 4, E);
            Assert.Less(four, one);
            Assert.AreEqual(four, EconomyCalc.ArrivalInterval(3f, 9, E), 1e-4);
            Assert.AreEqual(one, EconomyCalc.ArrivalInterval(3f, 0, E), 1e-4);
        }

        // --- rent ---
        [Test] public void Rent_Week1Table()
        {
            var w1 = D.weeks.First(w => w.week == 1);
            CollectionAssert.AreEqual(new[] { 60, 80, 100, 120, 150 }, Enumerable.Range(1, 5).Select(d => EconomyCalc.Rent(1, d, w1, w1.rentPerDay, E)));
        }
        [Test] public void Rent_WeekTablesMatch40PercentRule()
        {
            var w1 = D.weeks.First(w => w.week == 1);
            foreach (var w in D.weeks)
                for (int d = 1; d <= 5; d++)
                    Assert.AreEqual(EconomyCalc.Rent(w.week, d, null, w1.rentPerDay, E), EconomyCalc.Rent(w.week, d, w, w1.rentPerDay, E), 1.01, $"W{w.week} D{d}");
        }
        [Test] public void Rent_DayOutOfRange_Clamped()
        {
            var w1 = D.weeks.First(w => w.week == 1);
            Assert.AreEqual(150, EconomyCalc.Rent(1, 9, w1, w1.rentPerDay, E));
            Assert.AreEqual(60, EconomyCalc.Rent(1, 0, w1, w1.rentPerDay, E));
        }
    }
}
