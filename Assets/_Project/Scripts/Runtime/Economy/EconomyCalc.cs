using System;
using CarWash.Data;

namespace CarWash.Economy
{
    /// <summary>Everything the pay formula needs about one finished customer (GDD §8).</summary>
    [Serializable]
    public struct PayContext
    {
        public int basePay;
        /// <summary>Clean fraction 0..1 as measured (before the interior-trash penalty).</summary>
        public float cleanPct;
        /// <summary>Interior trash still inside (−trashPenalty each, GDD §5).</summary>
        public int trashLeft;
        public float elapsed, timeLimit;
        public bool waxed, surpriseHandledClean;
        public int damageHits;
    }

    [Serializable]
    public struct PayResult
    {
        public bool refused;
        public float effectiveClean, cleanMult, speedMult;
        public int tip, damage, pay;
        public override string ToString() => refused
            ? $"REFUSED (clean {effectiveClean:P0})"
            : $"${pay} (clean {effectiveClean:P0} x{cleanMult:0.##}, speed x{speedMult:0.##}, tip +{tip}, damage -{damage})";
    }

    /// <summary>
    /// Pure economy rules (TECH S08, GDD §8). All numbers come from BalanceData.economy / dirt; no Unity state.
    /// </summary>
    public static class EconomyCalc
    {
        public static float EffectiveClean(float cleanPct, int trashLeft, float trashPenalty)
            => Clamp01(Clamp01(cleanPct) - Math.Max(0, trashLeft) * trashPenalty);

        /// <summary>Clean multiplier; 0 = refuse (below the first threshold).</summary>
        public static float CleanMult(float clean, BalanceData.EconomyTuning e)
        {
            int band = 0;
            for (int i = 0; i < e.cleanThresholds.Length; i++)
                if (clean >= e.cleanThresholds[i] - 1e-5f) band = i + 1;
            return e.cleanMults[Math.Min(band, e.cleanMults.Length - 1)];
        }

        /// <summary>≤ fastFinishFraction of the time limit → fastMult; ≤ limit → 1; over → −penalty per second, floored.</summary>
        public static float SpeedMult(float elapsed, float timeLimit, BalanceData.EconomyTuning e)
        {
            if (timeLimit <= 0f) return 1f;
            if (elapsed <= timeLimit * e.fastFinishFraction) return e.fastMult;
            if (elapsed <= timeLimit) return 1f;
            return Math.Max(e.minSpeedMult, 1f - (elapsed - timeLimit) * e.overtimePenaltyPerSec);
        }

        public static PayResult CalcPay(PayContext c, BalanceData.EconomyTuning e, float trashPenalty)
        {
            var r = new PayResult { effectiveClean = EffectiveClean(c.cleanPct, c.trashLeft, trashPenalty) };
            r.cleanMult = CleanMult(r.effectiveClean, e);
            if (r.cleanMult <= 0f) { r.refused = true; return r; }
            r.speedMult = SpeedMult(c.elapsed, c.timeLimit, e);
            float tip = (c.waxed ? e.waxTip : 0f) + (c.surpriseHandledClean ? e.cleanSurpriseTip : 0f);
            r.tip = (int)Math.Round(c.basePay * tip, MidpointRounding.AwayFromZero);
            // DESIGN-GAP: GDD "cap 30%" has no base; we cap the damage at 30% of base pay.
            int cap = (int)Math.Round(c.basePay * e.damageCap, MidpointRounding.AwayFromZero);
            r.damage = Math.Min(Math.Max(0, c.damageHits) * e.damagePerHit, cap);
            float pay = c.basePay * r.cleanMult * r.speedMult + r.tip - r.damage;
            r.pay = Math.Max(0, (int)Math.Round(pay, MidpointRounding.AwayFromZero));
            return r;
        }

        /// <summary>Reputation change for one customer: +gain when paid ≥ base, −loss when refused / left angry.</summary>
        public static float RepDelta(PayResult r, int basePay, BalanceData.EconomyTuning e)
            => r.refused ? -e.repLoss : (r.pay >= basePay ? e.repGain : 0f);

        public static float RepAngry(BalanceData.EconomyTuning e) => -e.repLoss;

        public static float ApplyRep(float rep, float delta, BalanceData.EconomyTuning e)
            => Math.Max(0f, Math.Min(e.repMax, rep + delta));

        /// <summary>More players → customers arrive faster (DESIGN-GAP: player_scale = 1 + arrivalScalePerPlayer·(n−1)).</summary>
        public static float PlayerScale(int players, BalanceData.EconomyTuning e)
            => 1f + e.arrivalScalePerPlayer * Math.Max(0, Math.Min(4, players) - 1);

        /// <summary>Seconds between arrivals: lerp(slow, fast, rep/repMax) / player_scale.</summary>
        public static float ArrivalInterval(float rep, int players, BalanceData.EconomyTuning e)
        {
            float t = e.repMax > 0f ? Clamp01(rep / e.repMax) : 0f;
            return (e.arrivalSlow + (e.arrivalFast - e.arrivalSlow) * t) / PlayerScale(players, e);
        }

        /// <summary>Rent for day 1..5 of a week. Uses the week's table if present, else week-1 table × (1+increase)^(week−1).</summary>
        public static int Rent(int week, int day, WeekData weekData, int[] week1Rent, BalanceData.EconomyTuning e)
        {
            var table = weekData != null && weekData.rentPerDay != null && weekData.rentPerDay.Length > 0 ? weekData.rentPerDay : null;
            if (table != null) return table[Math.Max(0, Math.Min(table.Length - 1, day - 1))];
            if (week1Rent == null || week1Rent.Length == 0) return 0;
            int b = week1Rent[Math.Max(0, Math.Min(week1Rent.Length - 1, day - 1))];
            return (int)Math.Round(b * Math.Pow(1f + e.weeklyRentIncrease, Math.Max(0, week - 1)), MidpointRounding.AwayFromZero);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
