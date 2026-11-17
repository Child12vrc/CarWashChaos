using System.Collections.Generic;
using System.Linq;

namespace CarWash.Data
{
    /// <summary>Pure validation of loaded data (unique ids, references, sane ranges). Used by DataBuilder + tests.</summary>
    public static class DataValidator
    {
        public static List<string> Validate(BalanceData balance, IList<ToolData> tools, IList<VehicleData> vehicles,
            IList<CritterData> critters, IList<UpgradeData> upgrades, IList<WeekData> weeks)
        {
            var errors = new List<string>();
            var all = new List<(string kind, string id)>();
            all.AddRange(tools.Select(t => ("tool", t.id)));
            all.AddRange(vehicles.Select(v => ("vehicle", v.id)));
            all.AddRange(critters.Select(c => ("critter", c.id)));
            all.AddRange(upgrades.Select(u => ("upgrade", u.id)));
            all.AddRange(weeks.Select(w => ("week", w.id)));

            foreach (var e in all.Where(e => string.IsNullOrWhiteSpace(e.id))) errors.Add($"{e.kind}: empty id");
            foreach (var g in all.Where(e => !string.IsNullOrWhiteSpace(e.id)).GroupBy(e => e.id).Where(g => g.Count() > 1))
                errors.Add($"duplicate id '{g.Key}'");

            var critterIds = new HashSet<string>(critters.Select(c => c.id));
            var vehicleIds = new HashSet<string>(vehicles.Select(v => v.id));

            foreach (var t in tools)
            {
                if (t.range < 0 || t.weight < 0) errors.Add($"{t.id}: negative range/weight");
                if (t.rays < 0 || t.coneDeg < 0 || t.coneDeg > 180) errors.Add($"{t.id}: bad rays/cone");
            }
            foreach (var v in vehicles)
            {
                if (v.basePay <= 0) errors.Add($"{v.id}: basePay must be > 0");
                if (v.timeLimit <= 0) errors.Add($"{v.id}: timeLimit must be > 0");
                if (!System.Enum.TryParse(v.size, out VehicleSize _)) errors.Add($"{v.id}: bad size '{v.size}'");
                if (v.dirt.x < 0 || v.dirt.x > 1 || v.dirt.y < 0 || v.dirt.y > 1 || v.dirt.z < 0 || v.dirt.z > 1) errors.Add($"{v.id}: dirt out of 0..1");
                if (v.textureSize < 64 || (v.textureSize & (v.textureSize - 1)) != 0) errors.Add($"{v.id}: textureSize must be power of 2 ≥ 64");
                foreach (var s in v.surprisePool)
                {
                    if (!critterIds.Contains(s.critterId)) errors.Add($"{v.id}: unknown critter '{s.critterId}'");
                    if (s.weight < 0 || s.count < 1) errors.Add($"{v.id}: bad surprise weight/count");
                }
            }
            foreach (var c in critters)
            {
                if (!System.Enum.TryParse(c.trigger, out TriggerKind _)) errors.Add($"{c.id}: bad trigger '{c.trigger}'");
                if (c.maxWetness <= 0 || c.stunTime < 0 || c.moveSpeed < 0) errors.Add($"{c.id}: bad numbers");
            }
            foreach (var u in upgrades)
            {
                if (u.costs.Length == 0) errors.Add($"{u.id}: no costs");
                if (u.values.Length != u.costs.Length) errors.Add($"{u.id}: values/costs length mismatch");
                if (u.costs.Any(c => c <= 0)) errors.Add($"{u.id}: cost must be > 0");
            }
            foreach (var w in weeks)
            {
                if (w.rentPerDay.Length != 5) errors.Add($"{w.id}: rentPerDay needs 5 days");
                if (w.surpriseChance < 0 || w.surpriseChance > 1) errors.Add($"{w.id}: surpriseChance out of 0..1");
                if (!string.IsNullOrEmpty(w.bossVehicleId) && !vehicleIds.Contains(w.bossVehicleId)) errors.Add($"{w.id}: unknown boss '{w.bossVehicleId}'");
                foreach (var lv in w.lateVehicles)
                    if (!vehicleIds.Contains(lv.Split('@')[0])) errors.Add($"{w.id}: unknown late vehicle '{lv}'");
            }
            if (balance == null) errors.Add("balance missing");
            else
            {
                var e = balance.economy;
                if (e.cleanMults.Length != e.cleanThresholds.Length + 1) errors.Add("balance: cleanMults must be thresholds+1");
                for (int i = 1; i < e.cleanThresholds.Length; i++)
                    if (e.cleanThresholds[i] <= e.cleanThresholds[i - 1]) errors.Add("balance: cleanThresholds must ascend");
                if (balance.player.walkSpeed <= 0 || balance.player.gravity <= 0) errors.Add("balance: bad player tuning");
            }
            return errors;
        }
    }
}
