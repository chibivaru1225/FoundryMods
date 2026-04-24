using C3.ModKit;
using HarmonyLib;
using System.Collections.Generic;
using System.Text;

// ---------------------------------------------------------------------------
// Settings — discovered automatically by ModSettingsManager via reflection.
// Changes to restart-required settings take effect on next game load.
// ---------------------------------------------------------------------------
[ModSettingGroup]
[ModSettingIdentifier("nolimitshipping")]
[ModSettingTitle("NoLimitShipping")]
public static class NoLimitShippingSettings
{
    [ModSettingIdentifier("make_all_tradeable")]
    [ModSettingTitle("全アイテムを取引可能にする")]
    [ModSettingDescription("全アイテムをシッピングパッドで宇宙ステーションに送れるようにします。")]
    [ModSettingRequiresRestart]
    public static ModSetting<bool> makeAllItemsTradeable = new ModSetting<bool>(true);

    [ModSettingIdentifier("boost_capacity")]
    [ModSettingTitle("保管容量をブースト")]
    [ModSettingDescription("全カテゴリのステーション保管容量に倍率を掛けます。")]
    [ModSettingRequiresRestart]
    public static ModSetting<bool> boostCapacity = new ModSetting<bool>(true);

    [ModSettingIdentifier("multiplier_raw_materials")]
    [ModSettingTitle("容量倍率：生素材 (Raw Materials)")]
    [ModSettingDescription("鉱石などの生素材カテゴリの保管容量倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> multiplierRawMaterials = new ModSetting<int>(10000);

    [ModSettingIdentifier("multiplier_resources")]
    [ModSettingTitle("容量倍率：資源 (Resources)")]
    [ModSettingDescription("加工素材などの資源カテゴリの保管容量倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> multiplierResources = new ModSetting<int>(10000);

    [ModSettingIdentifier("multiplier_components")]
    [ModSettingTitle("容量倍率：部品 (Components)")]
    [ModSettingDescription("部品カテゴリの保管容量倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> multiplierComponents = new ModSetting<int>(10000);

    [ModSettingIdentifier("multiplier_construction_material")]
    [ModSettingTitle("容量倍率：建設資材 (Construction Material)")]
    [ModSettingDescription("建設資材カテゴリの保管容量倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> multiplierConstructionMaterial = new ModSetting<int>(10000);

    [ModSettingIdentifier("multiplier_sales_items")]
    [ModSettingTitle("容量倍率：販売品 (Sales Items)")]
    [ModSettingDescription("販売品カテゴリの保管容量倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> multiplierSalesItems = new ModSetting<int>(10000);

    [ModSettingIdentifier("multiplier_mod_added")]
    [ModSettingTitle("容量倍率：Mod追加アイテム")]
    [ModSettingDescription("このModで追加した輸送枠（機械・ロジスティクス等）の保管容量倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> multiplierModAdded = new ModSetting<int>(10000);

    [ModSettingIdentifier("show_in_shipping_pad")]
    [ModSettingTitle("シッピングパッドUIに全アイテム表示")]
    [ModSettingDescription("シッピングパッドの設定UIに全アイテムを表示します。")]
    public static ModSetting<bool> showAllInShippingPad = new ModSetting<bool>(true);

    [ModSettingIdentifier("show_in_station_storage")]
    [ModSettingTitle("ステーション保管庫に全アイテム表示")]
    [ModSettingDescription("ステーション保管庫のUIに全アイテムを表示します。")]
    public static ModSetting<bool> showAllInStationStorage = new ModSetting<bool>(true);
}

// ---------------------------------------------------------------------------
// Mod logic
// ---------------------------------------------------------------------------
public static class NoLimitShippingMod
{
    internal static void SetAllItemsTradeable()
    {
        if (!NoLimitShippingSettings.makeAllItemsTradeable) return;

        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            ItemTemplate item = kvp.Value;
            if (item.isHiddenItem) continue;

            item.canBeTradedOnStation = true;

            if (item.market_sellPrice <= 0) item.market_sellPrice = 1;
            if (item.market_buyPrice  <= 0) item.market_buyPrice  = 1;

            if (item._baseWeightInGrams < 1000)
                item._baseWeightInGrams = 1000;
        }
    }

    internal static void BoostStationCapacity()
    {
        if (!NoLimitShippingSettings.boostCapacity) return;

        // Build a per-category multiplier map from itemCategoryIdentifier.
        // One item per category is enough to learn the identifier → category mapping.
        var catMultiplierMap = new System.Collections.Generic.Dictionary<ItemCategoryTemplate, int>();
        ItemCategoryTemplate fallback = null;

        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            var it = kvp.Value;
            if (it.itemCategory == null || catMultiplierMap.ContainsKey(it.itemCategory)) continue;

            int mult = it.itemCategoryIdentifier switch
            {
                "_base_raw_materials"          => NoLimitShippingSettings.multiplierRawMaterials,
                "_base_resources"              => NoLimitShippingSettings.multiplierResources,
                "_base_components"             => NoLimitShippingSettings.multiplierComponents,
                "_base_construction_material"  => NoLimitShippingSettings.multiplierConstructionMaterial,
                "_base_sales_items"            => NoLimitShippingSettings.multiplierSalesItems,
                _                              => 1,  // 未知カテゴリは変更なし
            };
            catMultiplierMap[it.itemCategory] = mult;
            if (fallback == null) fallback = it.itemCategory;
        }

        var logSb = new StringBuilder("[NoLimitShipping] BoostStationCapacity:");
        foreach (var kv in catMultiplierMap)
        {
            kv.Key.stationCapacityPerLevel *= kv.Value;
            logSb.Append($" {kv.Key.name}x{kv.Value}");
        }
        UnityEngine.Debug.Log(logSb.ToString());
        UnityEngine.Debug.Log($"[NoLimitShipping] fallback={fallback?.name ?? "NULL"}");

        if (fallback == null) return;

        // Find building materials category.
        ItemCategoryTemplate buildingMatCat = null;
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            var it = kvp.Value;
            if (it.itemCategoryIdentifier == "_base_construction_material" && it.itemCategory != null)
            {
                buildingMatCat = it.itemCategory;
                break;
            }
        }
        UnityEngine.Debug.Log($"[NoLimitShipping] buildingMatCat={buildingMatCat?.name ?? "NOT FOUND"}");

        // Find raw materials category (for terrain blocks: dirt, sand, stone, mud).
        ItemCategoryTemplate rawMaterialsCat = null;
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            var it = kvp.Value;
            if (it.itemCategoryIdentifier == "_base_raw_materials" && it.itemCategory != null)
            {
                rawMaterialsCat = it.itemCategory;
                break;
            }
        }
        UnityEngine.Debug.Log($"[NoLimitShipping] rawMaterialsCat={rawMaterialsCat?.name ?? "NOT FOUND"}");

        // Find resources category (for science packs).
        ItemCategoryTemplate resourcesCat = null;
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            var it = kvp.Value;
            if (it.creativeModeCategory_str == "_base_cmct_resources" && it.itemCategory != null)
            {
                resourcesCat = it.itemCategory;
                break;
            }
        }
        UnityEngine.Debug.Log($"[NoLimitShipping] resourcesCat={resourcesCat?.name ?? "NOT FOUND"}");

        // Dedicated category for remaining mod-added items.
        // item.itemCategory → modCat (capacity), fallback.list_itemTemplates (UI grouping).
        int modMultiplier = NoLimitShippingSettings.multiplierModAdded;
        var modCat = UnityEngine.ScriptableObject.CreateInstance<ItemCategoryTemplate>();
        modCat.name = "NoLimitShipping";
        modCat.stationCapacityPerLevel = 100 * modMultiplier;

        int assignedTerrain = 0, assignedBlocks = 0, assignedScience = 0, assignedMod = 0;
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            ItemTemplate item = kvp.Value;
            if (item.isHiddenItem || item.itemCategory != null) continue;

            if (item.creativeModeCategory_str == "_base_cmct_blocks")
            {
                // Terrain blocks (dirt/sand/stone/mud variants) → raw materials
                string id = item.identifier ?? "";
                bool isTerrain = id.Contains("dirt") || id.Contains("sand") ||
                                 id.Contains("stone") || id.Contains("mud");
                if (isTerrain && resourcesCat != null)
                {
                    item.itemCategory = resourcesCat;
                    resourcesCat.list_itemTemplates.Add(item);
                    assignedTerrain++;
                }
                else if (buildingMatCat != null)
                {
                    // Other building blocks → building materials
                    item.itemCategory = buildingMatCat;
                    buildingMatCat.list_itemTemplates.Add(item);
                    assignedBlocks++;
                }
                else
                {
                    item.itemCategory = modCat;
                    fallback.list_itemTemplates.Add(item);
                    assignedMod++;
                }
            }
            else if (rawMaterialsCat != null && item.creativeModeCategory_str == "_base_cmct_science")
            {
                // Science Packs → raw materials category
                item.itemCategory = rawMaterialsCat;
                rawMaterialsCat.list_itemTemplates.Add(item);
                assignedScience++;
            }
            else
            {
                // Machines, logistics, etc. → NoLimitShipping category (capacity),
                // fallback list (UI grouping)
                item.itemCategory = modCat;
                fallback.list_itemTemplates.Add(item);
                assignedMod++;
            }
        }
        UnityEngine.Debug.Log($"[NoLimitShipping] Assigned {assignedTerrain} terrain→{resourcesCat?.name}, {assignedBlocks} blocks→{buildingMatCat?.name}, {assignedScience} science→{rawMaterialsCat?.name}, {assignedMod} others→NoLimitShipping(x{modMultiplier})");
    }
}

// ---------------------------------------------------------------------------
// Harmony patches
// ---------------------------------------------------------------------------

// Called just before native layer reads item template data.
[HarmonyPatch(typeof(NativeWrapper), "registerNativeLibData")]
public static class Patch_NativeWrapper_RegisterNativeLibData
{
    static bool _ran = false;
    static void Prefix()
    {
        if (_ran) return;
        _ran = true;
        UnityEngine.Debug.Log("[NoLimitShipping] registerNativeLibData Prefix fired");
        try { NoLimitShippingMod.SetAllItemsTradeable(); }
        catch (System.Exception e) { UnityEngine.Debug.LogError("[NoLimitShipping] SetAllItemsTradeable failed: " + e); }
        try { NoLimitShippingMod.BoostStationCapacity(); }
        catch (System.Exception e) { UnityEngine.Debug.LogError("[NoLimitShipping] BoostStationCapacity failed: " + e); }
    }
}

// ShippingPadConfigFrame.Awake() builds its slot dictionary once and caches it.
[HarmonyPatch(typeof(ShippingPadConfigFrame), "Awake")]
public static class Patch_ShippingPadConfigFrame_Awake
{
    static void Prefix()
    {
        NoLimitShippingMod.SetAllItemsTradeable();
    }
}

// Bypass the research-unlock visibility gate for the shipping pad.
[HarmonyPatch(typeof(ResearchSystem), "isItemVisibleOnShippingPad")]
public static class Patch_ResearchSystem_IsItemVisibleOnShippingPad
{
    static bool Prefix(ulong itemTemplateId, ref bool __result)
    {
        if (!NoLimitShippingSettings.showAllInShippingPad) return true;
        ItemTemplate template = ItemTemplateManager.getItemTemplate(itemTemplateId);
        if (template != null && template.canBeTradedOnStation)
        {
            __result = true;
            return false;
        }
        return true;
    }
}

// Bypass the research-unlock visibility gate for the sky platform storage view.
[HarmonyPatch(typeof(ResearchSystem), "isItemVisibleOnSkyPlatform")]
public static class Patch_ResearchSystem_IsItemVisibleOnSkyPlatform
{
    static bool Prefix(ulong itemTemplateId, ref bool __result)
    {
        if (!NoLimitShippingSettings.showAllInStationStorage) return true;
        ItemTemplate template = ItemTemplateManager.getItemTemplate(itemTemplateId);
        if (template != null && template.canBeTradedOnStation)
        {
            __result = true;
            return false;
        }
        return true;
    }
}
