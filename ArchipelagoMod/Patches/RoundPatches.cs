using System;
using HarmonyLib;
using WordPlayArchipelago.Archipelago;

namespace WordPlayArchipelago.Patches
{
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
        [HarmonyPostfix]
        public static void Postfix()
        {
            Plugin.Logger.LogInfo($"[GameCompletePatch] GameComplete postfix. IsConnected: {Plugin.Instance.Client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}");

            if (!Plugin.Instance.Client.IsConnected || !ArchipelagoClient.IsArchipelagoRun) return;

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
            Plugin.Logger.LogInfo($"[LogicManagerStartPatch] Start postfix. IsConnected: {Plugin.Instance.Client.IsConnected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}");

            if (Plugin.Instance.Client.IsConnected && ArchipelagoClient.IsArchipelagoRun)
            {
                LocationManager.OnNewRun();
                __instance.roundsToWin = Plugin.Instance.Client.TotalRoundsForDifficulty;
                Plugin.Logger.LogInfo($"Configured game roundsToWin to {__instance.roundsToWin} based on AP difficulty ({Plugin.Instance.Client.Difficulty}).");

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
                ItemManager.ApplyWordLengthRestriction();
                ItemManager.ApplyBoardSlot();

                // Clear any leftover single-word traps at run start
                TrapsManager.VowelsScoreZeroActive = false;
                TrapsManager.BadSlotIndex = -1;
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
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (ArchipelagoClient.IsArchipelagoRun && LogicManagerScript.Instance != null)
            {
                LogicManagerScript.Instance.allowedNumberOfLetters = ItemManager.UnlockedWordLength;
            }
        }

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
        }
    }
}
