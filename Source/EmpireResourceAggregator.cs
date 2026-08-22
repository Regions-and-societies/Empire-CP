using System.Collections.Generic;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Pure per-key summation for Empire's per-settlement resources (issue #3). It is kept out of the
    /// map overlay on purpose: a region's aggregate must be testable without the map framework, the
    /// same split that keeps core's resource display out of its type-unchecked map-mode file. Nothing
    /// here touches game state, so it runs under a Tier-1 sandbox test directly.
    /// </summary>
    public static class EmpireResourceAggregator
    {
        /// <summary>
        /// Sum contribution amounts by key. Null keys and non-positive amounts are dropped — a
        /// resource nobody produces is not something the region "supplies" — so an empty or all-zero
        /// input yields an empty result, and the region shows nothing rather than a column of zeroes.
        /// The generic key keeps the core testable with plain values while the game layer keys by the
        /// real Empire <c>ResourceTypeDef</c>.
        /// </summary>
        public static Dictionary<TKey, double> Aggregate<TKey>(IEnumerable<KeyValuePair<TKey, double>> contributions)
        {
            var totals = new Dictionary<TKey, double>();
            if (contributions == null) return totals;

            foreach (var contribution in contributions)
            {
                if (contribution.Key == null || contribution.Value <= 0) continue;
                totals.TryGetValue(contribution.Key, out double running);
                totals[contribution.Key] = running + contribution.Value;
            }

            return totals;
        }
    }
}
