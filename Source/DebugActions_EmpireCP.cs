using System.Linq;
using System.Reflection;
using System.Text;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
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
        /// Debug validation for the regional hiring cost (issue #2): names the resolved target method,
        /// then dumps the base laborer cost and its scaled price against several of the world's regions
        /// ordered by population, so both the binding and the scaling direction are checkable headlessly
        /// via run_debug_action — a crowded region must quote cheaper than an empty one.
        /// </summary>
        [DebugAction("Regions and Societies", "R&S Empire-CP: hiring cost by region", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap | AllowedGameStates.PlayingOnWorld)]
        private static void HiringCostByRegion()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- Empire-CP regional hiring cost ---");

            MethodInfo target = AccessTools.Method(typeof(LaborerHireUtil), "CalculateCost", new[] { typeof(int), typeof(int) });
            sb.AppendLine($"resolved target: {(target == null ? "NOT FOUND" : target.DeclaringType.FullName + "." + target.Name + "(int,int)")}");

            int count = LaborerHireUtil.CalculateCount();
            int baseCost = count * FCSettings.laborerCostPerDay * FCSettings.laborerDurationDays;
            sb.AppendLine($"base quote: {count} laborers x {FCSettings.laborerCostPerDay}/day x {FCSettings.laborerDurationDays} days = {baseCost} silver (unscaled)");
            sb.AppendLine($"bounds: population 0 -> x{RegionalHiringCost.MaxFactor:0.00}, dense -> x{RegionalHiringCost.MinFactor:0.00}, pivot at {RegionalHiringCost.PivotPopulation:0} pop");

            var mgr = Find.World?.GetComponent<SynapseRegionManager>();
            var provinces = mgr?.Provinces;
            if (provinces == null || provinces.Count == 0)
            {
                sb.AppendLine("no regions resolved on this world.");
                Log.Message(sb.ToString());
                return;
            }

            var sample = provinces
                .OrderByDescending(p => p.currentPopulation)
                .Where((p, i) => i < 4 || i >= provinces.Count - 4)   // densest few and emptiest few
                .Distinct();

            foreach (var p in sample)
            {
                int pop = p.currentPopulation;
                float factor = RegionalHiringCost.Factor(pop);
                int scaled = RegionalHiringCost.Scale(baseCost, pop);
                string where = string.IsNullOrEmpty(p.name) ? $"region {p.id}" : p.name;
                sb.AppendLine($"  {where,-24} pop={pop,-5} x{factor:0.00} -> {scaled,-6} ({RegionalHiringCost.Direction(pop)})");
            }

            Log.Message(sb.ToString());
        }
    }
}
