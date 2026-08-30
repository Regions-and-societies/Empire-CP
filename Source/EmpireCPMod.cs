using System;
using System.Runtime.CompilerServices;
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
    ///
    /// About.xml declares no hard dependency on a core edition: the two editions
    /// (<c>RegionsAndSocieties.Core</c> on Map Mode Framework, <c>RegionsAndSocieties.CoreRP2</c> on
    /// Realistic Planets 2) are mutually exclusive, and modDependencies cannot express "either of" —
    /// declaring one falsely flags the other edition's users. So core presence is checked here
    /// instead, and a missing core degrades to a warning rather than a type-load error.
    /// </summary>
    public class EmpireCPMod : Mod
    {
        public EmpireCPMod(ModContentPack content) : base(content)
        {
            if (!CoreLoaded())
            {
                Log.Warning("[RegionsAndSocieties.EmpireCP] Regions and Societies is not loaded — check your mod list to ensure the Regions and Societies (Realistic Planets 2) or the standard Map Mode Framework edition is active. The Empire Refactored integration was not applied.");
                return;
            }

            Initialize();
        }

        // Both editions ship the same "RegionsAndSocieties" assembly with an identical public API,
        // and RimWorld loads every active mod's assemblies before constructing any Mod class — so
        // scanning the domain detects whichever edition is present, regardless of load order or of
        // packageId suffixes that ModsConfig.IsActive would miss on local copies.
        private static bool CoreLoaded()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "RegionsAndSocieties") return true;
            }

            return false;
        }

        // NoInlining keeps the RegionsAndSocieties type references out of the constructor's JIT
        // scope, so a missing core reaches the warning above instead of a TypeLoadException.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Initialize()
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
