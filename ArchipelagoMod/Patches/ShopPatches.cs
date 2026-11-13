using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Enums;
using DG.Tweening;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordPlayArchipelago.Archipelago;

namespace WordPlayArchipelago.Patches
{
    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.BuyCard))]
    public static class ShopCardBuyPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ShopManager __instance, int cardID)
        {
            var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
            if (client == null || !client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;
            if (LogicManagerScript.Instance == null) return;
            if (LogicManagerScript.Instance.endlessMode) return;

            // If player's modifier or upgrade inventory is full, notify them that the check is collected and the bonus discarded
            if (__instance.upgradeCards != null && cardID >= 0 && cardID < __instance.upgradeCards.Count)
            {
                var card = __instance.upgradeCards[cardID];
                if (card != null && card.associatedBonus != null && !UnlockManager.PlaceholderShopSlots.Contains(cardID))
                {
                    int ownedMods = BonusInventory.Instance != null ? BonusInventory.Instance.numberOfOwnedModifiers : 0;
                    int maxMods = BonusInventory.Instance != null ? BonusInventory.Instance.maximumModifiers : 6;
                    int ownedUpgrades = BonusInventory.Instance != null ? BonusInventory.Instance.numberOfOwnedUpgrades : 0;
                    int maxUpgrades = BonusInventory.Instance != null ? BonusInventory.Instance.maximumUpgrades : 3;

                    if (card.associatedBonus.BonusType == BaseBonus.Type.Modifier && ownedMods >= maxMods)
                    {
                        Plugin.Logger?.LogInfo($"[ShopCardBuyPatch] Modifiers full ({ownedMods}/{maxMods}): AP Check collected; modifier not added.");
                        Plugin.Instance?.UI?.ShowBanner("Modifiers full (6/6): AP check collected (modifier discarded).", Color.cyan, 3.5f);
                    }
                    else if (card.associatedBonus.BonusType == BaseBonus.Type.Upgrade && ownedUpgrades >= maxUpgrades)
                    {
                        Plugin.Logger?.LogInfo($"[ShopCardBuyPatch] Upgrades full ({ownedUpgrades}/{maxUpgrades}): AP Check collected; upgrade not added.");
                        Plugin.Instance?.UI?.ShowBanner("Letter upgrades full (3/3): AP check collected (upgrade discarded).", Color.cyan, 3.5f);
                    }
                }
            }

            int round = LogicManagerScript.Instance.currentRound;
            if (round >= client.TotalRoundsForDifficulty) return;
            long locationId = LocationManager.GetCurrentShopLocation(cardID);
            if (locationId <= 0)
            {
                locationId = LocationManager.GetShopPurchaseLocationId(round, cardID);
            }

            string locTitle = LocationManager.IsShopLocation(locationId) ? "Round Shop Choice" : (LocationManager.GetLocationName(locationId) ?? "Round Shop Choice");
            string desc = locTitle;
            if (client.ScoutedLocations.TryGetValue(locationId, out var scouted))
            {
                string recipient = scouted.Player.Slot == client.SlotNumber ? "You" : scouted.Player.Name;
                desc = $"{locTitle}: {scouted.ItemDisplayName} ({recipient})";
            }
            else
            {
                string? locName = LocationManager.GetLocationName(locationId);
                if (!string.IsNullOrEmpty(locName)) desc = locName!;
            }

            Plugin.Logger?.LogInfo($"Player purchased Round Shop Choice on Round {round}! Checking Location #{locationId} ({desc})...");
            client.CheckLocation(locationId, desc);
        }
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.CheckIfCanBuyCards))]
    public static class ShopCheckIfCanBuyPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ShopManager __instance)
        {
            if (__instance.upgradeCards == null) return true;

            int currentRound = LogicManagerScript.Instance != null ? LogicManagerScript.Instance.currentRound : 1;

            for (int i = 0; i < __instance.numberOfShopItems && i < __instance.upgradeCards.Count; i++)
            {
                var card = __instance.upgradeCards[i];
                if (card == null) continue;

                // Always keep cards enabled so players can always get the AP check even if inventory is full
                card.EnableCard(instant: true);

                // Dynamically update card visuals and warnings based on current modifier/upgrade inventory count
                ShopCardFormatter.FormatShopCard(card, i, currentRound);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(BonusStore), nameof(BonusStore.PickThreeRandomUpgrades))]
    public static class BonusStorePickThreeRandomUpgradesPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BonusStore __instance, bool reroll)
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return true;

            // If we have saved shop perks from a resume, restore them exactly instead of re-rolling!
            if (!reroll && ShopSaveManager.HasSavedPerks)
            {
                Plugin.Logger?.LogInfo("[BonusStore] Restoring saved shop perks from resume save.");
                __instance.nextThreeUpgrades.Clear();
                UnlockManager.PlaceholderShopSlots.Clear();

                if (__instance.everyBonus != null)
                {
                    var bonusLookup = new Dictionary<string, BaseBonus>(StringComparer.OrdinalIgnoreCase);
                    foreach (var b in __instance.everyBonus)
                    {
                        if (b != null && !bonusLookup.ContainsKey(b.name))
                        {
                            bonusLookup[b.name] = b;
                        }
                    }

                    foreach (var perkName in ShopSaveManager.LoadedShopPerks)
                    {
                        if (bonusLookup.TryGetValue(perkName, out var b))
                        {
                            __instance.nextThreeUpgrades.Add(b);
                        }
                    }
                }

                LocationManager.RestoreShopLocations(ShopSaveManager.LoadedShopLocations);

                foreach (var slot in ShopSaveManager.LoadedPlaceholderSlots)
                {
                    UnlockManager.PlaceholderShopSlots.Add(slot);
                }

                for (int i = 0; i < __instance.nextThreeUpgrades.Count; i++)
                {
                    var b = __instance.nextThreeUpgrades[i];
                    if (b != null && !UnlockManager.PlaceholderShopSlots.Contains(i))
                    {
                        try { b.OnAppearInShop(); } catch { }
                    }
                }

                ShopSaveManager.ClearLoadedPerks();
                ShopSaveManager.RecordActiveShop();
                return false;
            }

            // Handle reroll tracking
            if (reroll)
            {
                __instance.perksBeforeReroll.Clear();
                foreach (var b in __instance.nextThreeUpgrades)
                {
                    if (b != null) __instance.perksBeforeReroll.Add(b);
                }
            }
            else
            {
                __instance.perksBeforeReroll.Clear();
            }

            __instance.nextThreeUpgrades.Clear();
            UnlockManager.PlaceholderShopSlots.Clear();

            // Handle forceAddToShop if present
            if (__instance.forceAddToShop != null)
            {
                __instance.nextThreeUpgrades.Add(__instance.forceAddToShop);
                __instance.forceAddToShop = null;
            }

            int needed = ShopManager.Instance != null ? ShopManager.Instance.numberOfShopItems : 3;

            // Roll AP shop locations for this shop draft (rerolling or new round/run)
            LocationManager.RollShopLocations(reroll, needed);

            // 1. Gather all bonuses from everyBonus that are unlocked and not owned
            List<BaseBonus> allEligible = new List<BaseBonus>();
            if (__instance.everyBonus != null)
            {
                foreach (var b in __instance.everyBonus)
                {
                    if (b == null) continue;
                    if (!UnlockManager.IsBonusUnlocked(b)) continue;
                    if (__instance.nextThreeUpgrades.Contains(b)) continue;

                    if (BonusInventory.Instance != null)
                    {
                        if (BonusInventory.Instance.ownedModifiers != null && BonusInventory.Instance.ownedModifiers.Contains(b)) continue;
                        if (BonusInventory.Instance.ownedUpgrades != null && BonusInventory.Instance.ownedUpgrades.Contains(b)) continue;
                    }

                    allEligible.Add(b);
                }
            }

            // Partition into fresh (not shown before this reroll) and reroll pool
            List<BaseBonus> freshPool = new List<BaseBonus>();
            List<BaseBonus> rerollPool = new List<BaseBonus>();
            foreach (var b in allEligible)
            {
                if (__instance.perksBeforeReroll.Contains(b))
                {
                    rerollPool.Add(b);
                }
                else
                {
                    freshPool.Add(b);
                }
            }

            // 2. First pass: Pick from freshPool
            int attempts = 0;
            while (__instance.nextThreeUpgrades.Count < needed && freshPool.Count > 0 && attempts <= 100)
            {
                attempts++;
                int index = UnityEngine.Random.Range(0, freshPool.Count);
                BaseBonus candidate = freshPool[index];

                bool passedRarity = false;
                try
                {
                    passedRarity = Traverse.Create(__instance).Method("RolledAOne", candidate).GetValue<bool>();
                }
                catch
                {
                    passedRarity = true;
                }

                // Respect rarity roll unless pool is small
                if (passedRarity || attempts > 50)
                {
                    __instance.nextThreeUpgrades.Add(candidate);
                    freshPool.RemoveAt(index);
                }
            }

            // 3. Second pass: If not enough fresh bonuses, pick from rerollPool
            // (prevents turning all slots into placeholders when player has 3 or fewer unlocked modifiers)
            int rerollAttempts = 0;
            while (__instance.nextThreeUpgrades.Count < needed && rerollPool.Count > 0 && rerollAttempts <= 100)
            {
                rerollAttempts++;
                int index = UnityEngine.Random.Range(0, rerollPool.Count);
                BaseBonus candidate = rerollPool[index];

                bool passedRarity = false;
                try
                {
                    passedRarity = Traverse.Create(__instance).Method("RolledAOne", candidate).GetValue<bool>();
                }
                catch
                {
                    passedRarity = true;
                }

                if (passedRarity || rerollAttempts > 50)
                {
                    __instance.nextThreeUpgrades.Add(candidate);
                    rerollPool.RemoveAt(index);
                }
            }

            // 4. Third pass: If there are STILL not enough unlocked items in total, fill remaining slots with placeholders
            while (__instance.nextThreeUpgrades.Count < needed)
            {
                int slotIndex = __instance.nextThreeUpgrades.Count;
                UnlockManager.PlaceholderShopSlots.Add(slotIndex);

                BaseBonus? placeholder = null;
                if (__instance.everyBonus != null && __instance.everyBonus.Count > 0)
                {
                    placeholder = __instance.everyBonus[slotIndex % __instance.everyBonus.Count];
                }
                if (placeholder != null)
                {
                    __instance.nextThreeUpgrades.Add(placeholder);
                }
                else
                {
                    break;
                }
            }

            // 4. Trigger OnAppearInShop for real bonuses
            for (int i = 0; i < __instance.nextThreeUpgrades.Count; i++)
            {
                var b = __instance.nextThreeUpgrades[i];
                if (b != null && !UnlockManager.PlaceholderShopSlots.Contains(i))
                {
                    try
                    {
                        b.OnAppearInShop();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger?.LogWarning($"[BonusStore] OnAppearInShop error: {ex.Message}");
                    }
                }
            }

            Plugin.Logger?.LogInfo($"[BonusStore] Archipelago shop picked {__instance.nextThreeUpgrades.Count} items. Placeholders (Check Only): {UnlockManager.PlaceholderShopSlots.Count}");
            ShopSaveManager.RecordActiveShop();
            return false;
        }
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.ShowShop))]
    public static class ShopShowShopPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ShopManager __instance)
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;

            if (ShopSaveManager.LoadedRerollCost > 1)
            {
                Traverse.Create(__instance).Field("rerollCost").SetValue(ShopSaveManager.LoadedRerollCost);
                __instance.reRollButtonText.text = "-" + ShopSaveManager.LoadedRerollCost + " Plays";
            }

            ShopSaveManager.RecordActiveShop();
            try
            {
                SaveData.Instance?.Save();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[ShopShowShopPatch] Save failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.ReRollShop))]
    public static class ShopReRollShopPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;

            DOVirtual.DelayedCall(1.6f, () =>
            {
                ShopSaveManager.RecordActiveShop();
                try
                {
                    SaveData.Instance?.Save();
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"[ShopReRollShopPatch] Save failed: {ex.Message}");
                }
            });
        }
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.ShutShop))]
    public static class ShopShutShopPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;
            ShopSaveManager.ClearShopState();
        }
    }

    [HarmonyPatch(typeof(BonusInventory), nameof(BonusInventory.OnBonusPurchase))]
    public static class BonusInventoryOnBonusPurchasePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BaseBonus bonus, bool fromShop)
        {
            if (ArchipelagoClient.IsArchipelagoRun && fromShop)
            {
                if (bonus == null || !UnlockManager.IsBonusUnlocked(bonus))
                {
                    Plugin.Logger?.LogInfo($"[BonusInventory] Suppressing vanilla perk grant for '{bonus?.gameObject?.name}' because it is locked or a placeholder check.");
                    return false;
                }
            }
            return true;
        }
    }

    public static class ShopCardFormatter
    {
        public static void FormatShopCard(BonusCardScript card, int cardIdx, int round)
        {
            var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
            if (card == null || client == null || !client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;
            if (round >= client.TotalRoundsForDifficulty || (LogicManagerScript.Instance != null && LogicManagerScript.Instance.endlessMode)) return;

            long locationId = LocationManager.GetCurrentShopLocation(cardIdx);
            if (locationId <= 0)
            {
                locationId = LocationManager.GetShopPurchaseLocationId(round, cardIdx);
            }
            bool isScouted = client.ScoutedLocations.TryGetValue(locationId, out var scouted);
            bool isChecked = client.IsLocationChecked(locationId);

            // On-demand scout if not yet scouted
            if (!isScouted && client.IsConnected)
            {
                client.ScoutSpecificLocations(new[] { locationId }, () =>
                {
                    FormatShopCard(card, cardIdx, round);
                });
            }

            bool isPlaceholder = UnlockManager.PlaceholderShopSlots.Contains(cardIdx) ||
                                 card.associatedBonus == null ||
                                 !UnlockManager.IsBonusUnlocked(card.associatedBonus);

            if (isPlaceholder)
            {
                // Ensure card is active and interactable
                card.gameObject.SetActive(true);

                // 1. Header Text and Colors
                var headerField = Traverse.Create(card).Field("headerText").GetValue<TMP_Text>();
                if (headerField != null)
                {
                    headerField.text = isChecked ? "[CHECKED] AP Check" : "[AP] AP Check";
                }

                var mainHeader = Traverse.Create(card).Field("mainHeaderColour").GetValue<Image>();
                if (mainHeader != null)
                {
                    mainHeader.color = isChecked ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.15f, 0.45f, 0.75f);
                }
                var subHeader = Traverse.Create(card).Field("headerColour").GetValue<Image>();
                if (subHeader != null)
                {
                    subHeader.color = isChecked ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.12f, 0.40f, 0.70f);
                }

                // 2. Tile Icon & Backer
                var tileText = Traverse.Create(card).Field("shopCardTileText").GetValue<TMP_Text>();
                if (tileText != null)
                {
                    tileText.text = isChecked ? "✓" : "AP";
                    tileText.color = Color.white;
                }

                var tileBacker = Traverse.Create(card).Field("shopCardTileBackerImage").GetValue<Image>();
                if (tileBacker != null)
                {
                    tileBacker.color = isChecked ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.15f, 0.45f, 0.75f);
                }

                var tileScore = Traverse.Create(card).Field("shopCardTileScoreText").GetValue<TMP_Text>();
                if (tileScore != null)
                {
                    tileScore.text = "";
                }

                var tileImage = Traverse.Create(card).Field("shopCardTileImage").GetValue<Image>();
                if (tileImage != null)
                {
                    tileImage.enabled = false;
                }

                var scoreCorner = Traverse.Create(card).Field("scoreCorner").GetValue<GameObject>();
                if (scoreCorner != null) scoreCorner.SetActive(false);

                var multiplierOverlay = Traverse.Create(card).Field("multiplierOverlay").GetValue<GameObject>();
                if (multiplierOverlay != null) multiplierOverlay.SetActive(false);

                var legendaryOverlay = Traverse.Create(card).Field("legendaryOverlay").GetValue<GameObject>();
                if (legendaryOverlay != null) legendaryOverlay.SetActive(false);

                // 3. Description
                var descField = Traverse.Create(card).Field("myDescription").GetValue<TMP_Text>();
                if (descField != null)
                {
                    descField.margin = Vector4.zero;
                    if (isChecked)
                    {
                        string checkedName = isScouted && scouted != null ? scouted.ItemDisplayName : "Archipelago Item";
                        string recipient = isScouted && scouted != null ? (scouted.Player.Slot == client.SlotNumber ? "You" : scouted.Player.Name) : "";
                        descField.text = $"<size=120%><color=#888888><b>[CHECKED]</b></color></size>\n\n<size=105%><b>{checkedName}</b></size>\n<size=80%>(Sent to {recipient})</size>";
                    }
                    else if (isScouted && scouted != null)
                    {
                        string recipient = (scouted.Player.Slot == client.SlotNumber) ? "You" : scouted.Player.Name;
                        string itemColorHex = "#00FFFF";
                        string typeLabel = "Archipelago Item";
                        if (scouted.Flags.HasFlag(ItemFlags.Advancement))
                        {
                            itemColorHex = "#FFD700";
                            typeLabel = "Progression Item";
                        }
                        else if (scouted.Flags.HasFlag(ItemFlags.Trap))
                        {
                            itemColorHex = "#FF4444";
                            typeLabel = "Trap Item";
                        }
                        else if (scouted.Flags.HasFlag(ItemFlags.NeverExclude))
                        {
                            itemColorHex = "#55FF55";
                            typeLabel = "Useful Item";
                        }

                        descField.text = $"<size=125%><color={itemColorHex}><b>{scouted.ItemDisplayName}</b></color></size>\n\n<size=90%>For: <b>{recipient}</b></size>\n\n<size=75%><color=#A0A0A0>{typeLabel}</color></size>";
                    }
                    else
                    {
                        string locName = LocationManager.IsShopLocation(locationId) ? "Round Shop Choice" : (LocationManager.GetLocationName(locationId) ?? "Round Shop Choice");
                        descField.text = $"<size=120%><color=#00FFFF><b>Archipelago Item</b></color></size>\n\n<size=85%>{locName}</size>\n\n<size=75%><color=#AAAAAA>Choose to check this location</color></size>";
                    }
                }

                // 4. Rarity Text
                var rarityField = Traverse.Create(card).Field("rarityText").GetValue<TMP_Text>();
                if (rarityField != null)
                {
                    if (isChecked)
                    {
                        rarityField.text = "<color=#888888>Checked</color>";
                    }
                    else if (isScouted && scouted != null)
                    {
                        if (scouted.Flags.HasFlag(ItemFlags.Advancement)) rarityField.text = "<color=#FFD700>Progression</color>";
                        else if (scouted.Flags.HasFlag(ItemFlags.Trap)) rarityField.text = "<color=#FF4444>Trap</color>";
                        else if (scouted.Flags.HasFlag(ItemFlags.NeverExclude)) rarityField.text = "<color=#55FF55>Useful</color>";
                        else rarityField.text = "<color=#00FFFF>Filler</color>";
                    }
                    else
                    {
                        rarityField.text = "<color=#00FFAA>Archipelago</color>";
                    }
                }

                card.EnableCard(instant: true);
            }
            else
            {
                // Unlocked Bonus: keep normal vanilla bonus appearance + add AP Check info at bottom
                int ownedMods = BonusInventory.Instance != null ? BonusInventory.Instance.numberOfOwnedModifiers : 0;
                int maxMods = BonusInventory.Instance != null ? BonusInventory.Instance.maximumModifiers : 6;
                int ownedUpgrades = BonusInventory.Instance != null ? BonusInventory.Instance.numberOfOwnedUpgrades : 0;
                int maxUpgrades = BonusInventory.Instance != null ? BonusInventory.Instance.maximumUpgrades : 3;

                bool isModFull = card.associatedBonus.BonusType == BaseBonus.Type.Modifier && ownedMods >= maxMods;
                bool isUpgradeFull = card.associatedBonus.BonusType == BaseBonus.Type.Upgrade && ownedUpgrades >= maxUpgrades;

                var headerField = Traverse.Create(card).Field("headerText").GetValue<TMP_Text>();
                string vanillaHeader = card.associatedBonus.BonusType == BaseBonus.Type.Upgrade
                    ? "Upgrade"
                    : (card.associatedBonus.specialRoundCard ? "Special" : (card.associatedBonus.BonusType == BaseBonus.Type.Gift ? "Gift" : "Modifier"));

                if (headerField != null)
                {
                    if (isChecked)
                    {
                        headerField.text = $"[CHECKED] {vanillaHeader}";
                    }
                    else if (isModFull)
                    {
                        headerField.text = $"[AP] {vanillaHeader} <color=#FF4444>[FULL 6/6]</color>";
                    }
                    else if (isUpgradeFull)
                    {
                        headerField.text = $"[AP] {vanillaHeader} <color=#FF4444>[FULL 3/3]</color>";
                    }
                    else
                    {
                        headerField.text = $"[AP] {vanillaHeader}";
                    }
                }

                var descField = Traverse.Create(card).Field("myDescription").GetValue<TMP_Text>();
                if (descField != null)
                {
                    string vanillaDesc;
                    if (card.associatedBonus.additionalDescription == BonusDescriptions.Description.None)
                    {
                        vanillaDesc = ShopManager.Instance != null
                            ? ShopManager.Instance.ApplyFormatting(card.associatedBonus.shopDescription, false)
                            : card.associatedBonus.shopDescription;
                    }
                    else
                    {
                        string extraDesc = BonusDescriptions.Instance != null
                            ? BonusDescriptions.Instance.ShopDescription(card.associatedBonus.additionalDescription)
                            : "";
                        vanillaDesc = ShopManager.Instance != null
                            ? ShopManager.Instance.ApplyFormatting(card.associatedBonus.shopDescription, false) + "\n<size=80%>" + ShopManager.Instance.ApplyFormatting(extraDesc, false) + "</size>"
                            : card.associatedBonus.shopDescription + "\n" + extraDesc;
                    }

                    string apInfo;
                    if (isChecked)
                    {
                        string checkedName = isScouted && scouted != null ? scouted.ItemDisplayName : "Archipelago Item";
                        string recipient = isScouted && scouted != null ? (scouted.Player.Slot == client.SlotNumber ? "You" : scouted.Player.Name) : "";
                        apInfo = $"<color=#888888><b>✓ Checked:</b> {checkedName} ({recipient})</color>";
                    }
                    else if (isScouted && scouted != null)
                    {
                        string recipient = (scouted.Player.Slot == client.SlotNumber) ? "You" : scouted.Player.Name;
                        string itemColorHex = "#00FFFF";
                        if (scouted.Flags.HasFlag(ItemFlags.Advancement)) itemColorHex = "#FFD700";
                        else if (scouted.Flags.HasFlag(ItemFlags.Trap)) itemColorHex = "#FF4444";
                        else if (scouted.Flags.HasFlag(ItemFlags.NeverExclude)) itemColorHex = "#55FF55";

                        apInfo = $"<b>AP Check:</b> <color={itemColorHex}>{scouted.ItemDisplayName}</color> <color=#DDDDDD>({recipient})</color>";
                    }
                    else
                    {
                        string locName = LocationManager.GetLocationName(locationId) ?? $"Shop Check #{cardIdx + 1}";
                        apInfo = $"<b>AP Check #{cardIdx + 1}:</b> <color=#00FFFF>{locName}</color>";
                    }

                    string warningText = "";
                    if (isModFull)
                    {
                        warningText = "\n\n<color=#FF4444><b>MODIFIERS FULL (6/6)</b></color>";
                    }
                    else if (isUpgradeFull)
                    {
                        warningText = "\n\n<color=#FF4444><b>UPGRADES FULL (3/3)</b></color>";
                    }

                    descField.margin = new Vector4(0f, 0f, 0f, 8f);
                    descField.text = $"{vanillaDesc}\n\n<size=85%>{apInfo}</size>{warningText}";
                }

                var rarityField = Traverse.Create(card).Field("rarityText").GetValue<TMP_Text>();
                if (rarityField != null)
                {
                    string rText = card.associatedBonus.UpgradeRarity.ToString();
                    rarityField.text = isChecked ? $"<color=#888888>{rText} • Checked</color>" : rText;
                }
            }
        }
    }

    [HarmonyPatch(typeof(BonusCardScript), nameof(BonusCardScript.FillInDetails))]
    public static class BonusCardFillInDetailsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(BonusCardScript __instance, BaseBonus newBonus, bool hoverDescriptionCard, bool testingOnly)
        {
            if (testingOnly || hoverDescriptionCard) return;
            var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
            if (client == null || !client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;
            if (ShopManager.Instance == null || ShopManager.Instance.upgradeCards == null) return;

            int cardIndex = ShopManager.Instance.upgradeCards.IndexOf(__instance);
            if (cardIndex < 0 || cardIndex >= 3) return;

            int currentRound = LogicManagerScript.Instance != null ? LogicManagerScript.Instance.currentRound : 1;
            ShopCardFormatter.FormatShopCard(__instance, cardIndex, currentRound);
        }
    }
}
