using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordPlayArchipelago.Archipelago
{
    public class TrapsManager
    {
        public enum TrapType
        {
            LosePlay,
            LoseRefresh,
            DeleteBagLetter,
            BadLetterSlot,
            VowelsZeroScore,
            ScrambleBoard,
            Screamer
        }

        // Active trap states
        public static int BadSlotIndex { get; set; } = -1; // -1 means none active
        public static bool VowelsScoreZeroActive { get; set; }

        public static void TriggerTrap(TrapType trap)
        {
            switch (trap)
            {
                case TrapType.LosePlay:
                    ExecuteLosePlay();
                    break;
                case TrapType.LoseRefresh:
                    ExecuteLoseRefresh();
                    break;
                case TrapType.DeleteBagLetter:
                    ExecuteDeleteBagLetter();
                    break;
                case TrapType.BadLetterSlot:
                    ExecuteBadLetterSlot();
                    break;
                case TrapType.VowelsZeroScore:
                    ExecuteVowelsZeroScore();
                    break;
                case TrapType.ScrambleBoard:
                    ExecuteScrambleBoard();
                    break;
                case TrapType.Screamer:
                    ExecuteScreamer();
                    break;
            }
        }

        private static void ExecuteScreamer()
        {
            Plugin.EnsureCoreGameObject();
            if (Plugin.StaticScreamer != null)
            {
                Plugin.StaticScreamer.TriggerScreamer();
            }
            else if (ScreamerManager.Instance != null)
            {
                ScreamerManager.Instance.TriggerScreamer();
            }
            else
            {
                Plugin.Logger?.LogWarning("[TrapsManager] ScreamerManager instance is null!");
            }
        }

        private static void ExecuteLosePlay()
        {
            if (LogicManagerScript.Instance == null) return;

            Plugin.Logger.LogWarning("[Trap] Triggered: Lose a Play!");
            (Plugin.StaticUI ?? Plugin.Instance?.UI)?.ShowBanner("TRAP: Lost 1 Play!", new Color(1f, 0.4f, 0.4f), 4f);

            // Deduct 1 play, but leave at minimum 1 to avoid instant death from trap unless already critical
            if (LogicManagerScript.Instance.lives > 1)
            {
                LogicManagerScript.Instance.LoseLife(1);
            }
            else
            {
                Plugin.Logger.LogInfo("[Trap] Player was at 1 life, spared fatal blow from trap.");
            }
        }

        private static void ExecuteLoseRefresh()
        {
            if (LogicManagerScript.Instance == null) return;

            Plugin.Logger.LogWarning("[Trap] Triggered: Lose a Refresh!");
            (Plugin.StaticUI ?? Plugin.Instance?.UI)?.ShowBanner("TRAP: Lost 1 Refresh (Discard)!", new Color(1f, 0.4f, 0.4f), 4f);

            if (LogicManagerScript.Instance.numberOfRefreshes > 0)
            {
                LogicManagerScript.Instance.ChangeNumberOfRefreshes(-1);
            }
        }

        private static void ExecuteDeleteBagLetter()
        {
            if (LetterBagManager.Instance == null) return;

            if (LetterBagManager.Instance.LetterBag.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, LetterBagManager.Instance.LetterBag.Count);
                string deletedTile = LetterBagManager.Instance.LetterBag[randomIndex];
                LetterBagManager.Instance.LetterBag.RemoveAt(randomIndex);

                char letter = deletedTile.Length > 0 ? deletedTile[0] : '?';
                Plugin.Logger.LogWarning($"[Trap] Deleted letter '{letter}' from Letter Bag!");
                (Plugin.StaticUI ?? Plugin.Instance?.UI)?.ShowBanner($"TRAP: Letter '{letter}' removed from Letter Bag!", new Color(1f, 0.3f, 0.3f), 4f);

                if (LetterDrawerManager.Instance != null)
                {
                    LetterDrawerManager.Instance.UpdateBag();
                }
            }
        }

        private static void ExecuteBadLetterSlot()
        {
            // Set a random slot index (e.g. 0 to Math.Min(4, UnlockedWordLength - 1)) that will yield 0 score on the next submission
            int maxSlot = Math.Min(4, ItemManager.UnlockedWordLength - 1);
            if (maxSlot < 0) maxSlot = 0;
            BadSlotIndex = UnityEngine.Random.Range(0, maxSlot + 1);
            Plugin.Logger.LogWarning($"[Trap] Letter Slot #{BadSlotIndex + 1} scores 0 on next word!");
            (Plugin.StaticUI ?? Plugin.Instance?.UI)?.ShowBanner($"TRAP: Letter Slot #{BadSlotIndex + 1} will score 0 points!", new Color(1f, 0.5f, 0.2f), 5f);
        }

        private static void ExecuteVowelsZeroScore()
        {
            VowelsScoreZeroActive = true;
            if (LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.vowelsDontScore = true;
            }
            Plugin.Logger.LogWarning("[Trap] Vowels score 0 on next word!");
            (Plugin.StaticUI ?? Plugin.Instance?.UI)?.ShowBanner("TRAP: Vowels will award 0 points on next word!", new Color(1f, 0.4f, 0.4f), 5f);
        }

        private static void ExecuteScrambleBoard()
        {
            if (ToolsScript.Instance != null && ToolsScript.Instance.refresh != null)
            {
                Plugin.Logger.LogWarning("[Trap] Scrambled Board!");
                (Plugin.StaticUI ?? Plugin.Instance?.UI)?.ShowBanner("TRAP: Board Scrambled!", new Color(1f, 0.6f, 0.2f), 4f);
                ToolsScript.Instance.refresh.RefreshAllTiles(refreshIsFree: true, playerActivatedRefresh: false);
            }
        }
    }
}
