using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using HarmonyLib;
using TMPro;
using UnityEngine;
using WordPlayArchipelago.Archipelago;

namespace WordPlayArchipelago.Patches
{
    [Serializable]
    public class ApSaveData : SaveData.DataToSave
    {
        public bool shopIsShowing;
        public List<string> shopPerkNames = new List<string>();
        public List<long> shopLocationIds = new List<long>();
        public List<int> placeholderSlots = new List<int>();
        public int shopRerollCost = 1;
    }

    [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.AddLives))]
    public static class LogicManagerAddLivesPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (ShopSaveManager.IsResumingShop)
            {
                Plugin.Logger?.LogInfo("[ShopSaveManager] Suppressing duplicate AddLives during shop resume.");
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.FinishRound))]
    public static class RoundFinishPatch
    {
        [HarmonyPrefix]
        public static void Prefix(LogicManagerScript __instance)
        {
            Plugin.Logger.LogInfo($"[RoundFinishPatch] FinishRound prefix. IsConnected: {Plugin.Instance.Client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}, Round: {__instance?.currentRound}");

            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;
            if (__instance == null) return;

            int completedRound = __instance.currentRound;

            // In endless mode or if past the total rounds for this difficulty, do not send checks
            if (__instance.endlessMode || completedRound > Plugin.Instance.Client.TotalRoundsForDifficulty)
            {
                Plugin.Logger.LogInfo($"[RoundFinishPatch] Endless mode or round {completedRound} > {Plugin.Instance.Client.TotalRoundsForDifficulty}. Skipping AP round check.");
                return;
            }

            long locationId = LocationManager.GetRoundClearLocationId(completedRound);

            string desc = $"Round {completedRound} Clear";
            if (Plugin.Instance.Client.ScoutedLocations.TryGetValue(locationId, out var scouted))
            {
                string recipient = scouted.Player.Slot == Plugin.Instance.Client.SlotNumber ? "You" : scouted.Player.Name;
                desc += $": {scouted.ItemDisplayName} ({recipient})";
            }

            Plugin.Logger.LogInfo($"Player completed Round {completedRound}! Checking Location #{locationId} ({desc})...");
            Plugin.Instance.Client.CheckLocation(locationId, desc);
        }
    }

    [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.GameComplete))]
    public static class GameCompletePatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            Plugin.Logger.LogInfo($"[GameCompletePatch] GameComplete prefix. IsConnected: {Plugin.Instance.Client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}");

            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;

            // Clear in-progress run marker and AP save file since run has completed
            SaveDataSavePatch.ClearApResume();

            Plugin.Logger.LogInfo("Game Won! Recording Run Victory for Archipelago Goal...");
            Plugin.Instance.Client.RecordRunWin();
        }
    }

    [HarmonyPatch(typeof(LogicManagerScript), "Start")]
    public static class LogicManagerStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(LogicManagerScript __instance)
        {
            Plugin.Logger.LogInfo($"[LogicManagerStartPatch] Start postfix. IsConnected: {Plugin.Instance.Client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}, Resumed: {__instance.resumedGame}");

            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                // Only reset shop check rotation if this is a fresh run, not a resumed save!
                if (!__instance.resumedGame)
                {
                    LocationManager.OnNewRun();
                    ShopSaveManager.ClearShopState();
                }
                else
                {
                    Plugin.Logger.LogInfo("[LogicManagerStartPatch] Resumed game: preserving LocationManager shop check rotation.");
                    if (ShopSaveManager.LoadedShopIsShowing || (__instance.score >= __instance.scoreToNextRound && __instance.scoreToNextRound > 0 && !__instance.inSpellingMode))
                    {
                        Plugin.Logger.LogInfo("[LogicManagerStartPatch] Resumed game was at shop / end of round choice -> Re-opening shop!");
                        ShopSaveManager.IsResumingShop = true;
                        DOVirtual.DelayedCall(0.5f, () =>
                        {
                            if (ShopManager.Instance != null && !ShopManager.Instance.shopIsShowing)
                            {
                                ShopManager.Instance.ShowShop();
                            }
                            DOVirtual.DelayedCall(2.0f, () =>
                            {
                                ShopSaveManager.IsResumingShop = false;
                            });
                        });
                    }
                }

                __instance.roundsToWin = Plugin.Instance.Client.TotalRoundsForDifficulty;
                Plugin.Logger.LogInfo($"Configured game roundsToWin to {__instance.roundsToWin} based on AP difficulty ({Plugin.Instance.Client.Difficulty}).");

                // Record AP in-progress run state in PlayerPrefs
                PlayerPrefs.SetInt("AP_RunInProgress", 1);
                PlayerPrefs.SetString("AP_InProgressSlot", Plugin.Instance.Client.SlotName);
                PlayerPrefs.SetString("AP_InProgressSeed", Plugin.Instance.Client.SeedName);
                PlayerPrefs.SetString("AP_InProgressDifficulty", Plugin.Instance.Client.Difficulty);
                PlayerPrefs.SetInt("AP_InProgressRound", __instance.currentRound);
                PlayerPrefs.Save();

                if (ItemManager.PendingBonusPlays > 0)
                {
                    __instance.AddLives(ItemManager.PendingBonusPlays);
                    ItemManager.PendingBonusPlays = 0;
                }
                if (ItemManager.PendingBonusRefreshes > 0)
                {
                    __instance.ChangeNumberOfRefreshes(ItemManager.PendingBonusRefreshes);
                    ItemManager.PendingBonusRefreshes = 0;
                }

                // Enforce starting AP word length restriction & board slots
                ItemManager.AppliedBoardSlots = 0;
                ItemManager.ApplyWordLengthRestriction();
                ItemManager.ApplyBoardSlot();

                try
                {
                    SaveData.Instance?.Save();
                }
                catch { }

                // Clear any leftover single-word traps at run start and reset visuals
                TrapsManager.ResetSingleWordTrapsVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.GameOver))]
    public static class GameOverPatch
    {
        [HarmonyPrefix]
        public static void Prefix(bool noTiles)
        {
            try
            {
                var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
                if (client == null) return;

                Plugin.Logger.LogInfo($"[GameOverPatch] GameOver prefix. IsConnected: {client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}");

                if (!client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;

                // Clear in-progress run marker and AP save file since run has ended
                SaveDataSavePatch.ClearApResume();
                ShopSaveManager.ClearShopState();

                string reason = noTiles
                    ? $"{client.SlotName} ran out of tiles in the bag!"
                    : $"{client.SlotName} ran out of plays!";

                Plugin.Logger.LogInfo($"Game Over triggered. Sending Death Link ({reason})...");
                client.DeathLink?.SendDeath(reason);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[GameOverPatch] Exception in GameOver: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.GoToNextRound))]
    public static class GoToNextRoundPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Plugin.Logger.LogInfo($"[GoToNextRoundPatch] GoToNextRound postfix. IsApRun: {ArchipelagoClient.IsArchipelagoRun}");

            if (!ArchipelagoClient.IsArchipelagoRun) return;

            // Re-apply unlocked word length and slot restrictions for the new round
            ItemManager.ApplyWordLengthRestriction();

            // Re-apply VowelsDontScore if single-word trap is active across round transition
            if (TrapsManager.VowelsScoreZeroActive && LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.vowelsDontScore = true;
            }

            TrapsManager.RefreshAllTileVisuals();
            if (TrapsManager.BadSlotIndex >= 0)
            {
                TrapsManager.UpdateBadSlotVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(LogicManagerScript), nameof(LogicManagerScript.IsSpecialRound))]
    public static class LogicManagerIsSpecialRoundPatch
    {
        [HarmonyPrefix]
        public static void Prefix(LogicManagerScript __instance)
        {
            // Safeguard: wrap specialRoundWeAreOn to avoid IndexOutOfRangeException in endless or long runs
            if (__instance != null && __instance.specialRoundTypes != null && __instance.specialRoundTypes.Count > 0)
            {
                if (__instance.specialRoundWeAreOn >= __instance.specialRoundTypes.Count)
                {
                    __instance.specialRoundWeAreOn %= __instance.specialRoundTypes.Count;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SaveData), nameof(SaveData.Save))]
    public static class SaveDataSavePatch
    {
        public static string ApSaveFilePath => Path.Combine(Application.persistentDataPath, "ap_resumesavefile.json");

        public static void ClearApResume()
        {
            PlayerPrefs.SetInt("AP_RunInProgress", 0);
            PlayerPrefs.Save();
            ShopSaveManager.ClearShopState();
            try
            {
                string path = ApSaveFilePath;
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Plugin.Logger?.LogInfo("[ClearApResume] Deleted ap_resumesavefile.json");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[ClearApResume] Could not delete ap_resumesavefile.json: {ex.Message}");
            }
        }

        [HarmonyPrefix]
        public static bool Prefix(SaveData __instance)
        {
            if (ArchipelagoClient.IsArchipelagoRun && (Plugin.StaticClient?.IsConnected ?? false))
            {
                try
                {
                    Traverse.Create(__instance).Method("GetAllLetters").GetValue();
                    Traverse.Create(__instance).Method("GetAllBonuses").GetValue();
                    Traverse.Create(__instance).Method("GetAllLogicData").GetValue();

                    bool isShopShowing = ShopSaveManager.LoadedShopIsShowing;
                    if (ShopManager.Instance != null && ShopManager.Instance.shopIsShowing)
                    {
                        isShopShowing = true;
                        ShopSaveManager.RecordActiveShop();
                    }

                    var dataToSave = new ApSaveData
                    {
                        lettersInBag = __instance.lettersInBagToSave,
                        spentLetters = __instance.spentLettersToSave,
                        purchasedModifiers = __instance.purchasedModifiersToSave,
                        modifierInt1 = __instance.modifierInt1ToSave,
                        modifierInt2 = __instance.modifierInt2ToSave,
                        purchasedUpgrades = __instance.purchasedUpgradesToSave,
                        upgradeInt1 = __instance.upgradeInt1ToSave,
                        upgradeInt2 = __instance.upgradeInt2ToSave,
                        currentRound = __instance.currentRoundToSave,
                        lives = __instance.livesToSave,
                        mostLivesEver = __instance.mostLivesEverToSave,
                        score = __instance.scoreToSave,
                        refreshes = __instance.refreshesToSave,
                        previousUp = __instance.previousUpToSave,
                        scoreToNext = __instance.scoreToNextToSave,
                        wordsSpelledSoFar = __instance.wordSpelledSoFarToSave,
                        specialRoundLetterToStartFrom = __instance.specialRoundLetterToStartFromToSave,
                        specialRoundPicks = __instance.specialRoundPicksToSave,
                        specialRoundStuckLetter = __instance.specialRoundStuckLetterToSave,
                        specialRoundWeAreOn = __instance.specialRoundWeAreOnToSave,
                        mandatoryTileSpace = __instance.mandatoryTileSpaceToSave,
                        onRunawayStopper = __instance.onRunawayStopperToSave,
                        allowedTiles = __instance.allowedTilesToSave,
                        lockedTiles = __instance.lockedTilesToSave,
                        mustBeVowel = __instance.mustBeVowelToSave,
                        seed = __instance.seedToSave,
                        metaRule = __instance.metaRuleToSave,
                        gameLog = __instance.gameLogToSave,
                        gameLogRow = __instance.gameLogRowToSave,
                        totalScore = __instance.totalScoreToSave,
                        wordsSpelled = __instance.wordsSpelledToSave,
                        upgradesPurchased = __instance.upgradesPurchasedToSave,
                        bestWordText = __instance.bestWordTextToSave,
                        bestWordScore = __instance.bestWordScoreToSave,
                        lockedDiamonds = __instance.LockedDiamondsToSave,

                        shopIsShowing = isShopShowing,
                        shopPerkNames = new List<string>(ShopSaveManager.LoadedShopPerks),
                        shopLocationIds = new List<long>(ShopSaveManager.LoadedShopLocations),
                        placeholderSlots = new List<int>(ShopSaveManager.LoadedPlaceholderSlots),
                        shopRerollCost = ShopSaveManager.LoadedRerollCost
                    };

                    string contents = JsonUtility.ToJson(dataToSave, prettyPrint: true);
                    if (File.Exists(ApSaveFilePath))
                    {
                        File.Delete(ApSaveFilePath);
                    }
                    File.WriteAllText(ApSaveFilePath, contents);
                    Plugin.Logger?.LogInfo($"[SaveDataSavePatch] AP Data saved to {ApSaveFilePath} (shopShowing={isShopShowing})");

                    if (LogicManagerScript.Instance != null)
                    {
                        LogicManagerScript.Instance.haveSaveData = true;
                    }

                    PlayerPrefs.SetInt("AP_RunInProgress", 1);
                    PlayerPrefs.SetString("AP_InProgressSlot", Plugin.StaticClient.SlotName);
                    PlayerPrefs.SetString("AP_InProgressSeed", Plugin.StaticClient.SeedName);
                    PlayerPrefs.SetString("AP_InProgressDifficulty", Plugin.StaticClient.Difficulty);
                    int r = LogicManagerScript.Instance != null ? LogicManagerScript.Instance.currentRound : 1;
                    PlayerPrefs.SetInt("AP_InProgressRound", r);
                    PlayerPrefs.Save();
                    Plugin.Logger?.LogInfo($"[SaveDataSavePatch] Updated AP in-progress state: Round {r}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[SaveDataSavePatch] Error saving AP data: {ex}");
                }

                // Strictly skip vanilla Save() so resumesavefile.json and MetaGameRules are NOT touched!
                return false;
            }

            return true; // Let vanilla Save() run for normal games
        }
    }

    [HarmonyPatch(typeof(SaveData), nameof(SaveData.Load))]
    public static class SaveDataLoadPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SaveData __instance)
        {
            if (ArchipelagoClient.IsArchipelagoRun)
            {
                string path = SaveDataSavePatch.ApSaveFilePath;
                if (!File.Exists(path))
                {
                    Plugin.Logger?.LogWarning($"[SaveDataLoadPatch] AP Save file not found at {path}");
                    return false;
                }

                try
                {
                    ApSaveData dataToSave = JsonUtility.FromJson<ApSaveData>(File.ReadAllText(path));
                    SubmissionScript.Instance.LoadStuckLetter(dataToSave.specialRoundStuckLetter);
                    LogicManagerScript.Instance.LoadGameState(
                        dataToSave.currentRound, dataToSave.lives, dataToSave.mostLivesEver, dataToSave.score,
                        dataToSave.refreshes, dataToSave.scoreToNext, dataToSave.previousUp, dataToSave.specialRoundPicks,
                        dataToSave.specialRoundWeAreOn, dataToSave.onRunawayStopper, dataToSave.mandatoryTileSpace,
                        dataToSave.seed, dataToSave.metaRule, dataToSave.allowedTiles, dataToSave.lockedTiles,
                        dataToSave.wordsSpelledSoFar, dataToSave.mustBeVowel, dataToSave.specialRoundLetterToStartFrom
                    );
                    GameLog.Instance.LoadLog(dataToSave.gameLog, dataToSave.gameLogRow);
                    BonusInventory.Instance.LoadBonuses(
                        dataToSave.purchasedModifiers, dataToSave.modifierInt1, dataToSave.modifierInt2,
                        dataToSave.purchasedUpgrades, dataToSave.upgradeInt1, dataToSave.upgradeInt2
                    );
                    EndScreenStats.Instance.LoadEndScreenStats(
                        dataToSave.totalScore, dataToSave.wordsSpelled, dataToSave.upgradesPurchased,
                        dataToSave.bestWordText, dataToSave.bestWordScore
                    );
                    LetterBagManager.Instance.LoadLetters(dataToSave.lettersInBag, dataToSave.spentLetters);
                    MetaGameRules.Instance.currentRule = dataToSave.metaRule;
                    GridManagerScript.Instance.LoadLockedDiamonds(dataToSave.lockedDiamonds);

                    if (dataToSave.shopIsShowing || (dataToSave.score >= dataToSave.scoreToNext && dataToSave.scoreToNext > 0))
                    {
                        ShopSaveManager.LoadedShopIsShowing = true;
                        ShopSaveManager.LoadedShopPerks = dataToSave.shopPerkNames ?? new List<string>();
                        ShopSaveManager.LoadedShopLocations = dataToSave.shopLocationIds ?? new List<long>();
                        ShopSaveManager.LoadedPlaceholderSlots = dataToSave.placeholderSlots ?? new List<int>();
                        ShopSaveManager.LoadedRerollCost = dataToSave.shopRerollCost > 0 ? dataToSave.shopRerollCost : 1;
                        Plugin.Logger?.LogInfo($"[SaveDataLoadPatch] AP Save has active shop state: perks={ShopSaveManager.LoadedShopPerks.Count}, locations={ShopSaveManager.LoadedShopLocations.Count}, rerollCost={ShopSaveManager.LoadedRerollCost}");
                    }
                    else
                    {
                        ShopSaveManager.ClearShopState();
                    }

                    Plugin.Logger?.LogInfo($"[SaveDataLoadPatch] Successfully loaded AP Save from {path} (Round {dataToSave.currentRound})");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[SaveDataLoadPatch] Error loading AP save: {ex}");
                }

                return false; // Skip vanilla Load()!
            }

            return true; // Let vanilla Load() load normal resumesavefile.json
        }
    }

    [HarmonyPatch(typeof(MetaGameRules), nameof(MetaGameRules.ClearResume))]
    public static class MetaGameRulesClearResumePatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // If an Archipelago run is active, only clear the AP resume and NEVER touch vanilla hasInProgressData!
            if (ArchipelagoClient.IsArchipelagoRun)
            {
                SaveDataSavePatch.ClearApResume();
                Plugin.Logger?.LogInfo("[MetaGameRulesClearResumePatch] AP run active: Cleared AP resume only, preserved vanilla resume data.");
                return false; // Strictly prevent clearing vanilla hasInProgressData!
            }
            return true; // Let vanilla ClearResume() clear normal games
        }
    }

    [HarmonyPatch(typeof(MetaGameRules), nameof(MetaGameRules.FeedInGameState))]
    public static class MetaGameRulesFeedInGameStatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Archipelago runs must never alter vanilla personal stats, completion medals, or achievements
            if (ArchipelagoClient.IsArchipelagoRun)
            {
                Plugin.Logger?.LogInfo("[MetaGameRulesFeedInGameStatePatch] AP run active: Skipped vanilla personal stats / levelCompletion update.");
                return false; // Skip vanilla stats update, levelCompletion, achievements, and MetaGameSave!
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameOverScreen), nameof(GameOverScreen.ChangeToWinScreen))]
    public static class GameOverScreenChangeToWinPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameOverScreen __instance)
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;

            var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
            int completed = client != null ? client.CompletedWins : 1;
            int required = client != null ? client.WinsRequired : 1;

            string winTitle = completed >= required ? $"Archipelago Goal Won! ({completed}/{required})" : $"Run Won! ({completed}/{required} Wins)";
            try
            {
                Traverse.Create(__instance).Field("topTextTA").Method("SetText", winTitle, false).GetValue();
            }
            catch
            {
                // Fallback
            }

            var leftButtonText = Traverse.Create(__instance).Field("leftButtonText").GetValue<TMP_Text>();
            if (leftButtonText != null)
            {
                leftButtonText.text = completed < required ? $"Next Run ({completed}/{required})" : "Play Again";
                var unlockedCol = Traverse.Create(__instance).Field("unlockedButtonColour").GetValue<Color>();
                leftButtonText.color = unlockedCol;
            }

            var lockGO = Traverse.Create(__instance).Field("lockGO").GetValue<GameObject>();
            if (lockGO != null)
            {
                lockGO.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(GameOverScreen), nameof(GameOverScreen.PressLeftButton))]
    public static class GameOverScreenPressLeftButtonPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(GameOverScreen __instance)
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return true;

            bool showingWin = Traverse.Create(__instance).Field("showingWinScreen").GetValue<bool>();
            if (showingWin)
            {
                Plugin.Logger.LogInfo("[GameOverScreen] Left button clicked on Win screen during AP run -> Cleanly restarting next run for Archipelago!");
                __instance.RestartGame();
                return false; // Skip vanilla PlayEndlessMode()!
            }
            return true;
        }
    }
}
