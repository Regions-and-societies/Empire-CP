using System.Text;
using LudeonTK;
using RimWorld.Planet;
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
    }
}
