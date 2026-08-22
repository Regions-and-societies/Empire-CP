using System.Linq;
using System.Reflection;
using System.Text;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using LudeonTK;
using RimWorld.Planet;
using RegionsAndSocieties.Integration;
using RegionsAndSocieties.Sizing;
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
        /// Debug validation for the tier-based hiring cost (issue #2): names the resolved target
        /// method and the +25/+10/0/-10/-25 ladder, then dumps each Empire settlement's tier and the
        /// price the laborer quote would carry there, so both the binding and the direction are
        /// checkable headlessly via run_debug_action — a bigger settlement must quote cheaper.
        /// </summary>
        [DebugAction("Regions and Societies", "R&S Empire-CP: hiring cost by tier", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap | AllowedGameStates.PlayingOnWorld)]
        private static void HiringCostByTier()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- Empire-CP hiring cost by settlement tier ---");

            MethodInfo target = AccessTools.Method(typeof(LaborerHireUtil), "CalculateCost", new[] { typeof(int), typeof(int) });
            sb.AppendLine($"resolved target: {(target == null ? "NOT FOUND" : target.DeclaringType.FullName + "." + target.Name + "(int,int)")}");

            int count = LaborerHireUtil.CalculateCount();
            int baseCost = count * FCSettings.laborerCostPerDay * FCSettings.laborerDurationDays;
            sb.AppendLine($"base quote: {count} laborers x {FCSettings.laborerCostPerDay}/day x {FCSettings.laborerDurationDays} days = {baseCost} silver (unadjusted)");

            sb.Append("ladder:");
            foreach (SettlementTier t in new[] { SettlementTier.Village, SettlementTier.Town, SettlementTier.City, SettlementTier.MajorCity, SettlementTier.Metropolis })
            {
                sb.Append($"  {HiringCostModel.TierLabel(t)} {HiringCostModel.PercentLabel(t)}");
            }
            sb.AppendLine();

            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0)
            {
                sb.AppendLine("no Empire settlements on this world.");
                Log.Message(sb.ToString());
                return;
            }

            foreach (var s in settlements.OrderBy(s => (int)HiringCostPatches.TierOf(s)))
            {
                if (s == null) continue;
                SettlementTier tier = HiringCostPatches.TierOf(s);
                int scaled = HiringCostModel.Scale(baseCost, tier);
                sb.AppendLine($"  {s.Name,-24} tier={HiringCostModel.TierLabel(tier),-11} {HiringCostModel.PercentLabel(tier),-5} -> {scaled} silver");
            }

            Log.Message(sb.ToString());
        }
    }
}
