using FactionColonies;
using RimWorld.Planet;
using RegionsAndSocieties.Integration;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    /// <summary>
    /// Typed adapter for Empire Refactored (packageId <c>Matathias.Empire</c>, namespace
    /// <c>FactionColonies</c>). Replaces core's string-declared reflection profile; this assembly
    /// hard-references <c>Empire.dll</c>, so the member names below are compiler-verified — the
    /// exact defence against R&T #30, where an invented member name made every Empire settlement
    /// read population zero for a whole release without an error.
    ///
    /// <para>Facts carried from the verified profile (read against Empire Refactored 1.6.20):
    /// population is <c>workers</c>/<c>workersMax</c> (doubles — narrowed here), level is
    /// <c>settlementLevel</c> on <see cref="WorldSettlementFC"/> itself, and the real level ceiling
    /// is per-def and player-configurable, so 10 stays the assumed ceiling (the clamp Empire's own
    /// tests use).</para>
    ///
    /// <para>One refinement over the old profile: a <see cref="WorldSettlementFC"/> whose def reads
    /// as an outpost (defName/label containing "outpost") classifies as an Outpost rather than a
    /// Settlement — preserving the old IsCityDef distinction, which now lives behind the adapter
    /// instead of in core's ownership walk.</para>
    /// </summary>
    public class EmpireAdapter : WorldObjectAdapterBase
    {
        public override string AdapterId { get { return "empire"; } }

        public override string DisplayName { get { return "Empire Refactored"; } }

        /// <summary>Same slot as core's old reflection profile: first of the mod adapters.</summary>
        public override int Priority { get { return 100; } }

        private static readonly bool Active = ModsConfig.IsActive("Matathias.Empire");

        public override bool IsActive { get { return Active; } }

        /// <summary>Empire runs the player's own colonies as WorldSettlementFC objects.</summary>
        public override bool PlayerOwnedByDefault { get { return true; } }

        public override bool TryClassify(WorldObject obj, out WorldObjectKind kind)
        {
            kind = WorldObjectKind.Unknown;
            if (obj == null) return false;

            if (obj is WorldSettlementFC settlement)
            {
                kind = IsOutpostLikeDef(settlement.def) ? WorldObjectKind.Outpost : WorldObjectKind.Settlement;
                return true;
            }

            // The old profile's supplementary rules, preserved: any further WorldSettlement* type
            // is a settlement; anything named like MilitaryFC is a military installation.
            var type = obj.GetType();
            if (type.Namespace == "FactionColonies")
            {
                if (type.Name.StartsWith("WorldSettlement")) { kind = WorldObjectKind.Settlement; return true; }
                if (type.Name.Contains("MilitaryFC")) { kind = WorldObjectKind.Military; return true; }
            }

            return false;
        }

        public override bool TryGetPopulation(WorldObject obj, out int population)
        {
            population = 0;
            var settlement = obj as WorldSettlementFC;
            if (settlement == null) return false;

            population = (int)settlement.workers;
            return population >= 0;
        }

        public override bool TryGetLevel(WorldObject obj, out int level, out int maxLevel)
        {
            level = 0;
            maxLevel = 0;
            var settlement = obj as WorldSettlementFC;
            if (settlement == null) return false;

            level = settlement.settlementLevel;
            maxLevel = 10;
            return true;
        }

        private static bool IsOutpostLikeDef(Def def)
        {
            if (def == null) return false;
            if (def.defName != null && def.defName.IndexOf("outpost", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (def.label != null && def.label.IndexOf("outpost", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
