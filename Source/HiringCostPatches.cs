using System;
using System.Linq;
using System.Reflection;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using RegionsAndSocieties.Integration;
using RegionsAndSocieties.Sizing;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Ties Empire's laborer hiring cost to the tier of the settlement it is hired into (issue #2).
    /// Empire quotes a flat price from settlement count and settings; here that price is adjusted by
    /// the destination settlement's Village/Town/City/Major-City tier, classified through core's
    /// <see cref="SettlementSizeEvaluator"/> reading Empire's own level via the adapter registry. All
    /// arithmetic and wording live in the pure <see cref="HiringCostModel"/>; this file only resolves
    /// the settlement and moves the number.
    ///
    /// <para>The single adjusted seam is <c>LaborerHireUtil.CalculateCost(int,int)</c> — Empire's only
    /// caller of it is the quote (<c>CurrentCost</c>, shown on the button and used for the affordability
    /// check) and the charge (inside <c>HireLaborers</c>), so patching it once keeps the quoted and
    /// the paid price identical by construction. Identified against Empire Refactored's real assembly;
    /// a rename becomes a compile error on the <c>typeof</c> and a missing method logs at patch time
    /// via <see cref="EmpirePatches.PrepareGuard"/> instead of silently never binding.</para>
    /// </summary>
    public static class HiringCostPatches
    {
        /// <summary>The tier context recorded by the most recent cost calculation, for the tooltip.</summary>
        internal struct HireCostContext
        {
            public bool valid;
            public string settlementName;
            public SettlementTier tier;
            public int baseCost;
            public int scaledCost;
        }

        internal static HireCostContext LastContext;

        private static bool warned;

        /// <summary>Log the first failure to resolve or scale, then stay silent — one line, not a spew.</summary>
        internal static void WarnOnce(Exception ex)
        {
            if (warned) return;
            warned = true;
            Log.Warning($"[RegionsAndSocieties.EmpireCP] Regional hiring cost could not resolve a settlement tier; "
                      + $"leaving Empire's flat price unadjusted. This is logged once. {ex}");
        }

        /// <summary>
        /// Adjust a freshly-computed Empire laborer cost by the tier of the settlement it is hired
        /// into, and record the context so the button tooltip can explain it. On any failure the price
        /// is left exactly as Empire computed it, and the recorded context is invalidated so no stale
        /// tooltip shows.
        /// </summary>
        internal static void ScaleAndRecord(ref int result)
        {
            int baseCost = result;
            try
            {
                if (!TryResolveHiringSettlement(out WorldSettlementFC settlement))
                {
                    LastContext = default;
                    return;
                }

                SettlementTier tier = TierOf(settlement);
                int scaled = HiringCostModel.Scale(baseCost, tier);
                result = scaled;
                LastContext = new HireCostContext
                {
                    valid = true,
                    settlementName = settlement.Name,
                    tier = tier,
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
        /// The settlement the player is hiring into: the one on the tax map (where the labour is
        /// delivered), falling back to the first colony.
        /// </summary>
        private static bool TryResolveHiringSettlement(out WorldSettlementFC settlement)
        {
            settlement = null;
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0) return false;

            Map taxMap = FindFC.TaxMap;
            if (taxMap != null)
            {
                settlement = settlements.FirstOrDefault(s => s != null && ((WorldObject)s).Tile.tileId == taxMap.Tile.tileId);
            }
            if (settlement == null)
            {
                settlement = settlements.FirstOrDefault(s => s != null);
            }
            return settlement != null;
        }

        /// <summary>
        /// The settlement's rung, from Empire's own upgrade level read through the adapter registry
        /// (so this mod's own adapter is what answers) and split across the five rungs by
        /// <see cref="HiringCostModel.TierForLevel"/> — the full +25%…−25% range, two Empire levels
        /// per rung at the default ceiling.
        /// </summary>
        internal static SettlementTier TierOf(WorldSettlementFC settlement)
        {
            if (settlement == null) return SettlementTier.None;
            if (!WorldObjectAdapterRegistry.TryGetLevel(settlement, out int level, out int maxLevel))
            {
                return SettlementTier.None;
            }
            return HiringCostModel.TierForLevel(level, maxLevel);
        }

        /// <summary>
        /// Add an always-on tooltip to the hire-laborers button explaining the tier basis of the
        /// price. Only the enabled button is handled here — Empire already tips its own reason on the
        /// disabled one. The button is identified without string-guessing: its label is exactly the
        /// <c>FCHireLaborers</c> translation at the current adjusted quote, which we rebuild from the
        /// recorded context (drawn on the same frame, so it matches in any language).
        /// </summary>
        internal static void MaybeTipHireButton(Rect rect, string label, bool active)
        {
            try
            {
                if (!active || string.IsNullOrEmpty(label)) return;
                HireCostContext ctx = LastContext;
                if (!ctx.valid) return;

                string expected = TranslatorFormattedStringExtensions.Translate("FCHireLaborers", (NamedArgument)ctx.scaledCost);
                if (label != expected) return;

                TooltipHandler.TipRegion(rect, new TipSignal(
                    HiringCostModel.Describe(ctx.settlementName, ctx.tier, ctx.baseCost, ctx.scaledCost)));
            }
            catch (Exception ex)
            {
                WarnOnce(ex);
            }
        }
    }

    /// <summary>
    /// The one adjusted seam: both the quoted and the paid laborer price route through
    /// <c>CalculateCost</c>, so adjusting it here keeps them identical.
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
