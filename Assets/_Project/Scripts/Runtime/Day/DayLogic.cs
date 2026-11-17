using System;
using System.Collections.Generic;
using CarWash.Core;
using CarWash.Data;
using Unity.Netcode;

namespace CarWash.Day
{
    /// <summary>Phase inside a running day (GDD §8: 240 s + overtime until bays are empty, max 60 s).</summary>
    public enum DayPhase : byte { Closed, Open, Overtime, Closing }

    /// <summary>End-of-day report (replicated; shown on every client).</summary>
    public struct DaySummary : INetworkSerializable, IEquatable<DaySummary>
    {
        public int week, day;
        public int served, refused, angry, sentAway, captured, bonus;
        public int earned, tips, rent;
        public int moneyStart, moneyEnd;
        public float repStart, repEnd;
        public bool bankrupt;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref week); s.SerializeValue(ref day);
            s.SerializeValue(ref served); s.SerializeValue(ref refused); s.SerializeValue(ref angry); s.SerializeValue(ref sentAway);
            s.SerializeValue(ref captured); s.SerializeValue(ref bonus);
            s.SerializeValue(ref earned); s.SerializeValue(ref tips); s.SerializeValue(ref rent);
            s.SerializeValue(ref moneyStart); s.SerializeValue(ref moneyEnd);
            s.SerializeValue(ref repStart); s.SerializeValue(ref repEnd);
            s.SerializeValue(ref bankrupt);
        }

        public bool Equals(DaySummary o) => week == o.week && day == o.day && served == o.served && refused == o.refused &&
            angry == o.angry && sentAway == o.sentAway && captured == o.captured && bonus == o.bonus && earned == o.earned && tips == o.tips && rent == o.rent &&
            moneyStart == o.moneyStart && moneyEnd == o.moneyEnd && repStart == o.repStart && repEnd == o.repEnd && bankrupt == o.bankrupt;
        public override bool Equals(object o) => o is DaySummary d && Equals(d);
        public override int GetHashCode() => (week * 31 + day) * 31 + earned;
    }

    /// <summary>Pure day/week rules (TECH S06). DayManager (server) calls these; EditMode tested.</summary>
    public static class DayLogic
    {
        public const int DaysPerWeek = 5;

        /// <summary>
        /// Customer pool for a day: non-boss vehicles whose tier is in the week's tiers, plus "ID@N" late vehicles from
        /// day N; a late vehicle is excluded before its day even if its tier matches. Only ids the spawner knows.
        /// </summary>
        public static List<string> Candidates(IEnumerable<VehicleData> all, WeekData week, int day, Func<string, bool> spawnable = null)
        {
            var late = new Dictionary<string, int>();
            if (week != null && week.lateVehicles != null)
                foreach (var l in week.lateVehicles)
                {
                    var parts = l.Split('@');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int d)) late[parts[0]] = d;
                }
            var r = new List<string>();
            foreach (var v in all)
            {
                if (v == null || v.isBoss) continue;
                if (spawnable != null && !spawnable(v.id)) continue;
                bool tierOk = week != null && Array.IndexOf(week.tiers, v.tier) >= 0;
                if (late.TryGetValue(v.id, out int from)) { if (day >= from) r.Add(v.id); }
                else if (tierOk) r.Add(v.id);
            }
            return r;
        }

        public static string Pick(List<string> candidates, Random rng)
            => candidates == null || candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];

        /// <summary>The week's boss arrives once on the last day, at this fraction of the day.</summary>
        public const float BossArrivalFraction = 0.3f;
        public static bool BossDay(int day) => day == DaysPerWeek;

        public static bool IsBankrupt(int money, int rent) => money < rent;

        /// <summary>Where the summary leads: bankrupt → GameOver; last day → WeekEnd; else Shop (→ next day).</summary>
        public static GameState AfterSummary(bool bankrupt, int day)
            => bankrupt ? GameState.GameOver : day >= DaysPerWeek ? GameState.WeekEnd : GameState.Shop;

        public static void NextDay(int week, int day, out int nextWeek, out int nextDay)
        {
            if (day >= DaysPerWeek) { nextWeek = week + 1; nextDay = 1; }
            else { nextWeek = week; nextDay = day + 1; }
        }

        /// <summary>Phase for elapsed day time `t`. Overtime lasts while bays are busy, at most maxOvertime.</summary>
        public static DayPhase PhaseAt(float t, float dayLength, float maxOvertime, int busyBays)
        {
            if (t < dayLength) return DayPhase.Open;
            if (busyBays > 0 && t < dayLength + maxOvertime) return DayPhase.Overtime;
            return DayPhase.Closing;
        }
    }
}
