using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RegionsAndSocieties.EmpireCP
{
    public static class EmpirePatches
    {
        /// <summary>
        /// Prepare() helper for the typed patch classes: a target Empire removed or renamed skips
        /// that one patch with a loud warning instead of sinking the whole PatchAll batch. The
        /// silent-never-bound failure mode (Factions#48) stays impossible either way — this logs.
        /// </summary>
        public static bool PrepareGuard(MethodBase target, string label)
        {
            if (target != null) return true;
            Log.Warning($"[RegionsAndSocieties.EmpireCP] {label}: target method not found on this Empire version; that patch is skipped.");
            return false;
        }

        public static string GetDefNameSafe(object obj)
        {
            if (obj == null) return null;
            
            // Try field first (most likely for Def.defName)
            var field = obj.GetType().GetField("defName", BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(obj) as string;
            }
            
            // Try property
            var prop = obj.GetType().GetProperty("defName", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                return prop.GetValue(obj) as string;
            }
            
            return null;
        }

        // 0.7: CalculateProductionBase_Postfix, CalculateProductionMult_Postfix and
        // SendMilitary_Prefix moved to RimSynapse.Factions.Patches.Factions_EmpirePatch when
        // production, taxation, military reach and standing moved to the Factions mod. They called
        // ProductionScalingUtility and MilitaryReachUtility, so keeping them here would have made
        // this mod depend on the one that already hard-depends on it. GetDefNameSafe and
        // GetPlayerFaction are public because that patch calls them rather than keeping a second
        // copy — two copies of a reflection accessor is how the two drift apart.
        //
        // GetResourceScale lived here until 0.7 and is now Factions' ProductionEvaluator
        // .AbundanceFactor, with its constants in ProductionRules, unchanged to the digit.


        private static T GetFieldValue<T>(object obj, string name, T defaultValue = default)
        {
            if (obj == null) return defaultValue;
            var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is T typedVal) return typedVal;
            }
            return defaultValue;
        }

        private static System.Collections.IEnumerable GetFieldEnumerable(object obj, string name)
        {
            if (obj == null) return null;
            var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(obj) as System.Collections.IEnumerable;
            }
            return null;
        }

        public static bool BuildParams_Prefix(object __instance, double valueBase, ref ThingSetMaker thingSetMaker, ref ThingSetMakerParams __result)
        {
            try
            {
                if (__instance == null) return true;

                Type thingSetMakerClass = GetFieldValue<Type>(__instance, "thingSetMakerClass");
                if (thingSetMakerClass != null)
                {
                    thingSetMaker = (ThingSetMaker)Activator.CreateInstance(thingSetMakerClass);
                }
                else
                {
                    Type fallbackType = GenTypes.GetTypeInAnyAssembly("FactionColonies.ThingSetMaker_MarketValue");
                    if (fallbackType != null)
                    {
                        thingSetMaker = (ThingSetMaker)Activator.CreateInstance(fallbackType);
                    }
                    else
                    {
                        thingSetMaker = new ThingSetMaker_MarketValue();
                    }
                }

                ThingSetMakerParams param = new ThingSetMakerParams();

                bool useValueFactors = GetFieldValue<bool>(__instance, "useValueFactors");
                double valueMinFactor = GetFieldValue<double>(__instance, "valueMinFactor");
                double valueMaxFactor = GetFieldValue<double>(__instance, "valueMaxFactor");
                double valueMinFlatOffset = GetFieldValue<double>(__instance, "valueMinFlatOffset");
                double valueMaxFlatOffset = GetFieldValue<double>(__instance, "valueMaxFlatOffset");

                if (useValueFactors)
                {
                    param.totalMarketValueRange = new FloatRange(
                        (float)(valueBase * valueMinFactor),
                        (float)(valueBase * valueMaxFactor));
                }
                else
                {
                    param.totalMarketValueRange = new FloatRange(
                        (float)(valueBase + valueMinFlatOffset),
                        (float)(valueBase + valueMaxFlatOffset));
                }

                bool overrideTechLevel = GetFieldValue<bool>(__instance, "overrideTechLevel");
                if (overrideTechLevel)
                {
                    param.techLevel = GetFieldValue<TechLevel>(__instance, "techLevel");
                }
                else
                {
                    TechLevel resolvedTech = TechLevel.Industrial;
                    Type findFcType = GenTypes.GetTypeInAnyAssembly("FactionColonies.FindFC");
                    if (findFcType != null)
                    {
                        var factionProp = findFcType.GetProperty("EmpireFaction", BindingFlags.Public | BindingFlags.Static);
                        var empireFaction = factionProp?.GetValue(null) as Faction;
                        if (empireFaction?.def != null)
                        {
                            resolvedTech = empireFaction.def.techLevel;
                        }
                        else if (Faction.OfPlayer?.def != null)
                        {
                            resolvedTech = Faction.OfPlayer.def.techLevel;
                        }
                    }
                    param.techLevel = resolvedTech;
                }

                param.filter = new ThingFilter();

                var thingCategoryAllowList = GetFieldEnumerable(__instance, "thingCategoryAllowList");
                if (thingCategoryAllowList != null)
                {
                    foreach (object item in thingCategoryAllowList)
                    {
                        if (item is ThingCategoryDef cat) param.filter.SetAllow(cat, true);
                    }
                }

                var thingAllowList = GetFieldEnumerable(__instance, "thingAllowList");
                if (thingAllowList != null)
                {
                    foreach (object item in thingAllowList)
                    {
                        if (item is ThingDef thing) param.filter.SetAllow(thing, true);
                    }
                }

                var stuffCategoryAllowList = GetFieldEnumerable(__instance, "stuffCategoryAllowList");
                if (stuffCategoryAllowList != null)
                {
                    foreach (object item in stuffCategoryAllowList)
                    {
                        if (item is StuffCategoryDef stuff) param.filter.SetAllow(stuff, true);
                    }
                }

                var thingBlockList = GetFieldEnumerable(__instance, "thingBlockList");
                if (thingBlockList != null)
                {
                    foreach (object item in thingBlockList)
                    {
                        if (item is ThingDef thing) param.filter.SetAllow(thing, false);
                    }
                }

                var stuffCategoryBlockList = GetFieldEnumerable(__instance, "stuffCategoryBlockList");
                if (stuffCategoryBlockList != null)
                {
                    foreach (object item in stuffCategoryBlockList)
                    {
                        if (item is StuffCategoryDef stuff) param.filter.SetAllow(stuff, false);
                    }
                }

                var modExtensions = GetFieldEnumerable(__instance, "modExtensions");
                if (modExtensions != null)
                {
                    foreach (object ext in modExtensions)
                    {
                        if (ext == null) continue;
                        var setFilterMethod = ext.GetType().GetMethod("SetFilter", BindingFlags.Public | BindingFlags.Instance);
                        if (setFilterMethod != null)
                        {
                            setFilterMethod.Invoke(ext, new object[] { param.filter, param.techLevel ?? TechLevel.Undefined });
                        }
                    }
                }

                bool useCountRange = GetFieldValue<bool>(__instance, "useCountRange");
                if (useCountRange)
                {
                    param.countRange = GetFieldValue<IntRange>(__instance, "countRange");
                }

                bool useQualityGenerator = GetFieldValue<bool>(__instance, "useQualityGenerator");
                if (useQualityGenerator)
                {
                    param.qualityGenerator = GetFieldValue<QualityGenerator>(__instance, "qualityGenerator");
                }

                __result = param;
                return false; // Skip the original method
            }
            catch (Exception ex)
            {
                Log.Error($"[RegionsAndSocieties] Error in BuildParams_Prefix: {ex}");
                return true; // Fallback to original if something failed
            }
        }

        public static bool GenerateRewardThings_Prefix(double valueBase, object rewardDef, ref System.Collections.Generic.List<Thing> __result)
        {
            try
            {
                if (rewardDef == null)
                {
                    Log.Error("[Empire][ERR] GenerateRewardThings called with null rewardDef");
                    __result = new System.Collections.Generic.List<Thing>();
                    return false;
                }

                var buildParamsMethod = rewardDef.GetType().GetMethod("BuildParams", BindingFlags.Public | BindingFlags.Instance);
                if (buildParamsMethod == null)
                {
                    Log.Error($"[RegionsAndSocieties] Could not find BuildParams method on rewardDef");
                    __result = new System.Collections.Generic.List<Thing>();
                    return false;
                }

                object[] args = new object[] { valueBase, null };
                ThingSetMakerParams param = (ThingSetMakerParams)buildParamsMethod.Invoke(rewardDef, args);
                ThingSetMaker thingSetMaker = args[1] as ThingSetMaker;

                if (thingSetMaker == null)
                {
                    Log.Error($"[Empire][ERR] GenerateRewardThings: thingSetMaker is null for {GetDefNameSafe(rewardDef)}");
                    __result = new System.Collections.Generic.List<Thing>();
                    return false;
                }

                System.Collections.Generic.List<Thing> things = null;
                for (int attempts = 0; attempts < 100; attempts++)
                {
                    try
                    {
                        things = thingSetMaker.Generate(param);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Empire][ERR] Exception in thingSetMaker.Generate for {GetDefNameSafe(rewardDef)}: {ex}");
                        things = null;
                    }

                    if (things != null && ReturnValueOfTitheSafe(things) >= (param.totalMarketValueRange?.min ?? 0f))
                    {
                        __result = things;
                        return false;
                    }
                }

                Log.Warning($"[Empire][WARN] GenerateRewardThings failed to meet minimum value after 100 attempts for {GetDefNameSafe(rewardDef)}. Returning last result.");
                __result = things ?? new System.Collections.Generic.List<Thing>();
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[RegionsAndSocieties] Error in GenerateRewardThings_Prefix: {ex}");
                return true; // Fallback to original
            }
        }

        public static bool ReturnValueOfTithe_Prefix(System.Collections.Generic.List<Thing> things, ref double __result)
        {
            __result = ReturnValueOfTitheSafe(things);
            return false;
        }

        private static double ReturnValueOfTitheSafe(System.Collections.Generic.List<Thing> things)
        {
            if (things == null) return 0;
            double totalValue = 0;
            foreach (Thing thing in things)
            {
                if (thing == null) continue;
                totalValue += thing.stackCount * thing.MarketValue;
            }
            return totalValue;
        }

        public static bool IsCity(WorldObject obj)
        {
            if (obj == null) return false;
            
            if (obj is Settlement)
            {
                string typeName = obj.GetType().FullName;
                if (typeName.IndexOf("outpost", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                return true;
            }
            
            Type type = obj.GetType();
            while (type != null)
            {
                if (type.FullName == "Outposts.Outpost")
                    return false;
                type = type.BaseType;
            }
            
            if (obj.GetType().FullName == "FactionColonies.WorldSettlementFC")
            {
                var defProp = obj.GetType().GetProperty("def", BindingFlags.Public | BindingFlags.Instance);
                if (defProp != null)
                {
                    var defVal = defProp.GetValue(obj);
                    if (defVal != null)
                    {
                        return IsCityDef(defVal);
                    }
                }
            }
            
            string objTypeName = obj.GetType().FullName;
            if (objTypeName.IndexOf("outpost", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            
            return false;
        }

        public static bool IsCityDef(object settlementdef)
        {
            if (settlementdef == null) return false;
            string defName = GetDefNameSafe(settlementdef);
            if (defName != null && defName.IndexOf("outpost", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            
            var labelField = settlementdef.GetType().GetField("label", BindingFlags.Public | BindingFlags.Instance);
            if (labelField != null)
            {
                string label = labelField.GetValue(settlementdef) as string;
                if (label != null && label.IndexOf("outpost", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
            
            return true;
        }

        /// <summary>
        /// Region ownership now resolves through core's helper, which classifies through the
        /// adapter registry — this patch's own adapter is what makes WorldSettlementFC read as a
        /// settlement there, so the answer matches the old IsCity-based walk.
        /// </summary>
        public static Faction GetRegionOwner(int tileId)
        {
            return RegionsAndSocieties.Patches.RegionOwnershipHelpers.GetRegionOwner(tileId);
        }

        /// <summary>
        /// Typed now — and typing found a live bug: the old reflection asked FactionFC for a
        /// <c>faction</c> property that has never existed, so it silently fell back to
        /// <see cref="Faction.OfPlayer"/> on every call. The real accessor is
        /// <c>FindFC.EmpireFaction</c> (the player-colony faction, cached by Empire itself).
        /// Registered into core's PlayerFactionOverride seam by the mod entry, so core's own
        /// patches (road overlay) resolve the same faction this file does.
        /// </summary>
        public static Faction GetPlayerFaction()
        {
            try
            {
                var empireFaction = FactionColonies.FindFC.EmpireFaction;
                if (empireFaction != null) return empireFaction;
            }
            catch
            {
                // Before the world exists, fall through.
            }
            return Faction.OfPlayer;
        }

    }

    [HarmonyPatch]
    public static class Patch_WorldTileChecker_IsValidTileForNewSettlement
    {
        // Skip cleanly (with a loud log) if Empire renamed the method, instead of sinking PatchAll.
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_WorldTileChecker_IsValidTileForNewSettlement"); }

        public static MethodBase TargetMethod()
        {
            // Typed: a rename in Empire is now a compile error on the typeof, and the method lookup
            // failing logs at patch time instead of silently never binding.
            return AccessTools.Method(typeof(FactionColonies.util.WorldTileChecker), "IsValidTileForNewSettlement",
                new[] { typeof(PlanetTile), typeof(FactionColonies.WorldSettlementDef), typeof(StringBuilder) });
        }

        [HarmonyPrefix]
        public static bool Prefix(PlanetTile tile, object settlementdef, ref bool __result, StringBuilder reason)
        {
            if (!tile.Valid)
            {
                reason?.Append("FCSelectedInvalidTile".Translate());
                __result = false;
                return false;
            }

            Faction placingFaction = EmpirePatches.GetPlayerFaction();
            if (placingFaction != null)
            {
                Faction owner = EmpirePatches.GetRegionOwner(tile.tileId);
                if (owner != null && owner != placingFaction)
                {
                    reason?.Append("This region is owned by another faction.");
                    __result = false;
                    return false;
                }
            }

            if (settlementdef != null)
            {
                var allowsTileLayerMethod = settlementdef.GetType().GetMethod("AllowsTileLayer", BindingFlags.Public | BindingFlags.Instance);
                if (allowsTileLayerMethod != null)
                {
                    bool allowed = (bool)allowsTileLayerMethod.Invoke(settlementdef, new object[] { tile });
                    if (!allowed)
                    {
                        reason?.Append("FCInvalidPlanetLayer".Translate());
                        __result = false;
                        return false;
                    }
                }
            }

            if (Find.WorldObjects.AnyWorldObjectAt(tile.tileId))
            {
                reason?.Append("Tile is already occupied.");
                __result = false;
                return false;
            }

            __result = true;
            return false; // Skip original complex constraints (allowedBiomes, FCFactionBaseAdjacent, etc.)
        }
    }

    [HarmonyPatch]
    public static class Patch_DebugUtil_CreateTenRandomSettlements
    {
        // Skip cleanly (with a loud log) if Empire renamed the method, instead of sinking PatchAll.
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_DebugUtil_CreateTenRandomSettlements"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionColonies.DebugUtil), "CreateTenRandomSettlements");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            try
            {
                var factionCompType = GenTypes.GetTypeInAnyAssembly("FactionColonies.FactionFC");
                var findFcType = GenTypes.GetTypeInAnyAssembly("FactionColonies.FindFC");
                if (factionCompType == null || findFcType == null) return true;

                var factionCompProp = findFcType.GetProperty("FactionComp", BindingFlags.Public | BindingFlags.Static);
                var empireFactionProp = findFcType.GetProperty("EmpireFaction", BindingFlags.Public | BindingFlags.Static);
                if (factionCompProp == null || empireFactionProp == null) return true;

                var factionComp = factionCompProp.GetValue(null);
                var empireFaction = (Faction)empireFactionProp.GetValue(null);
                if (factionComp == null || empireFaction == null)
                {
                    Log.Error("Debug - FactionFC WorldComponent or EmpireFaction is null, cannot create settlements.");
                    return false;
                }

                var worldSettlementDefType = GenTypes.GetTypeInAnyAssembly("FactionColonies.WorldSettlementDef");
                var def = GenDefDatabase.GetDef(worldSettlementDefType, "WorldSettlementDef_Surface");
                int created = 0;
                int maxAttempts = 500;

                for (int attempts = 0; attempts < maxAttempts && created < 10; attempts++)
                {
                    int tileId = FactionPlacementUtility.FindBestTileForFaction(empireFaction);
                    if (tileId == -1) continue;

                    // Call ColonyUtil.CreatePlayerColonySettlement(new PlanetTile(tileId), def)
                    var colonyUtilType = GenTypes.GetTypeInAnyAssembly("FactionColonies.ColonyUtil");
                    var createMethod = colonyUtilType?.GetMethod("CreatePlayerColonySettlement", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(PlanetTile), worldSettlementDefType, typeof(string) }, null);
                    if (createMethod != null)
                    {
                        createMethod.Invoke(null, new object[] { new PlanetTile(tileId), def, null });
                        created++;
                    }
                }

                Log.Warning($"Debug - Created {created}/10 random settlements using region-based world generation solver.");
            }
            catch (Exception ex)
            {
                Log.Error("Error in CreateTenRandomSettlements patch prefix: " + ex);
            }
            return false; // Skip original
        }
    }

    [HarmonyPatch]
    public static class Patch_DebugUtil_CreateSettlementPerResource
    {
        // Skip cleanly (with a loud log) if Empire renamed the method, instead of sinking PatchAll.
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_DebugUtil_CreateSettlementPerResource"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionColonies.DebugUtil), "CreateSettlementPerResource");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            try
            {
                var findFcType = GenTypes.GetTypeInAnyAssembly("FactionColonies.FindFC");
                if (findFcType == null) return true;

                var factionCompProp = findFcType.GetProperty("FactionComp", BindingFlags.Public | BindingFlags.Static);
                var empireFactionProp = findFcType.GetProperty("EmpireFaction", BindingFlags.Public | BindingFlags.Static);
                if (factionCompProp == null || empireFactionProp == null) return true;

                var factionComp = factionCompProp.GetValue(null);
                var empireFaction = (Faction)empireFactionProp.GetValue(null);
                if (factionComp == null || empireFaction == null)
                {
                    Log.Error("Debug - FactionFC WorldComponent or EmpireFaction is null, cannot create settlements.");
                    return false;
                }

                var worldSettlementDefType = GenTypes.GetTypeInAnyAssembly("FactionColonies.WorldSettlementDef");
                var def = GenDefDatabase.GetDef(worldSettlementDefType, "WorldSettlementDef_Surface");

                var resourceTypeDef = GenTypes.GetTypeInAnyAssembly("FactionColonies.ResourceTypeDef");
                var allResourceTypeDefsObj = typeof(DefDatabase<>).MakeGenericType(resourceTypeDef)
                    .GetProperty("AllDefsListForReading", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);
                var allResourceDefs = (System.Collections.IEnumerable)allResourceTypeDefsObj;

                StringBuilder summary = new StringBuilder();
                int created = 0, specialtyCount = 0, skipped = 0;

                var colonyUtilType = GenTypes.GetTypeInAnyAssembly("FactionColonies.ColonyUtil");
                var createMethod = colonyUtilType?.GetMethod("CreatePlayerColonySettlement", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(PlanetTile), worldSettlementDefType, typeof(string) }, null);
                if (createMethod == null) return true;

                var biomeResourceDefType = GenTypes.GetTypeInAnyAssembly("FactionColonies.BiomeResourceDef");
                var defaultBiomeDef = GenDefDatabase.GetDef(biomeResourceDefType, "defaultBiome", false);

                foreach (Def rtd in allResourceDefs)
                {
                    // Find a tile in the region-based world generation manner
                    int chosenTileId = FactionPlacementUtility.FindBestTileForFaction(empireFaction);
                    if (chosenTileId == -1)
                    {
                        summary.AppendLine($"  {rtd.defName}: SKIPPED (no tile)");
                        skipped++;
                        continue;
                    }

                    PlanetTile chosen = new PlanetTile(chosenTileId);
                    var sObj = createMethod.Invoke(null, new object[] { chosen, def, null });
                    if (sObj == null) continue;

                    created++;
                    bool usedSpecialty = false;

                    var biome = Find.WorldGrid[chosen.tileId].PrimaryBiome;
                    var bres = GenDefDatabase.GetDef(biomeResourceDefType, biome.defName, false) ?? defaultBiomeDef;
                    if (bres != null)
                    {
                        var getBiomeResourceMethod = bres.GetType().GetMethod("GetBiomeResource", BindingFlags.Public | BindingFlags.Instance);
                        if (getBiomeResourceMethod != null)
                        {
                            var avail = getBiomeResourceMethod.Invoke(bres, new object[] { rtd });
                            if (avail != null)
                            {
                                var additiveField = avail.GetType().GetField("additive", BindingFlags.Public | BindingFlags.Instance);
                                if (additiveField != null)
                                {
                                    float additive = (float)additiveField.GetValue(avail);
                                    if (additive > 1f) usedSpecialty = true;
                                }
                            }
                        }
                    }
                    if (usedSpecialty) specialtyCount++;

                    // Upgrade to 5
                    var upgradeMethod = sObj.GetType().GetMethod("UpgradeSettlement", BindingFlags.Public | BindingFlags.Instance);
                    var levelField = sObj.GetType().GetField("settlementLevel", BindingFlags.Public | BindingFlags.Instance);
                    if (upgradeMethod != null && levelField != null)
                    {
                        int level = (int)levelField.GetValue(sObj);
                        upgradeMethod.Invoke(sObj, new object[] { 5 - level });
                    }

                    // Assign workers
                    var getResourceMethod = sObj.GetType().GetMethod("GetResource", BindingFlags.Public | BindingFlags.Instance);
                    var resourcesProp = sObj.GetType().GetProperty("Resources", BindingFlags.Public | BindingFlags.Instance);
                    var increaseWorkersMethod = sObj.GetType().GetMethod("IncreaseWorkers", BindingFlags.Public | BindingFlags.Instance);
                    var workersUltraMaxProp = sObj.GetType().GetProperty("workersUltraMax", BindingFlags.Public | BindingFlags.Instance);

                    if (getResourceMethod != null && resourcesProp != null && increaseWorkersMethod != null && workersUltraMaxProp != null)
                    {
                        var targetResource = getResourceMethod.Invoke(sObj, new object[] { rtd });
                        int cap = (int)workersUltraMaxProp.GetValue(sObj);
                        string biomeNote = usedSpecialty ? "specialty biome" : "base biome";

                        if (targetResource == null)
                        {
                            summary.AppendLine($"  {rtd.defName}: {sObj.GetType().GetProperty("Name").GetValue(sObj)} L5 ({biomeNote}), no workers (resource absent)");
                        }
                        else
                        {
                            var allResources = (System.Collections.IEnumerable)resourcesProp.GetValue(sObj);
                            foreach (var r in allResources)
                            {
                                increaseWorkersMethod.Invoke(sObj, new object[] { r, -(cap + 1) });
                            }
                            increaseWorkersMethod.Invoke(sObj, new object[] { targetResource, cap });

                            var assignedProp = targetResource.GetType().GetProperty("assignedWorkers", BindingFlags.Public | BindingFlags.Instance);
                            int assignedVal = assignedProp != null ? (int)assignedProp.GetValue(targetResource) : 0;
                            summary.AppendLine($"  {rtd.defName}: {sObj.GetType().GetProperty("Name").GetValue(sObj)} L5 ({biomeNote}), {assignedVal} workers");
                        }
                    }
                }

                Log.Warning($"Debug - Create Settlement Per Resource: created {created} settlement(s) using region-based world generation solver.\n{summary}");
            }
            catch (Exception ex)
            {
                Log.Error("Error in CreateSettlementPerResource patch prefix: " + ex);
            }
            return false; // Skip original
        }
    }

    // --- Economy governance targets, applied by PatchAll -----------------------------------------
    // These were harmony.Patch'd manually from core's mod constructor via GenTypes string lookups
    // (the old TryPatchEmpires). Typed targets now: a renamed Empire type is a compile error, and a
    // renamed method logs at patch time instead of silently never binding.

    [HarmonyPatch]
    public static class Patch_ResourceEventRewardDef_BuildParams
    {
        // Skip cleanly (with a loud log) if Empire renamed the method, instead of sinking PatchAll.
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_ResourceEventRewardDef_BuildParams"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionColonies.ResourceEventRewardDef), "BuildParams");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, double valueBase, ref ThingSetMaker thingSetMaker, ref ThingSetMakerParams __result)
        {
            return EmpirePatches.BuildParams_Prefix(__instance, valueBase, ref thingSetMaker, ref __result);
        }
    }

    [HarmonyPatch]
    public static class Patch_PaymentUtil_GenerateRewardThings
    {
        // Skip cleanly (with a loud log) if Empire renamed the method, instead of sinking PatchAll.
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_PaymentUtil_GenerateRewardThings"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionColonies.PaymentUtil), "GenerateRewardThings");
        }

        [HarmonyPrefix]
        public static bool Prefix(double valueBase, object rewardDef, ref System.Collections.Generic.List<Thing> __result)
        {
            return EmpirePatches.GenerateRewardThings_Prefix(valueBase, rewardDef, ref __result);
        }
    }

    [HarmonyPatch]
    public static class Patch_PaymentUtil_ReturnValueOfTithe
    {
        // Skip cleanly (with a loud log) if Empire renamed the method, instead of sinking PatchAll.
        public static bool Prepare() { return EmpirePatches.PrepareGuard(TargetMethod(), "Patch_PaymentUtil_ReturnValueOfTithe"); }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionColonies.PaymentUtil), "ReturnValueOfTithe");
        }

        [HarmonyPrefix]
        public static bool Prefix(System.Collections.Generic.List<Thing> things, ref double __result)
        {
            return EmpirePatches.ReturnValueOfTithe_Prefix(things, ref __result);
        }
    }
}
