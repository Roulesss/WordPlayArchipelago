using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using WordPlayArchipelago.Archipelago;

namespace WordPlayArchipelago.Patches
{
    [HarmonyPatch(typeof(SubmitWord), "WordWasAccepted")]
    public static class SubmitWordAcceptedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SubmitWord __instance)
        {
            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;

            // Apply Filler Bonus Points if pending
            if (FillerManager.PendingWordBonusPoints > 0)
            {
                int bonus = FillerManager.PendingWordBonusPoints;
                FillerManager.PendingWordBonusPoints = 0;

                Plugin.Logger.LogInfo($"Applying +{bonus} filler bonus points to score!");
                if (LogicManagerScript.Instance != null)
                {
                    LogicManagerScript.Instance.score += bonus;
                }
            }

            // Ensure VowelsDontScore is active in game logic if trap is active
            if (TrapsManager.VowelsScoreZeroActive && LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.vowelsDontScore = true;
            }

            // NOTE: We deliberately do NOT clear single-word traps here!
            // WordWasAccepted runs BEFORE WordIsCorrect scores tiles.
            // Traps are cleared in EndScreenStatsSubmittedWordPatch.Postfix after scoring finishes.
        }
    }

    [HarmonyPatch(typeof(EndScreenStats), nameof(EndScreenStats.SubmittedWord))]
    public static class EndScreenStatsSubmittedWordPatch
    {
        [HarmonyPostfix]
        public static void Postfix(int wordScore, string currentWord)
        {
            Plugin.Logger.LogInfo($"[EndScreenStatsSubmittedWordPatch] Word: '{currentWord}', Score: {wordScore}, IsConnected: {Plugin.Instance.Client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}");

            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;

            // 1. Check Single-Word Score Milestones
            foreach (var milestone in LocationManager.WordScoreMilestones)
            {
                if (wordScore >= milestone.Key)
                {
                    Plugin.Instance.Client.CheckLocation(milestone.Value, $"Word Score: {milestone.Key} Pts ({currentWord})");
                }
            }

            // 2. Check Run Score Milestones
            if (LogicManagerScript.Instance != null)
            {
                int currentRunScore = LogicManagerScript.Instance.score;
                foreach (var milestone in LocationManager.RunScoreMilestones)
                {
                    if (currentRunScore >= milestone.Key)
                    {
                        Plugin.Instance.Client.CheckLocation(milestone.Value, $"Run Score: {milestone.Key} Pts");
                    }
                }
            }

            // 3. Reset Single-Word Traps strictly after this word has completed scoring
            if (TrapsManager.VowelsScoreZeroActive)
            {
                TrapsManager.VowelsScoreZeroActive = false;
                if (LogicManagerScript.Instance != null &&
                    (LogicManagerScript.Instance.currentSpecialRound == null || !LogicManagerScript.Instance.currentSpecialRound.vowelsDontScore))
                {
                    LogicManagerScript.Instance.vowelsDontScore = false;
                }
                Plugin.Logger.LogInfo("[Trap] Vowels Zero Score trap expired after 1 word.");
            }

            if (TrapsManager.BadSlotIndex != -1)
            {
                Plugin.Logger.LogInfo($"[Trap] Bad Letter Slot #{TrapsManager.BadSlotIndex + 1} trap expired after 1 word.");
                TrapsManager.BadSlotIndex = -1;
            }
        }
    }

    [HarmonyPatch(typeof(TileScript), nameof(TileScript.GetTileScore))]
    public static class TileScriptGetTileScorePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TileScript __instance, ref int __result)
        {
            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return true;

            // Vowels Zero Score Trap
            if (TrapsManager.VowelsScoreZeroActive && VowelManager.Instance != null && VowelManager.Instance.IsThisTileAVowel(__instance.myLetter))
            {
                __result = 0;
                return false;
            }

            // Bad Letter Slot Trap
            if (TrapsManager.BadSlotIndex >= 0 && SubmissionScript.Instance != null &&
                TrapsManager.BadSlotIndex < SubmissionScript.Instance.submittingTiles.Count &&
                SubmissionScript.Instance.submittingTiles[TrapsManager.BadSlotIndex] == __instance)
            {
                __result = 0;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TileScript), nameof(TileScript.GetTileScoreWithDiamond))]
    public static class TileScriptGetTileScoreWithDiamondPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TileScript __instance, ref int __result)
        {
            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return true;

            // Vowels Zero Score Trap
            if (TrapsManager.VowelsScoreZeroActive && VowelManager.Instance != null && VowelManager.Instance.IsThisTileAVowel(__instance.myLetter))
            {
                __result = 0;
                return false;
            }

            // Bad Letter Slot Trap
            if (TrapsManager.BadSlotIndex >= 0 && SubmissionScript.Instance != null &&
                TrapsManager.BadSlotIndex < SubmissionScript.Instance.submittingTiles.Count &&
                SubmissionScript.Instance.submittingTiles[TrapsManager.BadSlotIndex] == __instance)
            {
                __result = 0;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BaseBonus), nameof(BaseBonus.OnLetterScored))]
    public static class BaseBonusOnLetterScoredPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string letter, int tileCount, ref float __result)
        {
            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return true;

            // Vowels Zero Score Trap
            if (TrapsManager.VowelsScoreZeroActive && VowelManager.Instance != null && VowelManager.Instance.IsThisTileAVowel(letter))
            {
                __result = 0f;
                return false;
            }

            // Bad Letter Slot Trap
            if (TrapsManager.BadSlotIndex >= 0 && tileCount == TrapsManager.BadSlotIndex)
            {
                __result = 0f;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.RestrictSubmissionBoxes))]
    public static class SubmissionScriptRestrictBoxesPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref int newWordLength)
        {
            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                newWordLength = ItemManager.UnlockedWordLength;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(SubmissionScript __instance)
        {
            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                int maxLen = ItemManager.UnlockedWordLength;
                __instance.longestAllowedWord = maxLen;
                if (__instance.tileHolderScripts != null)
                {
                    for (int i = 0; i < __instance.tileHolderScripts.Count && i < 20; i++)
                    {
                        var holder = __instance.tileHolderScripts[i];
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
            }
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.AllowAllSubmissionBoxes))]
    public static class SubmissionScriptAllowAllPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SubmissionScript __instance)
        {
            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                ItemManager.ApplyWordLengthRestriction();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), "FindFirstWithoutChildren")]
    public static class SubmissionScriptFindFirstPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SubmissionScript __instance)
        {
            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                if (__instance.longestAllowedWord > ItemManager.UnlockedWordLength)
                {
                    __instance.longestAllowedWord = ItemManager.UnlockedWordLength;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SubmitWord), nameof(SubmitWord.SubmitTheWord))]
    public static class SubmitWordCheckWordPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SubmitWord __instance)
        {
            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                if (SubmissionScript.Instance != null)
                {
                    int tileCount = 0;
                    foreach (var t in SubmissionScript.Instance.submittingTiles)
                    {
                        if (t != null) tileCount++;
                    }

                    if (tileCount > ItemManager.UnlockedWordLength)
                    {
                        Plugin.Logger.LogWarning($"[SubmitWord] Blocked word submission: {tileCount} tiles exceeds AP unlocked limit of {ItemManager.UnlockedWordLength}!");
                        Plugin.Instance.UI.ShowBanner($"Word Too Long! Max length is {ItemManager.UnlockedWordLength}", Color.red, 3f);
                        return false;
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(TileHolderScript), nameof(TileHolderScript.ChangeUpNavigationForLimitedLetters))]
    public static class TileHolderChangeUpNavPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TileHolderScript __instance)
        {
            try
            {
                var list = Traverse.Create(__instance).Field("whichButtonToGoTo").GetValue<List<Button>>();
                if (LogicManagerScript.Instance == null || list == null ||
                    LogicManagerScript.Instance.allowedNumberOfLetters < 0 ||
                    LogicManagerScript.Instance.allowedNumberOfLetters >= list.Count)
                {
                    // Skip the vanilla navigation remapping to prevent IndexOutOfRangeException
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(TileHolderScript), nameof(TileHolderScript.ChangeDownNavigationForLimitedLetters))]
    public static class TileHolderChangeDownNavPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TileHolderScript __instance)
        {
            try
            {
                var list = Traverse.Create(__instance).Field("whichButtonToGoTo").GetValue<List<Button>>();
                if (LogicManagerScript.Instance == null || list == null ||
                    LogicManagerScript.Instance.allowedNumberOfLetters < 0 ||
                    LogicManagerScript.Instance.allowedNumberOfLetters >= list.Count)
                {
                    // Skip the vanilla navigation remapping to prevent IndexOutOfRangeException
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
