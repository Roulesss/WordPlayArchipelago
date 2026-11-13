using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace WordPlayArchipelago.Archipelago
{
    public class ItemManager
    {
        public const long BaseItemId = 91000;

        // Progressive word length capacity
        public static int StartingWordLength { get; set; } = 5;
        public static int ProgressiveWordLengthCount { get; set; } = 0;
        public static int UnlockedWordLength => Mathf.Clamp(StartingWordLength + ProgressiveWordLengthCount, 4, 20);
        public static int ExtraBoardSlotsReceived { get; set; } = 0;
        public static int AppliedBoardSlots { get; set; } = 0;
        public static int PendingBonusPlays { get; set; } = 0;
        public static int PendingBonusRefreshes { get; set; } = 0;

        public static void ResetRunState()
        {
            PendingBonusPlays = 0;
            PendingBonusRefreshes = 0;
        }

        public static void ResetAllState()
        {
            ProgressiveWordLengthCount = 0;
            ExtraBoardSlotsReceived = 0;
            AppliedBoardSlots = 0;
            PendingBonusPlays = 0;
            PendingBonusRefreshes = 0;
        }

        public static void HandleReceivedItem(long itemId, string itemName, bool silentReplay = false)
        {
            Plugin.Logger.LogInfo($"[ItemManager] Processing item: {itemName} (ID: {itemId}), silentReplay={silentReplay}");

            switch (itemId)
            {
                // Progression Items
                case BaseItemId + 1: // Progressive Word Length
                    ProgressiveWordLengthCount++;
                    Plugin.Logger.LogInfo($"[Progression] Progressive Word Length received! Total: {ProgressiveWordLengthCount}. Max word length now {UnlockedWordLength}!");
                    if (!silentReplay)
                    {
                        Plugin.Instance?.UI?.ShowBanner($"Progressive Word Length: Max {UnlockedWordLength} letters!", Color.green, 4f);
                    }
                    ApplyWordLengthRestriction();
                    break;

                case BaseItemId + 2: // Extra Board Slot
                    ExtraBoardSlotsReceived++;
                    Plugin.Logger.LogInfo($"[Progression] Extra Board Slot received! Total bonus: {ExtraBoardSlotsReceived}");
                    if (!silentReplay)
                    {
                        Plugin.Instance?.UI?.ShowBanner("Extra Board Slot Unlocked!", Color.green, 4f);
                    }
                    ApplyBoardSlot();
                    break;

                case BaseItemId + 3: // Extra Play (Progression/Useful)
                    if (!silentReplay)
                    {
                        if (LogicManagerScript.Instance != null)
                        {
                            LogicManagerScript.Instance.AddLives(2);
                        }
                        else
                        {
                            PendingBonusPlays += 2;
                        }
                        Plugin.Instance?.UI?.ShowBanner("+2 Plays Received!", Color.cyan, 3.5f);
                    }
                    break;

                case BaseItemId + 4: // Extra Refresh (Progression/Useful)
                    if (!silentReplay)
                    {
                        if (LogicManagerScript.Instance != null)
                        {
                            LogicManagerScript.Instance.ChangeNumberOfRefreshes(2);
                        }
                        else
                        {
                            PendingBonusRefreshes += 2;
                        }
                        Plugin.Instance?.UI?.ShowBanner("+2 Refreshes Received!", Color.yellow, 3.5f);
                    }
                    break;

                case BaseItemId + 5: // Golden Tile Pack
                    if (!silentReplay) InjectSpecialTiles(SpecialTileManager.TileTypes.Golden, 2, "Golden Tiles");
                    break;

                case BaseItemId + 6: // Diamond Tile Pack
                    if (!silentReplay) InjectSpecialTiles(SpecialTileManager.TileTypes.Diamond, 2, "Diamond Tiles");
                    break;

                case BaseItemId + 7: // Emerald Tile Pack
                    if (!silentReplay) InjectSpecialTiles(SpecialTileManager.TileTypes.Emerald, 2, "Emerald Tiles");
                    break;

                case BaseItemId + 8: // Dot Tile Pack
                    if (!silentReplay) InjectSpecialTiles(SpecialTileManager.TileTypes.FullStop, 2, "Dot Tiles");
                    break;

                // Filler Items
                case BaseItemId + 101:
                    if (!silentReplay) FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus5);
                    break;
                case BaseItemId + 102:
                    if (!silentReplay) FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus10);
                    break;
                case BaseItemId + 103:
                    if (!silentReplay) FillerManager.GrantFiller(FillerManager.FillerType.ExtraPlay);
                    break;
                case BaseItemId + 104:
                    if (!silentReplay) FillerManager.GrantFiller(FillerManager.FillerType.ExtraRefresh);
                    break;
                case BaseItemId + 105:
                    if (!silentReplay) FillerManager.GrantFiller(FillerManager.FillerType.AddGoldTile);
                    break;
                case BaseItemId + 106:
                    if (!silentReplay) FillerManager.GrantFiller(FillerManager.FillerType.AddDiamondTile);
                    break;

                // Trap Items
                case BaseItemId + 201:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.LosePlay);
                    break;
                case BaseItemId + 202:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.LoseRefresh);
                    break;
                case BaseItemId + 203:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.DeleteBagLetter);
                    break;
                case BaseItemId + 204:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.BadLetterSlot);
                    break;
                case BaseItemId + 205:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.VowelsZeroScore);
                    break;
                case BaseItemId + 206:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.ScrambleBoard);
                    break;
                case BaseItemId + 207:
                    if (!silentReplay) TrapsManager.TriggerTrap(TrapsManager.TrapType.Screamer);
                    break;

                default:
                    if (itemId >= 92000 && itemId <= 94017)
                    {
                        UnlockManager.RegisterUnlock(itemId);
                        Plugin.Logger.LogInfo($"[ItemManager] Unlocked perk via AP item: {itemName} (ID: {itemId})");
                        if (!silentReplay)
                        {
                            Plugin.Instance?.UI?.ShowBanner($"Unlocked: {itemName}!", Color.green, 4f);
                        }
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"[ItemManager] Unrecognized item ID: {itemId} ({itemName})");
                    }
                    break;
            }
        }

        public static void ApplyWordLengthRestriction()
        {
            int maxLen = UnlockedWordLength;
            Plugin.Logger.LogInfo($"[ItemManager] Applying Word Length Restriction: {maxLen} letters.");

            var sub = SubmissionScript.Instance;
            if (sub == null || sub.tileHolderScripts == null) return;

            sub.longestAllowedWord = maxLen;

            if (maxLen < 10)
            {
                for (int i = 0; i < 10 && i < sub.tileHolderScripts.Count; i++)
                {
                    var holder = sub.tileHolderScripts[i];
                    if (holder != null) holder.gameObject.SetActive(i < maxLen);
                }
                for (int i = 10; i < 20 && i < sub.tileHolderScripts.Count; i++)
                {
                    var holder = sub.tileHolderScripts[i];
                    if (holder != null) holder.gameObject.SetActive(false);
                }

                if (sub.topRowOfGrid != null)
                {
                    foreach (var item in sub.topRowOfGrid)
                    {
                        if (item != null) item.ChangeUpNavigationForLimitedLetters();
                    }
                }
                if (BonusInventory.Instance?.allUpgradeStubs != null)
                {
                    foreach (var allUpgradeStub in BonusInventory.Instance.allUpgradeStubs)
                    {
                        if (allUpgradeStub != null) allUpgradeStub.SetNavigation();
                    }
                }
                foreach (var tileHolderScript in sub.tileHolderScripts)
                {
                    if (tileHolderScript != null) tileHolderScript.ChangeDownNavigationForLimitedLetters();
                }
            }
            else
            {
                for (int i = 0; i < 10 && i < sub.tileHolderScripts.Count; i++)
                {
                    var holder = sub.tileHolderScripts[i];
                    if (holder != null) holder.gameObject.SetActive(true);
                }

                if (sub.numberOfFilledTiles >= 10 && !sub.ElevenPlusShowing)
                {
                    sub.ShowAllSubmissionHolders();
                }

                bool elevenPlus = sub.ElevenPlusShowing;
                for (int i = 10; i < 20 && i < sub.tileHolderScripts.Count; i++)
                {
                    var holder = sub.tileHolderScripts[i];
                    if (holder != null)
                    {
                        bool shouldBeActive = elevenPlus && (i < maxLen);
                        if (!shouldBeActive)
                        {
                            DOTween.Kill(holder);
                            holder.transform.DOKill();
                            var cg = holder.GetComponent<CanvasGroup>();
                            if (cg != null)
                            {
                                DOTween.Kill(cg);
                                cg.alpha = 0f;
                            }
                        }
                        holder.gameObject.SetActive(shouldBeActive);
                    }
                }

                if (sub.topRowOfGrid != null)
                {
                    foreach (var item in sub.topRowOfGrid)
                    {
                        if (item != null)
                        {
                            if (elevenPlus) item.AddSecondRowToUpNavigation();
                            else item.RestoreNormalUpNavigation();
                        }
                    }
                }
                if (BonusInventory.Instance?.allUpgradeStubs != null)
                {
                    foreach (var allUpgradeStub in BonusInventory.Instance.allUpgradeStubs)
                    {
                        if (allUpgradeStub != null) allUpgradeStub.SetNavigation();
                    }
                }
                for (int i = 0; i < 10 && i < sub.tileHolderScripts.Count; i++)
                {
                    var holder = sub.tileHolderScripts[i];
                    if (holder != null)
                    {
                        if (elevenPlus) holder.AddSecondRowToDownNavigation();
                        else holder.RestoreNormalDownNavigation();
                    }
                }
            }

            try
            {
                sub.GetRightMostSubmissionSlot();
            }
            catch { }

            if (TrapsManager.BadSlotIndex >= 0)
            {
                TrapsManager.UpdateBadSlotVisuals();
            }
        }

        public static void ApplyBoardSlot()
        {
            if (ToolsScript.Instance != null && ExtraBoardSlotsReceived > 0)
            {
                while (AppliedBoardSlots < ExtraBoardSlotsReceived &&
                       ToolsScript.Instance.HowManyAvailableSlots() > 0 &&
                       (GridManagerScript.Instance == null || GridManagerScript.Instance.letterSpaces == null || GridManagerScript.Instance.letterSpaces.Count < 19))
                {
                    Plugin.Logger.LogInfo($"[ItemManager] Unlocking extra board slot: {AppliedBoardSlots + 1}/{ExtraBoardSlotsReceived}");
                    ToolsScript.Instance.AddBonusTile();
                    ToolsScript.Instance.permaSlots++;
                    AppliedBoardSlots++;
                }
            }
        }

        private static void InjectSpecialTiles(SpecialTileManager.TileTypes type, int count, string label)
        {
            if (LetterBagManager.Instance == null) return;

            string[] vowelsAndCons = { "E", "A", "O", "T", "N", "S" };
            for (int i = 0; i < count; i++)
            {
                string letter = vowelsAndCons[UnityEngine.Random.Range(0, vowelsAndCons.Length)];
                string typeID = SpecialTileManager.Instance.GetTypeID(type);
                LetterBagManager.Instance.AddExtraLetters($"{letter}{typeID}0000", 1, showToaster: false);
            }

            Plugin.Instance.UI.ShowBanner($"+{count} {label} Added to Bag!", Color.magenta, 4f);
        }
    }
}
