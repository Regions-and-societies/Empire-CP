using System;
using RegionsAndSocieties.Sizing;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// The pure cost model for Empire's laborer hiring (issue #2), kept out of the Harmony patch so
    /// the ladder is testable without a running game. The price is adjusted by the <b>tier</b> of the
    /// settlement the labour is hired into — core's Village / Town / City / Major City classification,
    /// itself population-derived — because a developed settlement has a labour pool a frontier village
    /// does not.
    ///
    /// <para>The steps are small but noticeable: +25% / +10% / 0 / −10% / −25%, centred on City.
    /// Core caps an ordinary settlement at Major City (Metropolis is the special capital tier), so an
    /// Empire colony spans the four lower rungs in practice; the Metropolis rung is defined for
    /// completeness so every <see cref="SettlementTier"/> has an honest answer.</para>
    /// </summary>
    public static class HiringCostModel
    {
        /// <summary>
        /// The cost multiplier for a settlement tier. City is the neutral centre (×1.00); each step
        /// away is a flat 10–25%. Dearer in a small settlement, cheaper in a developed one. An
        /// unclassified tier (<see cref="SettlementTier.None"/>) applies no adjustment.
        /// </summary>
        public static float Factor(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.Village: return 1.25f;     // +25% — a frontier hamlet, labour is scarce
                case SettlementTier.Town: return 1.10f;        // +10%
                case SettlementTier.City: return 1.00f;        //   0% — the neutral centre
                case SettlementTier.MajorCity: return 0.90f;   // −10%
                case SettlementTier.Metropolis: return 0.75f;  // −25% — a deep labour pool
                default: return 1.00f;                         // None / unclassified — no adjustment
            }
        }

        /// <summary>
        /// The base cost adjusted for the tier. A positive base never scales below 1; a non-positive
        /// base is returned untouched so a free or malformed quote stays free.
        /// </summary>
        public static int Scale(int baseCost, SettlementTier tier)
        {
            if (baseCost <= 0) return baseCost;
            int scaled = (int)Math.Round(baseCost * (double)Factor(tier), MidpointRounding.AwayFromZero);
            return scaled < 1 ? 1 : scaled;
        }

        /// <summary>The signed percentage for the description, e.g. "+25%", "0%", "−10%".</summary>
        public static string PercentLabel(SettlementTier tier)
        {
            int pct = (int)Math.Round((Factor(tier) - 1f) * 100f);
            if (pct > 0) return "+" + pct + "%";
            if (pct < 0) return "−" + Math.Abs(pct) + "%";
            return "0%";
        }

        /// <summary>The human name of a tier, for the tooltip.</summary>
        public static string TierLabel(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.Village: return "Village";
                case SettlementTier.Town: return "Town";
                case SettlementTier.City: return "City";
                case SettlementTier.MajorCity: return "Major City";
                case SettlementTier.Metropolis: return "Metropolis";
                default: return "settlement";
            }
        }

        /// <summary>
        /// The player-facing explanation of a quoted price: names the settlement, its tier and the
        /// adjustment, then the base and the result. Built here rather than in the patch so the
        /// wording is under test and the patch stays pure wiring.
        /// </summary>
        public static string Describe(string settlementName, SettlementTier tier, int baseCost, int scaledCost)
        {
            string where = string.IsNullOrEmpty(settlementName) ? "your settlement" : settlementName;
            return "Labour rate: " + where + " is a " + TierLabel(tier) + " (" + PercentLabel(tier) + ").\n"
                 + "Bigger settlements hire cheaper — base " + baseCost + " → " + scaledCost + " silver.";
        }
    }
}
