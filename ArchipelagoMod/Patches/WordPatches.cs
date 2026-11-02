using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using HarmonyLib;
using TMPro;
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

            // Ensure VowelsDontScore is active in game logic if trap is active
            if (TrapsManager.VowelsScoreZeroActive && LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.vowelsDontScore = true;
            }

            // NOTE: Traps and Filler bonuses are consumed and reset in EndScreenStatsSubmittedWordPatch
            // after the word scoring coroutine has completely finished!
        }
    }

    [HarmonyPatch(typeof(ScoreZoneScript), nameof(ScoreZoneScript.RevertPingColour))]
    public static class ScoreZoneRevertPingColourPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;

            if (FillerManager.PendingWordBonusPoints > 0 && SubmitWord.Instance != null)
            {
                int bonus = FillerManager.PendingWordBonusPoints;
                Plugin.Logger?.LogInfo($"[Filler] Adding +{bonus} filler bonus points to word Bonus Score (currentWordMultiplier was {SubmitWord.Instance.currentWordMultiplier}).");
                SubmitWord.Instance.currentWordMultiplier += bonus;
                var scoreBar = Traverse.Create(SubmitWord.Instance).Field("scoreBar").GetValue<BarScript>();
                if (scoreBar != null)
                {
                    scoreBar.ChangeWordMulti(SubmitWord.Instance.currentWordMultiplier);
                }
            }
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

            // 3. Clear Filler Bonus Points after word scoring is complete
            if (FillerManager.PendingWordBonusPoints > 0)
            {
                Plugin.Logger?.LogInfo($"[Filler] Consumed +{FillerManager.PendingWordBonusPoints} filler bonus points.");
                FillerManager.PendingWordBonusPoints = 0;
            }

            // 4. Reset Single-Word Traps strictly after this word has completed scoring
            if (TrapsManager.VowelsScoreZeroActive || TrapsManager.BadSlotIndex != -1)
            {
                Plugin.Logger.LogInfo("[Trap] Resetting single-word trap states and restoring tile visuals.");
                TrapsManager.ResetSingleWordTrapsVisuals();
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

    [HarmonyPatch(typeof(LastWordArea), nameof(LastWordArea.ShowSpecialRoundText))]
    public static class LastWordAreaShowSpecialRoundTextPatch
    {
        private static int _savedAllowed = 20;

        [HarmonyPrefix]
        public static void Prefix()
        {
            if (ArchipelagoClient.IsArchipelagoRun && LogicManagerScript.Instance != null)
            {
                _savedAllowed = LogicManagerScript.Instance.allowedNumberOfLetters;
                // Only keep allowedNumberOfLetters < 20 if this is actually a wordLengthIncreases special round
                if (LogicManagerScript.Instance.currentSpecialRound == null ||
                    !LogicManagerScript.Instance.currentSpecialRound.wordLengthIncreases)
                {
                    LogicManagerScript.Instance.allowedNumberOfLetters = 20;
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (ArchipelagoClient.IsArchipelagoRun && LogicManagerScript.Instance != null)
            {
                if (LogicManagerScript.Instance.currentSpecialRound != null &&
                    LogicManagerScript.Instance.currentSpecialRound.wordLengthIncreases)
                {
                    LogicManagerScript.Instance.allowedNumberOfLetters = _savedAllowed;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.ShowAllSubmissionHolders))]
    public static class SubmissionScriptShowAllPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SubmissionScript __instance)
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;

            int maxLen = ItemManager.UnlockedWordLength;
            __instance.longestAllowedWord = maxLen;

            if (__instance.tileHolderScripts != null)
            {
                for (int j = 10; j < 20 && j < __instance.tileHolderScripts.Count; j++)
                {
                    var holder = __instance.tileHolderScripts[j];
                    if (holder != null && j >= maxLen)
                    {
                        DOTween.Kill(holder);
                        holder.transform.DOKill();
                        var cg = holder.GetComponent<CanvasGroup>();
                        if (cg != null)
                        {
                            DOTween.Kill(cg);
                            cg.alpha = 0f;
                        }
                        holder.gameObject.SetActive(false);
                    }
                }
            }

            if (TrapsManager.BadSlotIndex >= 0)
            {
                TrapsManager.UpdateBadSlotVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(TileHolderScript), nameof(TileHolderScript.FadeInThenShow))]
    public static class TileHolderFadeInThenShowPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TileHolderScript __instance)
        {
            if (ArchipelagoClient.IsArchipelagoRun && __instance.submissionSlot)
            {
                if (__instance.submissionID >= ItemManager.UnlockedWordLength)
                {
                    DOTween.Kill(__instance);
                    __instance.transform.DOKill();
                    var cg = __instance.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        DOTween.Kill(cg);
                        cg.alpha = 0f;
                    }
                    __instance.gameObject.SetActive(false);
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.Show10SubmissionHolders))]
    public static class SubmissionScriptShow10Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SubmissionScript __instance)
        {
            if (!ArchipelagoClient.IsArchipelagoRun) return;

            int maxLen = ItemManager.UnlockedWordLength;
            __instance.longestAllowedWord = maxLen;

            if (maxLen < 10 && __instance.tileHolderScripts != null)
            {
                for (int i = maxLen; i < 10 && i < __instance.tileHolderScripts.Count; i++)
                {
                    var holder = __instance.tileHolderScripts[i];
                    if (holder != null) holder.gameObject.SetActive(false);
                }
            }

            if (TrapsManager.BadSlotIndex >= 0)
            {
                TrapsManager.UpdateBadSlotVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.AddToSubmissionList))]
    public static class SubmissionScriptAddPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (ArchipelagoClient.IsArchipelagoRun && TrapsManager.BadSlotIndex >= 0)
            {
                TrapsManager.UpdateBadSlotVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.RemoveFromSubmissionList))]
    public static class SubmissionScriptRemovePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (ArchipelagoClient.IsArchipelagoRun && TrapsManager.BadSlotIndex >= 0)
            {
                TrapsManager.UpdateBadSlotVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.RestrictSubmissionBoxes))]
    public static class SubmissionScriptRestrictBoxesPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(SubmissionScript __instance, int newWordLength)
        {
            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                ItemManager.ApplyWordLengthRestriction();
                return false;
            }
            return true;
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

    [HarmonyPatch(typeof(SubmissionScript), nameof(SubmissionScript.AddToSubmissionList))]
    public static class SubmissionScriptAddToSubmissionPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SubmissionScript __instance)
        {
            if (ArchipelagoClient.IsArchipelagoRun && __instance != null)
            {
                if (__instance.numberOfFilledTiles >= 10 && ItemManager.UnlockedWordLength > 10 && !__instance.ElevenPlusShowing)
                {
                    __instance.ShowAllSubmissionHolders();
                }
            }
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
                if (__instance.longestAllowedWord != ItemManager.UnlockedWordLength)
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
                int targetIndex = ArchipelagoClient.IsArchipelagoRun ? ItemManager.UnlockedWordLength : (LogicManagerScript.Instance?.allowedNumberOfLetters ?? 0);
                if (list == null || targetIndex < 0 || targetIndex >= list.Count)
                {
                    return false;
                }
                var btn = Traverse.Create(__instance).Field("myControllerButton").GetValue<Button>();
                if (btn != null)
                {
                    var nav = btn.navigation;
                    nav.selectOnUp = list[targetIndex];
                    btn.navigation = nav;
                }
                return false;
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
                int targetIndex = ArchipelagoClient.IsArchipelagoRun ? ItemManager.UnlockedWordLength : (LogicManagerScript.Instance?.allowedNumberOfLetters ?? 0);
                if (list == null || targetIndex < 0 || targetIndex >= list.Count)
                {
                    return false;
                }
                var btn = Traverse.Create(__instance).Field("myControllerButton").GetValue<Button>();
                if (btn != null)
                {
                    var nav = btn.navigation;
                    nav.selectOnDown = list[targetIndex];
                    btn.navigation = nav;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(LetterDrawerManager), nameof(LetterDrawerManager.UpdateBag))]
    public static class LetterDrawerUpdateBagPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(LetterDrawerManager __instance)
        {
            try
            {
                if (LetterBagManager.Instance == null || __instance == null) return true;

                var bagTiles = Traverse.Create(__instance).Field("bagTiles").GetValue<List<BagTile>>();
                if (bagTiles == null) return true;

                var letterBagCount = Traverse.Create(__instance).Field("letterBagCount").GetValue<TMP_Text>();

                Traverse.Create(__instance).Field("lastCount").SetValue(LetterBagManager.Instance.LetterBag.Count);
                List<string> list = LetterBagManager.Instance.LetterBag.OrderBy((string letter) => letter).ToList();
                int spentIdx = 0;
                for (int i = 0; i < bagTiles.Count; i++)
                {
                    if (bagTiles[i] == null) continue;

                    if (i < list.Count)
                    {
                        bagTiles[i].gameObject.SetActive(true);
                        bagTiles[i].WriteText(list[i], spent: false);
                    }
                    else if (spentIdx < LetterBagManager.Instance.SpentLetters.Count)
                    {
                        bagTiles[i].gameObject.SetActive(true);
                        bagTiles[i].WriteText(LetterBagManager.Instance.SpentLetters[spentIdx], spent: true);
                        spentIdx++;
                    }
                    else
                    {
                        bagTiles[i].gameObject.SetActive(false);
                    }
                }

                if (letterBagCount != null)
                {
                    string countStr = LetterBagManager.Instance.LetterBag.Count.ToString();
                    if (letterBagCount.text != "<noparse></noparse>" + countStr)
                    {
                        letterBagCount.text = countStr;
                    }
                }

                Traverse.Create(__instance).Field("nextCount").SetValue(LetterBagManager.Instance.LetterBag.Count);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[LetterDrawerUpdateBagPatch] Handled safely: {ex.Message}");
                return true;
            }
        }
    }
}
