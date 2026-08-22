using System;
using System.Reflection;
using HarmonyLib;
using RegionsAndSocieties;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Surfaces a region's Empire resource supply (issue #3) on core's geographic-provinces region
    /// tooltip, rather than as a separate map mode — the tooltip-only integration. When Empire is
    /// installed and the region actually supplies something, the aggregate (computed and bounded by
    /// <see cref="EmpireRegionResources"/>) is appended to the region inspect text; when Empire is
    /// absent the block is simply never added, so the reader sees the unmodified core tooltip rather
    /// than an empty Empire heading.
    ///
    /// <para>The target — <c>MapMode_GeographicProvinces.GetProvinceTooltip(GeographicProvince,int)</c>
    /// — is a core type this assembly references, so a rename is a compile error; a missing method
    /// logs at patch time via <see cref="EmpirePatches.PrepareGuard"/> instead of silently vanishing.
    /// All aggregation stays in <see cref="EmpireRegionResources"/>; this patch only appends its
    /// finished text.</para>
    /// </summary>
    [HarmonyPatch]
    public static class Patch_MapMode_GeographicProvinces_GetProvinceTooltip
    {
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_MapMode_GeographicProvinces_GetProvinceTooltip"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MapMode_GeographicProvinces), "GetProvinceTooltip",
                new[] { typeof(GeographicProvince), typeof(int) });
        }

        [HarmonyPostfix]
        public static void Postfix(GeographicProvince province, ref string __result)
        {
            try
            {
                if (!EmpireRegionResources.Available || province == null) return;

                string supply = EmpireRegionResources.TooltipFor(province);
                if (string.IsNullOrEmpty(supply)) return;

                __result = string.IsNullOrEmpty(__result) ? supply : __result + "\n\n" + supply;
            }
            catch (Exception ex)
            {
                HiringOverlayLog.WarnOnce(ex);
            }
        }
    }

    /// <summary>One-shot warning for the overlay patch, so a resolution fault logs once, not per hover.</summary>
    internal static class HiringOverlayLog
    {
        private static bool warned;

        internal static void WarnOnce(Exception ex)
        {
            if (warned) return;
            warned = true;
            Log.Warning($"[RegionsAndSocieties.EmpireCP] Region resource supply could not be appended to the "
                      + $"province tooltip; showing the core tooltip unchanged. This is logged once. {ex}");
        }
    }
}
