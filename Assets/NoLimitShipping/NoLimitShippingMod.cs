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

    [ModSettingIdentifier("capacity_multiplier")]
    [ModSettingTitle("容量倍率")]
    [ModSettingDescription("ステーション保管容量に掛ける倍率。デフォルト: 10000")]
    [ModSettingRange(1, 100000)]
    [ModSettingRequiresRestart]
    public static ModSetting<int> capacityMultiplier = new ModSetting<int>(10000);

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

        int multiplier = NoLimitShippingSettings.capacityMultiplier;
        var seen = new HashSet<ItemCategoryTemplate>();
        ItemCategoryTemplate fallback = null;

        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            var cat = kvp.Value.itemCategory;
            if (cat != null && seen.Add(cat))
            {
                cat.stationCapacityPerLevel *= multiplier;
                if (fallback == null) fallback = cat;
            }
        }

        UnityEngine.Debug.Log($"[NoLimitShipping] BoostStationCapacity: {seen.Count} categories x{multiplier}, fallback={fallback?.name ?? "NULL"}");

        if (fallback == null) return;

        int assigned = 0;
        var nocat = new StringBuilder();
        foreach (var kvp in ItemTemplateManager.getAllItemTemplates())
        {
            ItemTemplate item = kvp.Value;
            if (!item.isHiddenItem && item.itemCategory == null)
            {
                item.itemCategory = fallback;
                fallback.list_itemTemplates.Add(item);
                assigned++;
                if (nocat.Length < 600) nocat.Append(item.identifier).Append(' ');
            }
        }
        UnityEngine.Debug.Log($"[NoLimitShipping] BoostStationCapacity: assigned fallback to {assigned} items: {nocat}");
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
