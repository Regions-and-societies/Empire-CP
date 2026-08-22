using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactionColonies;
using RegionsAndSocieties;
using RimWorld.Planet;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// The game-facing half of the region resource aggregation (issue #3): it reads Empire's real
    /// per-settlement resource model — <c>WorldSettlementFC.Resources</c> (a <c>List&lt;ResourceFC&gt;</c>),
    /// each with a <c>def</c> and a <c>rawTotalProduction</c>, identified against Empire's assembly —
    /// and folds it per region through the pure <see cref="EmpireResourceAggregator"/>. It stays out
    /// of the map-mode file so the overlay only has to ask, never to compute.
    ///
    /// <para><see cref="Available"/> is false when Empire is not installed, so the overlay can be left
    /// unoffered rather than offered and empty.</para>
    /// </summary>
    public static class EmpireRegionResources
    {
        private static readonly bool active = ModsConfig.IsActive("Matathias.Empire");

        /// <summary>Empire present — the overlay is only offered when this is true (issue #3).</summary>
        public static bool Available { get { return active; } }

        /// <summary>
        /// Total production per resource type across the Empire settlements whose tile lies in the
        /// province. By construction this equals the sum of each settlement's per-resource
        /// <c>rawTotalProduction</c> — the acceptance identity for #3.
        /// </summary>
        public static Dictionary<ResourceTypeDef, double> ForRegion(GeographicProvince province)
        {
            var contributions = ContributionsForRegion(province);
            return EmpireResourceAggregator.Aggregate(contributions);
        }

        /// <summary>
        /// The raw (settlement, resource) contributions before summation — exposed so the debug action
        /// and the Tier-2 test can check the region aggregate against its parts.
        /// </summary>
        public static List<KeyValuePair<ResourceTypeDef, double>> ContributionsForRegion(GeographicProvince province)
        {
            var contributions = new List<KeyValuePair<ResourceTypeDef, double>>();
            if (!active || province == null) return contributions;

            List<WorldSettlementFC> settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0) return contributions;

            var tiles = new HashSet<int>(province.tiles);
            foreach (WorldSettlementFC settlement in settlements)
            {
                if (settlement == null) continue;
                if (!tiles.Contains(((WorldObject)settlement).Tile.tileId)) continue;

                List<ResourceFC> resources = settlement.Resources;
                if (resources == null) continue;
                foreach (ResourceFC resource in resources)
                {
                    if (resource?.def == null) continue;
                    contributions.Add(new KeyValuePair<ResourceTypeDef, double>(resource.def, resource.rawTotalProduction));
                }
            }
            return contributions;
        }

        /// <summary>
        /// A bounded, human tooltip of what the region supplies: the largest producers first, with a
        /// "+N more" tail so a resource-rich region stays a few lines instead of an unscrolled wall
        /// (#3). Returns null when the region supplies nothing, so the caller can omit the line
        /// entirely rather than print an empty heading.
        /// </summary>
        public static string TooltipFor(GeographicProvince province, int maxLines = 8)
        {
            Dictionary<ResourceTypeDef, double> totals = ForRegion(province);
            if (totals.Count == 0) return null;

            List<KeyValuePair<ResourceTypeDef, double>> ordered = totals
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => ResourceLabel(kv.Key))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Regional supply:");
            int shown = Math.Min(Math.Max(1, maxLines), ordered.Count);
            for (int i = 0; i < shown; i++)
            {
                KeyValuePair<ResourceTypeDef, double> entry = ordered[i];
                sb.AppendLine("  " + ResourceLabel(entry.Key) + ": " + entry.Value.ToString("0.#") + "/day");
            }
            if (ordered.Count > shown)
            {
                sb.AppendLine("  +" + (ordered.Count - shown) + " more");
            }
            return sb.ToString().TrimEnd();
        }

        private static string ResourceLabel(ResourceTypeDef def)
        {
            if (def == null) return "?";
            return string.IsNullOrEmpty(def.label) ? def.defName : def.label;
        }
    }
}
