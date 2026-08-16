using HarmonyLib;
using RegionsAndSocieties;
using RegionsAndSocieties.Integration;
using RegionsAndSocieties.Patches;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Mod entry. Loads after Regions and Societies core and Empire Refactored; this constructor is
    /// the whole integration wiring:
    /// 1. the typed adapter joins core's registry (classification, population, level, ownership),
    /// 2. PatchAll applies the typed Harmony patches (economy rewards, tithe, settlement placement,
    ///    debug-utils),
    /// 3. core's player-faction seam resolves to Empire's faction component, so core's own patches
    ///    (road overlay) agree with this patch about who "the player" is,
    /// 4. core's faction display-name seam names the player's empire from the faction component
    ///    (the old hardcoded PColony branch in core's TextureUtility).
    /// </summary>
    public class EmpireCPMod : Mod
    {
        public EmpireCPMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("regionsandsocieties.empirecp");
            harmony.PatchAll();

            WorldObjectAdapterRegistry.Register(new EmpireAdapter());

            RegionOwnershipHelpers.PlayerFactionOverride = EmpirePatches.GetPlayerFaction;

            TextureUtility.FactionDisplayNameOverride = faction =>
            {
                if (faction?.def == null || faction.def.defName != "PColony") return null;
                try
                {
                    string customName = FactionColonies.FindFC.FactionComp?.name;
                    if (!customName.NullOrEmpty() && customName != "Player Faction") return customName;
                }
                catch
                {
                    // Before the component exists, fall through to the fixed label.
                }
                return "Player's Empire";
            };

            Log.Message("[RegionsAndSocieties.EmpireCP] Registered the Empire Refactored adapter (priority 100) and applied typed patches.");
        }
    }
}
