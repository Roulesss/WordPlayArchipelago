using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace WordPlayArchipelago.Archipelago
{
    public static class ShopSaveManager
    {
        public static bool IsResumingShop { get; set; } = false;
        public static bool LoadedShopIsShowing { get; set; } = false;
        public static List<string> LoadedShopPerks { get; set; } = new List<string>();
        public static List<long> LoadedShopLocations { get; set; } = new List<long>();
        public static List<int> LoadedPlaceholderSlots { get; set; } = new List<int>();
        public static int LoadedRerollCost { get; set; } = 1;

        public static bool HasSavedPerks => LoadedShopPerks != null && LoadedShopPerks.Count > 0;

        public static void ClearShopState()
        {
            LoadedShopIsShowing = false;
            LoadedShopPerks.Clear();
            LoadedShopLocations.Clear();
            LoadedPlaceholderSlots.Clear();
            LoadedRerollCost = 1;
        }

        public static void ClearLoadedPerks()
        {
            LoadedShopPerks.Clear();
            LoadedShopLocations.Clear();
            LoadedPlaceholderSlots.Clear();
        }

        public static void RecordActiveShop()
        {
            if (ShopManager.Instance == null || !ShopManager.Instance.shopIsShowing)
            {
                ClearShopState();
                return;
            }

            LoadedShopIsShowing = true;
            LoadedShopPerks.Clear();
            if (BonusStore.Instance != null && BonusStore.Instance.nextThreeUpgrades != null)
            {
                foreach (var b in BonusStore.Instance.nextThreeUpgrades)
                {
                    if (b != null) LoadedShopPerks.Add(b.name);
                }
            }

            LoadedShopLocations.Clear();
            if (LocationManager.CurrentShopLocations != null)
            {
                foreach (var loc in LocationManager.CurrentShopLocations)
                {
                    LoadedShopLocations.Add(loc);
                }
            }

            LoadedPlaceholderSlots.Clear();
            foreach (var slot in UnlockManager.PlaceholderShopSlots)
            {
                LoadedPlaceholderSlots.Add(slot);
            }

            try
            {
                LoadedRerollCost = Traverse.Create(ShopManager.Instance).Field("rerollCost").GetValue<int>();
            }
            catch
            {
                LoadedRerollCost = 1;
            }
        }
    }
}
