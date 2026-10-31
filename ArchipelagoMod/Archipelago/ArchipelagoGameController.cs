using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WordPlayArchipelago.Patches;

namespace WordPlayArchipelago.Archipelago
{
    public class ArchipelagoGameController : MonoBehaviour
    {
        public static ArchipelagoGameController? Instance { get; private set; }

        private readonly HashSet<int> _completedRounds = new HashSet<int>();
        private readonly HashSet<long> _checkedMilestones = new HashSet<long>();
        private int _lastShopRoundFormatted = -1;
        private bool _victoryRecorded = false;
        private int _lastTrackedRound = 1;

        private void Awake()
        {
            Instance = this;
            Plugin.Logger.LogInfo("[ArchipelagoGameController] Awake called in Game scene!");
        }

        private void Start()
        {
            Plugin.Logger.LogInfo("[ArchipelagoGameController] Start called! Initializing Archipelago gameplay controllers...");

            if (LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.roundsToWin = Plugin.Instance.Client.TotalRoundsForDifficulty;
                Plugin.Logger.LogInfo($"[ArchipelagoGameController] Configured game roundsToWin = {LogicManagerScript.Instance.roundsToWin}");

                if (ItemManager.PendingBonusPlays > 0)
                {
                    LogicManagerScript.Instance.AddLives(ItemManager.PendingBonusPlays);
                    ItemManager.PendingBonusPlays = 0;
                }
                if (ItemManager.PendingBonusRefreshes > 0)
                {
                    LogicManagerScript.Instance.ChangeNumberOfRefreshes(ItemManager.PendingBonusRefreshes);
                    ItemManager.PendingBonusRefreshes = 0;
                }
            }

            // Enforce initial word length & bonus slots
            ItemManager.ApplyWordLengthRestriction();
            ItemManager.ApplyBoardSlot();
        }

        private void Update()
        {
            // If returning to Title or disconnected, destroy this controller
            if (!ArchipelagoClient.IsArchipelagoRun || !Plugin.Instance.Client.IsConnected || SceneManager.GetActiveScene().name != "Game")
            {
                Destroy(gameObject);
                return;
            }

            // Ensure client queues and incoming items are processed every frame
            Plugin.Instance?.Client?.Update();

            EnforceWordLengthRestrictions();
            MonitorRoundClearAndShop();
            MonitorRunScoreMilestones();
        }

        private void EnforceWordLengthRestrictions()
        {
            int allowed = ItemManager.UnlockedWordLength;

            if (LogicManagerScript.Instance != null && LogicManagerScript.Instance.allowedNumberOfLetters != allowed)
            {
                LogicManagerScript.Instance.allowedNumberOfLetters = allowed;
            }

            if (SubmissionScript.Instance != null)
            {
                if (SubmissionScript.Instance.longestAllowedWord != allowed)
                {
                    SubmissionScript.Instance.longestAllowedWord = allowed;
                    SubmissionScript.Instance.RestrictSubmissionBoxes(allowed);
                    Plugin.Logger.LogInfo($"[ArchipelagoGameController] Clamped submission boxes to {allowed} letters.");
                }

                // If user somehow manages to put more than allowed tiles in submission
                if (SubmissionScript.Instance.submittingTiles != null)
                {
                    int tileCount = 0;
                    foreach (var t in SubmissionScript.Instance.submittingTiles)
                    {
                        if (t != null) tileCount++;
                    }

                    if (tileCount > allowed)
                    {
                        if (SubmissionScript.Instance.submissionButton != null && SubmissionScript.Instance.submissionButton.interactable)
                        {
                            SubmissionScript.Instance.submissionButton.interactable = false;
                        }
                    }
                }
            }
        }

        private void MonitorRoundClearAndShop()
        {
            if (LogicManagerScript.Instance == null) return;

            int currentRound = LogicManagerScript.Instance.currentRound;

            // Detect if round advanced
            if (currentRound > _lastTrackedRound)
            {
                int justCleared = _lastTrackedRound;
                _lastTrackedRound = currentRound;

                if (_completedRounds.Add(justCleared))
                {
                    TriggerRoundClearCheck(justCleared);
                }
            }

            // Check if Shop Screen is active
            if (ShopManager.Instance != null && ShopManager.Instance.shopScreen != null && ShopManager.Instance.shopScreen.activeSelf)
            {
                // Shop is open! This also indicates currentRound was cleared!
                if (_completedRounds.Add(currentRound))
                {
                    TriggerRoundClearCheck(currentRound);
                }

                // Format the shop cards with scouted AP items
                if (_lastShopRoundFormatted != currentRound)
                {
                    _lastShopRoundFormatted = currentRound;
                    FormatShopCardsForRound(currentRound);
                }
            }
        }

        private void TriggerRoundClearCheck(int round)
        {
            long locationId = LocationManager.GetRoundClearLocationId(round);
            string desc = $"Round {round} Clear";

            if (Plugin.Instance.Client.ScoutedLocations.TryGetValue(locationId, out var scouted))
            {
                string recipient = scouted.Player.Slot == Plugin.Instance.Client.SlotNumber ? "You" : scouted.Player.Name;
                desc += $": {scouted.ItemDisplayName} ({recipient})";
            }

            Plugin.Logger.LogInfo($"[ArchipelagoGameController] Round {round} Complete! Checking location #{locationId} ({desc})...");
            Plugin.Instance.Client.CheckLocation(locationId, desc);

            // Check if this was the final round for the goal
            if (round >= LogicManagerScript.Instance.roundsToWin && !_victoryRecorded)
            {
                _victoryRecorded = true;
                Plugin.Logger.LogInfo("[ArchipelagoGameController] Final round reached! Recording run victory...");
                Plugin.Instance.Client.RecordRunWin();
            }
        }

        public void RefreshShopCardDisplays()
        {
            if (ShopManager.Instance != null && ShopManager.Instance.shopScreen != null && ShopManager.Instance.shopScreen.activeSelf)
            {
                int currentRound = LogicManagerScript.Instance != null ? LogicManagerScript.Instance.currentRound : 1;
                FormatShopCardsForRound(currentRound);
            }
        }

        private void FormatShopCardsForRound(int round)
        {
            if (ShopManager.Instance == null || ShopManager.Instance.upgradeCards == null) return;

            Plugin.Logger.LogInfo($"[ArchipelagoGameController] Formatting 3 Shop Draft Cards for Round {round}!");

            for (int c = 0; c < 3 && c < ShopManager.Instance.upgradeCards.Count; c++)
            {
                BonusCardScript card = ShopManager.Instance.upgradeCards[c];
                if (card != null)
                {
                    ShopCardFormatter.FormatShopCard(card, c, round);
                }
            }
        }

        private void MonitorRunScoreMilestones()
        {
            if (LogicManagerScript.Instance == null) return;

            int currentScore = LogicManagerScript.Instance.score;
            foreach (var milestone in LocationManager.RunScoreMilestones)
            {
                if (currentScore >= milestone.Key && _checkedMilestones.Add(milestone.Value))
                {
                    Plugin.Logger.LogInfo($"[ArchipelagoGameController] Run Score reached {milestone.Key}! Checking location #{milestone.Value}...");
                    Plugin.Instance.Client.CheckLocation(milestone.Value, $"Run Score: {milestone.Key} Pts");
                }
            }
        }
    }
}
