using System;
using System.Linq;
using System.Reflection;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using RegionsAndSocieties;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Wires Empire's laborer hiring cost to the region it is hired into (issue #2). Empire quotes a
    /// flat price from settlement count and settings; here that price is scaled by the population of
    /// the region receiving the labour, read through core's public population endpoint
    /// (<see cref="PopulationDensityUtility"/> / <see cref="SynapseRegionManager"/>). All arithmetic
    /// and wording live in the pure <see cref="RegionalHiringCost"/>; this file only resolves the
    /// region and moves the number.
    ///
    /// <para>The single scaled seam is <c>LaborerHireUtil.CalculateCost(int,int)</c> — Empire's only
    /// caller of it is the quote (<c>CurrentCost</c>, shown on the button and used for the affordability
    /// check) and the charge (inside <c>HireLaborers</c>), so patching it once keeps the quoted and
    /// the paid price identical by construction. Identified against Empire Refactored's real assembly;
    /// a rename becomes a compile error on the <c>typeof</c> and a missing method logs at patch time
    /// via <see cref="EmpirePatches.PrepareGuard"/> instead of silently never binding.</para>
    /// </summary>
    public static class HiringCostPatches
    {
        /// <summary>The region context recorded by the most recent cost calculation, for the tooltip.</summary>
        internal struct RegionCostContext
        {
            public bool valid;
            public string regionName;
            public int population;
            public int baseCost;
            public int scaledCost;
        }

        internal static RegionCostContext LastContext;

        private static bool warned;

        /// <summary>Log the first failure to resolve or scale, then stay silent — one line, not a spew.</summary>
        internal static void WarnOnce(Exception ex)
        {
            if (warned) return;
            warned = true;
            Log.Warning($"[RegionsAndSocieties.EmpireCP] Regional hiring cost could not resolve a region; "
                      + $"leaving Empire's flat price unscaled. This is logged once. {ex}");
        }

        /// <summary>
        /// Scale a freshly-computed Empire laborer cost by the region it is hired into, and record the
        /// context so the button tooltip can explain it. On any failure the price is left exactly as
        /// Empire computed it, and the recorded context is invalidated so no stale tooltip shows.
        /// </summary>
        internal static void ScaleAndRecord(ref int result)
        {
            int baseCost = result;
            try
            {
                if (!TryResolveRegion(out string regionName, out int population))
                {
                    LastContext = default;
                    return;
                }

                int scaled = RegionalHiringCost.Scale(baseCost, population);
                result = scaled;
                LastContext = new RegionCostContext
                {
                    valid = true,
                    regionName = regionName,
                    population = population,
                    baseCost = baseCost,
                    scaledCost = scaled,
                };
            }
            catch (Exception ex)
            {
                LastContext = default;
                WarnOnce(ex);
            }
        }

        /// <summary>
        /// The region the player is hiring into: the tax settlement's tile, falling back to the first
        /// colony. Population is the region's aggregate dwelling count from core's public endpoint —
        /// the honest "how many people are here", not the smeared heatmap influence. Falls back to the
        /// per-tile heatmap read only if the tile has no resolved province.
        /// </summary>
        private static bool TryResolveRegion(out string regionName, out int population)
        {
            regionName = null;
            population = 0;

            World world = Find.World;
            if (world == null) return false;

            PlanetTile tile = default;
            bool haveTile = false;

            Map taxMap = FindFC.TaxMap;
            if (taxMap != null)
            {
                tile = taxMap.Tile;
                haveTile = tile.Valid;
            }
            if (!haveTile)
            {
                WorldSettlementFC first = FindFC.Settlements?.FirstOrDefault();
                if (first != null)
                {
                    tile = ((WorldObject)first).Tile;
                    haveTile = tile.Valid;
                }
            }
            if (!haveTile) return false;

            int tileId = tile.tileId;
            SynapseRegionManager mgr = world.GetComponent<SynapseRegionManager>();
            GeographicProvince province = mgr?.GetProvinceForTile(tileId);
            if (province != null)
            {
                regionName = province.name;
                population = province.currentPopulation;
            }
            else
            {
                population = PopulationDensityUtility.GetPopulationAtTile(tileId);
            }
            return true;
        }

        /// <summary>
        /// Add an always-on tooltip to the hire-laborers button explaining the regional basis of the
        /// price. Only the enabled button is handled here — Empire already tips its own reason on the
        /// disabled one. The button is identified without string-guessing: its label is exactly the
        /// <c>FCHireLaborers</c> translation at the current scaled quote, which we rebuild from the
        /// recorded context (drawn on the same frame, so it matches in any language).
        /// </summary>
        internal static void MaybeTipHireButton(Rect rect, string label, bool active)
        {
            try
            {
                if (!active || string.IsNullOrEmpty(label)) return;
                RegionCostContext ctx = LastContext;
                if (!ctx.valid) return;

                string expected = TranslatorFormattedStringExtensions.Translate("FCHireLaborers", (NamedArgument)ctx.scaledCost);
                if (label != expected) return;

                TooltipHandler.TipRegion(rect, new TipSignal(
                    RegionalHiringCost.Describe(ctx.regionName, ctx.population, ctx.baseCost, ctx.scaledCost)));
            }
            catch (Exception ex)
            {
                WarnOnce(ex);
            }
        }
    }

    /// <summary>
    /// The one scaled seam: both the quoted and the paid laborer price route through
    /// <c>CalculateCost</c>, so scaling it here keeps them identical.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_LaborerHireUtil_CalculateCost
    {
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_LaborerHireUtil_CalculateCost"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LaborerHireUtil), "CalculateCost", new[] { typeof(int), typeof(int) });
        }

        [HarmonyPostfix]
        public static void Postfix(ref int __result)
        {
            HiringCostPatches.ScaleAndRecord(ref __result);
        }
    }

    /// <summary>
    /// Request a fresh heatmap the moment a hire is committed, so the price charged inside
    /// <c>HireLaborers</c> (and the quote redrawn immediately after) reflects the latest population
    /// rather than a stale cached aggregate.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_LaborerHireUtil_HireLaborers
    {
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_LaborerHireUtil_HireLaborers"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LaborerHireUtil), "HireLaborers");
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            try { PopulationDensityUtility.MarkCacheDirty(); }
            catch (Exception ex) { HiringCostPatches.WarnOnce(ex); }
        }
    }

    /// <summary>
    /// Empire's own button helper carries the rect, so the hire-button tooltip attaches here without
    /// patching the large, type-unchecked settlement window that draws the button.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_UIUtil_ClampedButtonText
    {
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_UIUtil_ClampedButtonText"); }

        public static MethodBase TargetMethod()
        {
            // Match by name — Empire declares a single ClampedButtonText, and its last parameter is a
            // TextAnchor? that lives in a Unity module this project does not reference.
            return AccessTools.Method(typeof(UIUtil), "ClampedButtonText");
        }

        [HarmonyPostfix]
        public static void Postfix(Rect rect, string label, bool active)
        {
            HiringCostPatches.MaybeTipHireButton(rect, label, active);
        }
    }
}
