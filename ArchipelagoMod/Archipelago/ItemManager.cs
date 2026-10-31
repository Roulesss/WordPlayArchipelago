using System;
using System.Collections.Generic;
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
        public static int PendingBonusPlays { get; set; } = 0;
        public static int PendingBonusRefreshes { get; set; } = 0;

        public static void ResetRunState()
        {
            ProgressiveWordLengthCount = 0;
            ExtraBoardSlotsReceived = 0;
            PendingBonusPlays = 0;
            PendingBonusRefreshes = 0;
        }

        public static void HandleReceivedItem(long itemId, string itemName)
        {
            Plugin.Logger.LogInfo($"[ItemManager] Processing item: {itemName} (ID: {itemId})");

            switch (itemId)
            {
                // Progression Items
                case BaseItemId + 1: // Progressive Word Length
                    ProgressiveWordLengthCount++;
                    Plugin.Logger.LogInfo($"[Progression] Progressive Word Length received! Total: {ProgressiveWordLengthCount}. Max word length now {UnlockedWordLength}!");
                    Plugin.Instance.UI.ShowBanner($"Progressive Word Length: Max {UnlockedWordLength} letters!", Color.green, 4f);
                    ApplyWordLengthRestriction();
                    break;

                case BaseItemId + 2: // Extra Board Slot
                    ExtraBoardSlotsReceived++;
                    Plugin.Logger.LogInfo($"[Progression] Extra Board Slot received! Total bonus: {ExtraBoardSlotsReceived}");
                    Plugin.Instance.UI.ShowBanner("Extra Board Slot Unlocked!", Color.green, 4f);
                    ApplyBoardSlot();
                    break;

                case BaseItemId + 3: // Extra Play (Progression/Useful)
                    if (LogicManagerScript.Instance != null)
                    {
                        LogicManagerScript.Instance.AddLives(2);
                    }
                    else
                    {
                        PendingBonusPlays += 2;
                    }
                    Plugin.Instance.UI.ShowBanner("+2 Plays Received!", Color.cyan, 3.5f);
                    break;

                case BaseItemId + 4: // Extra Refresh (Progression/Useful)
                    if (LogicManagerScript.Instance != null)
                    {
                        LogicManagerScript.Instance.ChangeNumberOfRefreshes(2);
                    }
                    else
                    {
                        PendingBonusRefreshes += 2;
                    }
                    Plugin.Instance.UI.ShowBanner("+2 Refreshes Received!", Color.yellow, 3.5f);
                    break;

                case BaseItemId + 5: // Golden Tile Pack
                    InjectSpecialTiles(SpecialTileManager.TileTypes.Golden, 2, "Golden Tiles");
                    break;

                case BaseItemId + 6: // Diamond Tile Pack
                    InjectSpecialTiles(SpecialTileManager.TileTypes.Diamond, 2, "Diamond Tiles");
                    break;

                case BaseItemId + 7: // Emerald Tile Pack
                    InjectSpecialTiles(SpecialTileManager.TileTypes.Emerald, 2, "Emerald Tiles");
                    break;

                case BaseItemId + 8: // Dot Tile Pack
                    InjectSpecialTiles(SpecialTileManager.TileTypes.FullStop, 2, "Dot Tiles");
                    break;

                // Filler Items
                case BaseItemId + 101:
                    FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus5);
                    break;
                case BaseItemId + 102:
                    FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus10);
                    break;
                case BaseItemId + 103:
                    FillerManager.GrantFiller(FillerManager.FillerType.ExtraPlay);
                    break;
                case BaseItemId + 104:
                    FillerManager.GrantFiller(FillerManager.FillerType.ExtraRefresh);
                    break;
                case BaseItemId + 105:
                    FillerManager.GrantFiller(FillerManager.FillerType.AddGoldTile);
                    break;
                case BaseItemId + 106:
                    FillerManager.GrantFiller(FillerManager.FillerType.AddDiamondTile);
                    break;

                // Trap Items
                case BaseItemId + 201:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.LosePlay);
                    break;
                case BaseItemId + 202:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.LoseRefresh);
                    break;
                case BaseItemId + 203:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.DeleteBagLetter);
                    break;
                case BaseItemId + 204:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.BadLetterSlot);
                    break;
                case BaseItemId + 205:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.VowelsZeroScore);
                    break;
                case BaseItemId + 206:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.ScrambleBoard);
                    break;
                case BaseItemId + 207:
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.Screamer);
                    break;

                default:
                    if (itemId >= 92000 && itemId <= 94017)
                    {
                        UnlockManager.RegisterUnlock(itemId);
                        Plugin.Logger.LogInfo($"[ItemManager] Unlocked perk via AP item: {itemName} (ID: {itemId})");
                        Plugin.Instance?.UI?.ShowBanner($"Unlocked: {itemName}!", Color.green, 4f);
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
            if (LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.allowedNumberOfLetters = maxLen;
            }
            if (SubmissionScript.Instance != null)
            {
                SubmissionScript.Instance.longestAllowedWord = maxLen;
                if (SubmissionScript.Instance.tileHolderScripts != null)
                {
                    for (int i = 0; i < SubmissionScript.Instance.tileHolderScripts.Count && i < 20; i++)
                    {
                        var holder = SubmissionScript.Instance.tileHolderScripts[i];
                        if (holder == null) continue;

                        bool active = (i < maxLen);
                        holder.gameObject.SetActive(active);
                        if (active)
                        {
                            var cg = holder.GetComponent<CanvasGroup>();
                            if (cg != null)
                            {
                                cg.alpha = 1f;
                                cg.interactable = true;
                                cg.blocksRaycasts = true;
                            }
                        }
                    }
                }
                try
                {
                    SubmissionScript.Instance.RestrictSubmissionBoxes(maxLen);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[ItemManager] RestrictSubmissionBoxes warning: {ex.Message}");
                }
                try
                {
                    SubmissionScript.Instance.GetRightMostSubmissionSlot();
                }
                catch { }
            }
        }

        public static void ApplyBoardSlot()
        {
            if (ToolsScript.Instance != null && ExtraBoardSlotsReceived > 0)
            {
                while (ToolsScript.Instance.permaSlots < ExtraBoardSlotsReceived &&
                       ToolsScript.Instance.HowManyAvailableSlots() > 0)
                {
                    Plugin.Logger.LogInfo($"[ItemManager] Unlocking extra board slot: {ToolsScript.Instance.permaSlots + 1}/{ExtraBoardSlotsReceived}");
                    ToolsScript.Instance.AddBonusTile();
                    ToolsScript.Instance.permaSlots++;
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
