using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordPlayArchipelago.Archipelago;

namespace WordPlayArchipelago.Patches
{
    public static class TitleLevelSelectPatches
    {
        public static bool IsApSelected { get; private set; } = false;
        public static LevelSelectScript? CurrentLevelSelect { get; private set; }
        public static GameObject? ApTileGameObject { get; private set; }
        public static Button? ApButton { get; private set; }

        private static bool _subscribedToEvents = false;

        public static void OnLevelSelectStart(LevelSelectScript __instance)
        {
            Plugin.Logger.LogInfo($"[LevelSelect] OnLevelSelectStart called for '{__instance.gameObject.name}'!");
            CurrentLevelSelect = __instance;
            IsApSelected = false;

            if (!_subscribedToEvents)
            {
                Plugin.Instance.Client.OnConnectionStatusChanged += () =>
                {
                    Plugin.Logger.LogInfo($"[TitleLevelSelectPatches] OnConnectionStatusChanged fired! (IsApSelected: {IsApSelected})");
                    LevelSelectScript? target = CurrentLevelSelect;
                    if (target == null || !target.gameObject.activeInHierarchy)
                    {
                        target = UnityEngine.Object.FindObjectOfType<LevelSelectScript>();
                        if (target != null) CurrentLevelSelect = target;
                    }

                    if (target != null && IsApSelected)
                    {
                        RefreshApDetails(target);
                    }
                };
                _subscribedToEvents = true;
            }

            CloneCasualButtonForArchipelago(__instance);
        }

        public static void CloneCasualButtonForArchipelago(LevelSelectScript levelSelect)
        {
            try
            {
                Plugin.Logger.LogInfo("[LevelSelect] CloneCasualButtonForArchipelago executing...");
                var pickerTiles = Traverse.Create(levelSelect).Field("pickerTiles").GetValue<List<GameObject>>();
                if (pickerTiles == null)
                {
                    Plugin.Logger.LogError("[LevelSelect] pickerTiles list is NULL!");
                    return;
                }

                Plugin.Logger.LogInfo($"[LevelSelect] pickerTiles.Count = {pickerTiles.Count}");
                if (pickerTiles.Count <= 6 || pickerTiles[6] == null)
                {
                    Plugin.Logger.LogWarning($"[LevelSelect] Cannot find Q tile at index 6! Count is {pickerTiles.Count}");
                    return;
                }

                GameObject qTile = pickerTiles[6];
                Plugin.Logger.LogInfo($"[LevelSelect] Found Q Tile: '{qTile.name}' under '{qTile.transform.parent?.name}'");

                // Find the Casual section container
                // We want the container that contains Q tile and has the "CASUAL" header,
                // but NOT the parent container that contains both "STRATEGIC" and "CASUAL".
                Transform? casualContainer = null;
                Transform current = qTile.transform.parent;

                while (current != null && current != levelSelect.transform)
                {
                    bool hasCasual = false;
                    bool hasStrategic = false;

                    foreach (TMP_Text t in current.GetComponentsInChildren<TMP_Text>(true))
                    {
                        string txt = t.text.Trim();
                        if (txt.Equals("CASUAL", StringComparison.OrdinalIgnoreCase)) hasCasual = true;
                        if (txt.Equals("STRATEGIC", StringComparison.OrdinalIgnoreCase)) hasStrategic = true;
                    }

                    if (hasCasual && !hasStrategic)
                    {
                        casualContainer = current;
                        break;
                    }

                    current = current.parent;
                }

                if (casualContainer == null)
                {
                    casualContainer = qTile.transform.parent;
                    Plugin.Logger.LogWarning($"[LevelSelect] Fallback: Using Q's direct parent as casualContainer: '{casualContainer.name}'");
                }
                else
                {
                    Plugin.Logger.LogInfo($"[LevelSelect] Found Casual Container: '{casualContainer.name}' under '{casualContainer.parent?.name}'");
                }

                Transform parentTransform = casualContainer.parent;
                if (parentTransform == null)
                {
                    Plugin.Logger.LogError("[LevelSelect] casualContainer has NO parent!");
                    return;
                }

                // Check if already cloned
                Transform existing = parentTransform.Find("ArchipelagoContainer");
                if (existing != null)
                {
                    Plugin.Logger.LogInfo("[LevelSelect] ArchipelagoContainer already exists in hierarchy. Ensuring it is active.");
                    ApTileGameObject = existing.gameObject;
                    ApTileGameObject.SetActive(true);
                    ApButton = ApTileGameObject.GetComponentInChildren<Button>();
                    return;
                }

                // Clone the Casual container
                GameObject apContainer = UnityEngine.Object.Instantiate(casualContainer.gameObject, parentTransform);
                apContainer.name = "ArchipelagoContainer";
                apContainer.SetActive(true);
                apContainer.transform.localScale = Vector3.one;
                apContainer.transform.localRotation = Quaternion.identity;
                apContainer.transform.SetSiblingIndex(casualContainer.GetSiblingIndex() + 1);

                // Offset position if parent is not an automatic LayoutGroup
                RectTransform casualRT = casualContainer.GetComponent<RectTransform>();
                RectTransform apRT = apContainer.GetComponent<RectTransform>();
                LayoutGroup parentLayout = parentTransform.GetComponent<LayoutGroup>();

                if (casualRT != null && apRT != null && parentLayout == null)
                {
                    float width = casualRT.rect.width > 30f ? casualRT.rect.width : (casualRT.sizeDelta.x > 30f ? casualRT.sizeDelta.x : 120f);
                    float offset = width + 25f;
                    apRT.anchoredPosition = new Vector2(casualRT.anchoredPosition.x + offset, casualRT.anchoredPosition.y);
                    Plugin.Logger.LogInfo($"[LevelSelect] Set AP Container anchoredPosition: {apRT.anchoredPosition} (Casual at {casualRT.anchoredPosition}, width: {width})");
                }
                else if (parentLayout != null)
                {
                    Plugin.Logger.LogInfo($"[LevelSelect] Parent '{parentTransform.name}' has LayoutGroup '{parentLayout.GetType().Name}'. Rebuilding layout.");
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentTransform.GetComponent<RectTransform>());
                }

                // Label category as "ARCHIPELAGO"
                foreach (TMP_Text tmp in apContainer.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.text.Trim().Equals("CASUAL", StringComparison.OrdinalIgnoreCase))
                    {
                        tmp.text = "ARCHIPELAGO";
                        tmp.fontSize = Mathf.Min(tmp.fontSize, 14f);
                        Plugin.Logger.LogInfo("[LevelSelect] Relabeled header to ARCHIPELAGO");
                    }
                }

                // Find tile button inside container and label it "AP"
                Button apButton = apContainer.GetComponentInChildren<Button>();
                if (apButton != null)
                {
                    ApButton = apButton;
                    ApTileGameObject = apButton.gameObject;

                    foreach (TMP_Text tmp in apButton.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (tmp.text.Trim().Equals("Q", StringComparison.OrdinalIgnoreCase))
                        {
                            tmp.text = "AP";
                            Plugin.Logger.LogInfo("[LevelSelect] Relabeled button text to AP");
                        }
                    }

                    // Hook click listener
                    apButton.onClick = new Button.ButtonClickedEvent();
                    apButton.onClick.AddListener(() =>
                    {
                        Plugin.Logger.LogInfo("[LevelSelect] AP tile clicked!");
                        OnApTileClicked(levelSelect, apButton.gameObject);
                    });

                    // Set up keyboard / gamepad navigation
                    Button qButton = qTile.GetComponent<Button>();
                    if (qButton != null)
                    {
                        Navigation qNav = qButton.navigation;
                        qNav.mode = Navigation.Mode.Explicit;
                        qNav.selectOnRight = apButton;
                        qButton.navigation = qNav;

                        Navigation apNav = apButton.navigation;
                        apNav.mode = Navigation.Mode.Explicit;
                        apNav.selectOnLeft = qButton;
                        apButton.navigation = apNav;
                    }
                }
                else
                {
                    Plugin.Logger.LogError("[LevelSelect] Could not find Button inside cloned apContainer!");
                }

                Plugin.Logger.LogInfo("[LevelSelect] Successfully created and initialized ARCHIPELAGO (AP) tile!");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[LevelSelect] Error cloning Casual button for Archipelago: {ex}");
            }
        }

        private static void OnApTileClicked(LevelSelectScript levelSelect, GameObject apTile)
        {
            IsApSelected = true;

            // Move pointer to AP tile
            Transform pointer = Traverse.Create(levelSelect).Field("pickerPointer").GetValue<Transform>();
            if (pointer != null)
            {
                pointer.SetParent(apTile.transform, worldPositionStays: false);
            }

            // Update big letter
            TMP_Text bigTileLetter = Traverse.Create(levelSelect).Field("bigTileLetter").GetValue<TMP_Text>();
            if (bigTileLetter != null)
            {
                bigTileLetter.text = "AP";
            }

            RefreshApDetails(levelSelect);
        }

        public static void RefreshApDetails(LevelSelectScript levelSelect)
        {
            TMP_Text detailTitle = Traverse.Create(levelSelect).Field("detailTitle").GetValue<TMP_Text>();
            TMP_Text detailDescription = Traverse.Create(levelSelect).Field("detailDescription").GetValue<TMP_Text>();
            TMP_Text detailBestScore = Traverse.Create(levelSelect).Field("detailBestScore").GetValue<TMP_Text>();
            Button playButton = Traverse.Create(levelSelect).Field("playButton").GetValue<Button>();

            bool connected = Plugin.Instance.Client.IsConnected;
            bool connecting = Plugin.Instance.Client.IsConnecting;
            string? lastError = Plugin.Instance.Client.LastError;

            Plugin.Logger.LogInfo($"[RefreshApDetails] Connected: {connected}, Connecting: {connecting}, LastError: '{lastError}'");

            string host = Plugin.Instance.ServerAddress.Value;
            int port = Plugin.Instance.ServerPort.Value;
            string slot = Plugin.Instance.SlotName.Value;

            if (detailTitle != null)
            {
                detailTitle.text = connected
                    ? $"ARCHIPELAGO: {Plugin.Instance.Client.Difficulty.ToUpper()} MODE"
                    : "ARCHIPELAGO MULTIWORLD";
            }

            if (detailDescription != null)
            {
                if (connected)
                {
                    detailDescription.text =
                        $"Server: {host}:{port}\n" +
                        $"Slot: {Plugin.Instance.Client.SlotName} | Rounds: {Plugin.Instance.Client.TotalRoundsForDifficulty}\n" +
                        $"Goal: Win {Plugin.Instance.Client.WinsRequired} Run(s)  [Progress: {Plugin.Instance.Client.CompletedWins}/{Plugin.Instance.Client.WinsRequired}]";
                }
                else if (connecting)
                {
                    detailDescription.text =
                        $"Connecting to {host}:{port} as '{slot}'...\n\n" +
                        "Establishing socket connection. Please wait...";
                }
                else
                {
                    detailDescription.text =
                        $"Server: {host}:{port}\n" +
                        $"Slot Name: {slot}\n" +
                        "Config file: BepInEx/config/com.archipelago.wordplay.cfg (auto-reloaded)";
                }
            }

            if (detailBestScore != null)
            {
                if (connected)
                {
                    detailBestScore.text = $"CONNECTED: {Plugin.Instance.Client.SlotName} (READY TO PLAY)";
                }
                else if (connecting)
                {
                    detailBestScore.text = $"CONNECTING TO {host}:{port}...";
                }
                else if (!string.IsNullOrEmpty(lastError))
                {
                    detailBestScore.text = $"FAILED: {lastError} (Check config)";
                }
                else
                {
                    detailBestScore.text = "NOT CONNECTED - CLICK CONNECT BELOW";
                }
            }

            if (playButton != null)
            {
                TMP_Text playText = playButton.GetComponentInChildren<TMP_Text>();

                // Completely overwrite ButtonClickedEvent to clear any inspector persistent calls
                playButton.onClick = new Button.ButtonClickedEvent();

                if (connecting)
                {
                    playButton.interactable = false;
                    if (playText != null) playText.text = "CONNECTING...";
                }
                else if (connected)
                {
                    playButton.interactable = true;
                    if (playText != null) playText.text = "PLAY";

                    playButton.onClick.AddListener(() =>
                    {
                        Plugin.Logger.LogInfo($"[LevelSelect] Connected -> Launching AP Game (Difficulty ID: {Plugin.Instance.Client.DifficultyId})!");
                        ArchipelagoClient.IsArchipelagoRun = true;
                        TitleScreenManager.Instance.PlayGame(Plugin.Instance.Client.DifficultyId);
                    });
                }
                else
                {
                    playButton.interactable = true;
                    if (playText != null) playText.text = !string.IsNullOrEmpty(lastError) ? "RETRY CONNECT" : "CONNECT";

                    playButton.onClick.AddListener(() =>
                    {
                        Plugin.Logger.LogInfo("[LevelSelect] AP Action Button Clicked -> Connecting to Archipelago...");
                        try
                        {
                            Plugin.Instance.Config.Reload();
                            Plugin.Logger.LogInfo("[LevelSelect] Reloaded com.archipelago.wordplay.cfg from disk.");
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"[LevelSelect] Could not reload config: {ex.Message}");
                        }

                        string h = Plugin.Instance.ServerAddress.Value;
                        int p = Plugin.Instance.ServerPort.Value;
                        string s = Plugin.Instance.SlotName.Value;
                        string pw = Plugin.Instance.Password.Value;

                        Plugin.Instance.Client.Connect(h, p, s, pw);
                        RefreshApDetails(levelSelect);
                    });
                }
            }
        }

        public static void OnChangeDetails(LevelSelectScript __instance, int levelID)
        {
            if (IsApSelected)
            {
                IsApSelected = false;
                Button? playButton = Traverse.Create(__instance).Field("playButton").GetValue<Button>();
                if (playButton != null)
                {
                    playButton.interactable = true;
                    playButton.onClick = new Button.ButtonClickedEvent();
                    playButton.onClick.AddListener(__instance.PlayLevel);

                    TMP_Text? playText = playButton.GetComponentInChildren<TMP_Text>();
                    if (playText != null)
                    {
                        playText.text = "Play";
                    }
                }
            }
        }
    }

    // Explicit individual patch classes

    [HarmonyPatch(typeof(LevelSelectScript), "Start")]
    public static class LevelSelectStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(LevelSelectScript __instance)
        {
            Plugin.Logger.LogInfo("[Harmony] LevelSelectScript.Start postfix triggered!");
            TitleLevelSelectPatches.OnLevelSelectStart(__instance);
        }
    }

    [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.OpenPage))]
    public static class TitleScreenOpenPagePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TitleScreenManager __instance, GameObject exitingPage, GameObject enteringPage, TitleControl.TitleControlZone enteringZone)
        {
            LevelSelectScript? ls = null;
            if (enteringPage != null)
            {
                ls = enteringPage.GetComponentInChildren<LevelSelectScript>(true);
            }
            if (ls == null && __instance != null)
            {
                ls = Traverse.Create(__instance).Field("levelSelect").GetValue<LevelSelectScript>();
            }
            if (ls != null)
            {
                TitleLevelSelectPatches.OnLevelSelectStart(ls);
            }
        }
    }

    [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.OpenLevelSelect))]
    public static class TitleScreenOpenLevelSelectPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TitleScreenManager __instance)
        {
            Plugin.Logger.LogInfo("[TitleScreenManager] OpenLevelSelect called!");
            LevelSelectScript levelSelect = Traverse.Create(__instance).Field("levelSelect").GetValue<LevelSelectScript>();
            if (levelSelect != null)
            {
                TitleLevelSelectPatches.OnLevelSelectStart(levelSelect);
            }
        }
    }

    [HarmonyPatch(typeof(LevelSelectScript), nameof(LevelSelectScript.ChangeDetails))]
    public static class LevelSelectChangeDetailsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(LevelSelectScript __instance, int levelID)
        {
            TitleLevelSelectPatches.OnChangeDetails(__instance, levelID);
        }
    }

    [HarmonyPatch(typeof(LevelSelectScript), nameof(LevelSelectScript.PlayLevel))]
    public static class LevelSelectPlayLevelPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(LevelSelectScript __instance)
        {
            if (TitleLevelSelectPatches.IsApSelected || ArchipelagoClient.IsArchipelagoRun)
            {
                Plugin.Logger.LogInfo($"[LevelSelectPlayLevelPatch] PlayLevel called for Archipelago! (Connected: {Plugin.Instance.Client.IsConnected})");
                if (!Plugin.Instance.Client.IsConnected)
                {
                    Plugin.Logger.LogWarning("[LevelSelectPlayLevelPatch] BLOCKED: Archipelago is not connected!");
                    return false; // Strict block
                }

                ArchipelagoClient.IsArchipelagoRun = true;
                int difficultyId = Plugin.Instance.Client.DifficultyId;
                Plugin.Logger.LogInfo($"[LevelSelectPlayLevelPatch] Launching Archipelago Run with Difficulty: {Plugin.Instance.Client.Difficulty} (Rule ID: {difficultyId})");
                TitleScreenManager.Instance.PlayGame(difficultyId);
                return false;
            }

            Plugin.Logger.LogInfo("[LevelSelectPlayLevelPatch] Vanilla level played. Setting IsArchipelagoRun = false.");
            ArchipelagoClient.IsArchipelagoRun = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.PlayGame), new Type[] { typeof(int) })]
    public static class TitleScreenPlayGamePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(int numberToPlay)
        {
            Plugin.Logger.LogInfo($"[TitleScreenPlayGamePatch] PlayGame called with rule #{numberToPlay} (IsApSelected: {TitleLevelSelectPatches.IsApSelected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}, Connected: {Plugin.Instance.Client.IsConnected})");
            if (TitleLevelSelectPatches.IsApSelected || ArchipelagoClient.IsArchipelagoRun)
            {
                if (!Plugin.Instance.Client.IsConnected)
                {
                    Plugin.Logger.LogWarning("[TitleScreenPlayGamePatch] HARD BLOCKED PlayGame because Archipelago is selected but NOT connected!");
                    return false; // Prevent launch!
                }
                ArchipelagoClient.IsArchipelagoRun = true;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(EasyTransition.TransitionManager), nameof(EasyTransition.TransitionManager.Transition), new Type[] { typeof(string), typeof(EasyTransition.TransitionSettings), typeof(float) })]
    public static class TransitionManagerPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string sceneName)
        {
            if (sceneName == "Game" && TitleLevelSelectPatches.IsApSelected && !Plugin.Instance.Client.IsConnected)
            {
                Plugin.Logger.LogWarning("[TransitionManagerPatch] HARD BLOCKED Transition to 'Game' scene because Archipelago is not connected!");
                return false; // Prevent scene load!
            }
            return true;
        }
    }
}
