using System;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// The pure cost model for Empire's laborer hiring (issue #2), deliberately kept out of the
    /// Harmony patch so the scaling curve and its wording are testable without a running game — the
    /// same reasoning that pulled core's resource display out into its own type. The patch layer
    /// (<see cref="HiringCostPatches"/>) resolves the region's population through core's public
    /// population endpoint and calls in here; nothing in this file touches game state.
    ///
    /// <para>The curve is monotonic and bounded at both ends: an empty region pays exactly
    /// <see cref="MaxFactor"/>, a crowded one approaches <see cref="MinFactor"/>, and cost falls
    /// smoothly with population in between (halfway at <see cref="PivotPopulation"/>). Labour is
    /// cheaper where people already are and dearer where they are not.</para>
    /// </summary>
    public static class RegionalHiringCost
    {
        /// <summary>Cheapest multiplier — the busiest regions approach but never fall below this.</summary>
        public const float MinFactor = 0.60f;

        /// <summary>Dearest multiplier — an empty region (population 0) pays exactly this.</summary>
        public const float MaxFactor = 1.50f;

        /// <summary>Population at which the factor sits halfway between the two bounds.</summary>
        public const float PivotPopulation = 12f;

        /// <summary>
        /// The cost multiplier for a region of the given population. Saturating and monotonic:
        /// population 0 → <see cref="MaxFactor"/>, growing population → <see cref="MinFactor"/>,
        /// <see cref="PivotPopulation"/> → the midpoint. Always within [MinFactor, MaxFactor].
        /// </summary>
        public static float Factor(int regionalPopulation)
        {
            int pop = regionalPopulation > 0 ? regionalPopulation : 0;
            float k = pop / (pop + PivotPopulation);              // 0..1, 0.5 exactly at the pivot
            float factor = MaxFactor - (MaxFactor - MinFactor) * k;
            if (factor < MinFactor) return MinFactor;
            if (factor > MaxFactor) return MaxFactor;
            return factor;
        }

        /// <summary>
        /// The base cost scaled for the region. A positive base never scales below 1; a non-positive
        /// base is returned untouched so a free or malformed quote stays free.
        /// </summary>
        public static int Scale(int baseCost, int regionalPopulation)
        {
            if (baseCost <= 0) return baseCost;
            int scaled = (int)Math.Round(baseCost * (double)Factor(regionalPopulation), MidpointRounding.AwayFromZero);
            return scaled < 1 ? 1 : scaled;
        }

        /// <summary>
        /// The one-word direction of the current adjustment, so the description reads as an
        /// explanation rather than a bare number. The dead-band around 1.0 keeps a region that lands
        /// on the pivot from claiming to be either cheaper or dearer.
        /// </summary>
        public static string Direction(int regionalPopulation)
        {
            float f = Factor(regionalPopulation);
            if (f < 0.995f) return "cheaper";
            if (f > 1.005f) return "dearer";
            return "typical";
        }

        /// <summary>
        /// The player-facing explanation of a quoted price: names the region, states its population,
        /// and shows the base cost, the multiplier, and the result. Built here rather than in the
        /// patch so the wording is under test and the patch stays pure wiring.
        /// </summary>
        public static string Describe(string regionName, int regionalPopulation, int baseCost, int scaledCost)
        {
            string where = string.IsNullOrEmpty(regionName) ? "this region" : regionName;
            float f = Factor(regionalPopulation);
            return "Regional labour: " + where + " (population " + regionalPopulation + ").\n"
                 + "Hiring is " + Direction(regionalPopulation) + " where people are plentiful — base "
                 + baseCost + " × " + f.ToString("0.00") + " = " + scaledCost + " silver.";
        }
    }
}
