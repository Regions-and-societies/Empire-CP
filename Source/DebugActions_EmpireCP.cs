using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactionColonies;
using LudeonTK;
using RimWorld.Planet;
using RegionsAndSocieties;
using RegionsAndSocieties.Integration;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Debug validation for the patch (per the workspace debug-command gate): dump every Empire
    /// world object with its resolved kind, population (the #30 regression check — a real value,
    /// not zero) and level, so parity against the old reflection path is verifiable headlessly via
    /// run_debug_action.
    /// </summary>
    public static class DebugActions_EmpireCP
    {
        [DebugAction("Regions and Societies", "R&S Empire-CP: world-object dump", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap | AllowedGameStates.PlayingOnWorld)]
        private static void EmpireObjectDump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- Empire-CP world-object dump ---");
            sb.AppendLine($"player faction resolves to: {RegionsAndSocieties.Patches.RegionOwnershipHelpers.GetPlayerFaction()?.Name ?? "null"}");
            int count = 0;
            var objects = Find.WorldObjects != null ? Find.WorldObjects.AllWorldObjects : null;
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    WorldObject obj = objects[i];
                    if (obj == null || obj.GetType().Namespace != "FactionColonies") continue;

                    count++;
                    WorldObjectKind kind;
                    WorldObjectAdapterRegistry.TryClassify(obj, out kind);
                    int pop;
                    bool hasPop = WorldObjectAdapterRegistry.TryGetPopulation(obj, out pop);
                    int level, maxLevel;
                    bool hasLevel = WorldObjectAdapterRegistry.TryGetLevel(obj, out level, out maxLevel);
                    sb.AppendLine($"  {obj.GetType().Name,-28} tile={obj.Tile,-7} kind={kind,-10} pop={(hasPop ? pop.ToString() : "-"),-5} level={(hasLevel ? $"{level}/{maxLevel}" : "-"),-6} faction={obj.Faction?.Name ?? "none"}");
                }
            }
            sb.AppendLine($"{count} Empire world object(s).");
            Log.Message(sb.ToString());
        }

        /// <summary>
        /// Debug validation for region resource aggregation (issue #3): for every region that holds
        /// Empire settlements, dump each settlement's per-resource contribution beside the region
        /// aggregate, and confirm the aggregate equals the sum of its parts — so the acceptance
        /// identity is checkable headlessly via run_debug_action.
        /// </summary>
        [DebugAction("Regions and Societies", "R&S Empire-CP: region resource aggregate", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap | AllowedGameStates.PlayingOnWorld)]
        private static void RegionResourceAggregate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- Empire-CP region resource aggregate ---");

            if (!EmpireRegionResources.Available)
            {
                sb.AppendLine("Empire not active — overlay would not be offered.");
                Log.Message(sb.ToString());
                return;
            }

            var mgr = Find.World?.GetComponent<SynapseRegionManager>();
            var provinces = mgr?.Provinces;
            if (provinces == null || provinces.Count == 0)
            {
                sb.AppendLine("no regions resolved on this world.");
                Log.Message(sb.ToString());
                return;
            }

            int regionsWithSupply = 0;
            int regionsMatched = 0;
            foreach (var province in provinces)
            {
                var contributions = EmpireRegionResources.ContributionsForRegion(province);
                if (contributions.Count == 0) continue;

                regionsWithSupply++;
                string where = string.IsNullOrEmpty(province.name) ? $"region {province.id}" : province.name;
                sb.AppendLine($"[{where}] {contributions.Count} contribution(s):");
                foreach (var c in contributions)
                {
                    string label = string.IsNullOrEmpty(c.Key.label) ? c.Key.defName : c.Key.label;
                    sb.AppendLine($"    {label,-20} {c.Value,8:0.##}");
                }

                var aggregate = EmpireRegionResources.ForRegion(province);
                double aggregateSum = aggregate.Values.Sum();
                double partsSum = contributions.Sum(c => c.Value);
                bool matches = System.Math.Abs(aggregateSum - partsSum) < 0.001;
                if (matches) regionsMatched++;
                sb.AppendLine($"  aggregate: {aggregate.Count} resource type(s), total {aggregateSum:0.##} — sum-of-parts {partsSum:0.##} => {(matches ? "MATCH" : "MISMATCH")}");
                sb.AppendLine(EmpireRegionResources.TooltipFor(province) ?? "  (no tooltip)");
            }

            if (regionsWithSupply == 0) sb.AppendLine("no region currently supplies any resource.");

            // Tier-2 line, discoverable headlessly via read_rimworld_log. WARN (not FAIL) when no
            // region supplies anything yet — that is an empty fixture, not a broken aggregate.
            string verdict = regionsWithSupply == 0 ? "WARN" : (regionsMatched == regionsWithSupply ? "PASS" : "FAIL");
            sb.AppendLine($"[SYNAPSE-TEST] {verdict} Regions_EmpireResourceAggregateMatchesSettlements (#3) | "
                        + $"regionsWithSupply={regionsWithSupply} matched={regionsMatched}");
            Log.Message(sb.ToString());
        }
    }
}
