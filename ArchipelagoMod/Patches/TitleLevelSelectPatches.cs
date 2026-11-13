using System;
using System.Collections.Generic;
using System.IO;
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

        private static GameObject? _apButtonsRoot;
        private static Button? _apSingleButton;
        private static Button? _apResumeButton;
        private static Button? _apNewRunButton;

        private static bool _subscribedToEvents = false;

        public static void SetApSelected(bool selected)
        {
            IsApSelected = selected;
        }

        public static void HideApButtons(LevelSelectScript? levelSelect)
        {
            IsApSelected = false;

            if (_apButtonsRoot != null)
            {
                _apButtonsRoot.SetActive(false);
            }
            if (_apSingleButton != null) _apSingleButton.gameObject.SetActive(false);
            if (_apResumeButton != null) _apResumeButton.gameObject.SetActive(false);
            if (_apNewRunButton != null) _apNewRunButton.gameObject.SetActive(false);

            LevelSelectScript? target = levelSelect ?? CurrentLevelSelect;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = UnityEngine.Object.FindObjectOfType<LevelSelectScript>();
                if (target != null) CurrentLevelSelect = target;
            }

            if (target != null)
            {
                Button? playButton = Traverse.Create(target).Field("playButton").GetValue<Button>();
                if (playButton != null)
                {
                    Transform? parent = playButton.transform.parent;
                    if (parent != null)
                    {
                        for (int i = 0; i < parent.childCount; i++)
                        {
                            Transform child = parent.GetChild(i);
                            if (child != null && child.name.StartsWith("AP_"))
                            {
                                child.gameObject.SetActive(false);
                            }
                        }
                    }
                    playButton.gameObject.SetActive(true);
                }
            }
        }

        public static void OnLevelSelectStart(LevelSelectScript __instance)
        {
            Plugin.Logger.LogInfo($"[LevelSelect] OnLevelSelectStart called for '{__instance.gameObject.name}'!");
            CurrentLevelSelect = __instance;
            HideApButtons(__instance);

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
                if (connected)
                {
                    string diff = Plugin.Instance.Client.Difficulty;
                    string diffTitle = !string.IsNullOrEmpty(diff)
                        ? char.ToUpper(diff[0]) + diff.Substring(1).ToLower()
                        : "Normal";
                    detailTitle.text = $"Archipelago: {diffTitle} Mode";
                }
                else
                {
                    detailTitle.text = "Archipelago Multiworld";
                }
            }

            if (detailDescription != null)
            {
                if (connected)
                {
                    int rounds = Plugin.Instance.Client.TotalRoundsForDifficulty;
                    int winsReq = Plugin.Instance.Client.WinsRequired;
                    int completedWins = Plugin.Instance.Client.CompletedWins;

                    string winGoal = winsReq > 1
                        ? $"Goal: Win {winsReq} runs ({completedWins}/{winsReq}) • {rounds} rounds"
                        : $"Goal: Win 1 run • {rounds} rounds";

                    detailDescription.text = winGoal;
                }
                else if (connecting)
                {
                    detailDescription.text = $"Connecting to {host}:{port} as '{slot}'...";
                }
                else
                {
                    detailDescription.text = $"Connect to {host}:{port} as '{slot}'\nConfig: BepInEx/config/com.archipelago.wordplay.cfg";
                }
            }

            if (detailBestScore != null)
            {
                if (connected)
                {
                    detailBestScore.text = $"Connected: {Plugin.Instance.Client.SlotName}";
                }
                else if (connecting)
                {
                    detailBestScore.text = "Connecting to server...";
                }
                else if (!string.IsNullOrEmpty(lastError))
                {
                    detailBestScore.text = $"Connection error: {lastError}";
                }
                else
                {
                    detailBestScore.text = "Not connected";
                }
            }

            if (playButton != null)
            {
                EnsureApButtons(playButton);

                // Hide vanilla playButton completely so it is never modified or distorted
                playButton.gameObject.SetActive(false);
                if (_apButtonsRoot != null) _apButtonsRoot.SetActive(true);

                bool hasApInProgress = PlayerPrefs.GetInt("AP_RunInProgress", 0) == 1 && System.IO.File.Exists(SaveDataSavePatch.ApSaveFilePath);
                int inProgressRound = PlayerPrefs.GetInt("AP_InProgressRound", 1);

                if (connecting)
                {
                    if (_apResumeButton != null) _apResumeButton.gameObject.SetActive(false);
                    if (_apNewRunButton != null) _apNewRunButton.gameObject.SetActive(false);

                    if (_apSingleButton != null)
                    {
                        _apSingleButton.gameObject.SetActive(true);
                        _apSingleButton.interactable = false;
                        var sText = _apSingleButton.GetComponentInChildren<TMP_Text>();
                        if (sText != null) sText.text = "Connecting...";
                    }
                }
                else if (connected)
                {
                    if (hasApInProgress)
                    {
                        if (_apSingleButton != null) _apSingleButton.gameObject.SetActive(false);

                        Image vImg = playButton.GetComponent<Image>();
                        Sprite vanillaSprite = vImg != null ? vImg.sprite : null!;
                        float btnWidth = 460f;
                        float btnHeight = playButton.GetComponent<RectTransform>() != null && playButton.GetComponent<RectTransform>().sizeDelta.y > 0 ? playButton.GetComponent<RectTransform>().sizeDelta.y : 60f;
                        float spacing = 25f;
                        float leftX = -(btnWidth / 2f + spacing / 2f);
                        float rightX = (btnWidth / 2f + spacing / 2f);

                        // 1. Resume Button
                        if (_apResumeButton != null)
                        {
                            _apResumeButton.gameObject.SetActive(true);
                            _apResumeButton.interactable = true;
                            ConfigureApButton(_apResumeButton.gameObject, $"Resume (Round {inProgressRound})", isAmber: false, leftX, vanillaSprite, btnWidth, btnHeight);

                            _apResumeButton.onClick = new Button.ButtonClickedEvent();
                            _apResumeButton.onClick.AddListener(() =>
                            {
                                Plugin.Logger.LogInfo($"[LevelSelect] Connected -> Resuming AP Game at Round {inProgressRound}!");
                                ArchipelagoClient.IsArchipelagoRun = true;
                                TitleScreenManager.Instance.PlayGame(0);
                            });
                        }

                        // 2. New Run Button
                        if (_apNewRunButton != null)
                        {
                            _apNewRunButton.gameObject.SetActive(true);
                            _apNewRunButton.interactable = true;
                            ConfigureApButton(_apNewRunButton.gameObject, "New Run", isAmber: true, rightX, vanillaSprite, btnWidth, btnHeight);

                            _apNewRunButton.onClick = new Button.ButtonClickedEvent();
                            _apNewRunButton.onClick.AddListener(() =>
                            {
                                Plugin.Logger.LogInfo("[LevelSelect] 'New Run' clicked -> Clearing saved resume and starting fresh AP run!");
                                SaveDataSavePatch.ClearApResume();
                                ArchipelagoClient.IsArchipelagoRun = true;
                                TitleScreenManager.Instance.PlayGame(Plugin.Instance.Client.DifficultyId);
                            });
                        }
                    }
                    else
                    {
                        if (_apResumeButton != null) _apResumeButton.gameObject.SetActive(false);
                        if (_apNewRunButton != null) _apNewRunButton.gameObject.SetActive(false);

                        if (_apSingleButton != null)
                        {
                            _apSingleButton.gameObject.SetActive(true);
                            _apSingleButton.interactable = true;
                            var sText = _apSingleButton.GetComponentInChildren<TMP_Text>();
                            if (sText != null) sText.text = "Play";

                            _apSingleButton.onClick = new Button.ButtonClickedEvent();
                            _apSingleButton.onClick.AddListener(() =>
                            {
                                Plugin.Logger.LogInfo($"[LevelSelect] Connected -> Launching AP Game (Difficulty ID: {Plugin.Instance.Client.DifficultyId})!");
                                ArchipelagoClient.IsArchipelagoRun = true;
                                TitleScreenManager.Instance.PlayGame(Plugin.Instance.Client.DifficultyId);
                            });
                        }
                    }
                }
                else
                {
                    if (_apResumeButton != null) _apResumeButton.gameObject.SetActive(false);
                    if (_apNewRunButton != null) _apNewRunButton.gameObject.SetActive(false);

                    if (_apSingleButton != null)
                    {
                        _apSingleButton.gameObject.SetActive(true);
                        _apSingleButton.interactable = true;
                        var sText = _apSingleButton.GetComponentInChildren<TMP_Text>();
                        if (sText != null) sText.text = !string.IsNullOrEmpty(lastError) ? "Retry Connect" : "Connect";

                        _apSingleButton.onClick = new Button.ButtonClickedEvent();
                        _apSingleButton.onClick.AddListener(() =>
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
        }

        private static Sprite? _cachedAmberButtonSprite;

        public static Sprite GetAmberButtonSprite(Sprite source)
        {
            if (_cachedAmberButtonSprite != null) return _cachedAmberButtonSprite;
            if (source == null) return source!;

            try
            {
                Texture2D srcTex = source.texture;
                if (srcTex == null) return source;

                RenderTexture rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(srcTex, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;

                Texture2D readableTex = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
                readableTex.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
                readableTex.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color[] pixels = readableTex.GetPixels();
                float maxLum = 0.01f;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 0.1f)
                    {
                        float lum = pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
                        if (lum > maxLum) maxLum = lum;
                    }
                }

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.a > 0.01f)
                    {
                        float lum = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / maxLum;
                        lum = Mathf.Clamp01(lum);
                        pixels[i] = new Color(lum, lum, lum, c.a);
                    }
                }

                readableTex.SetPixels(pixels);
                readableTex.Apply();

                _cachedAmberButtonSprite = Sprite.Create(
                    readableTex,
                    source.rect,
                    source.pivot,
                    source.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect,
                    source.border
                );
                _cachedAmberButtonSprite.name = "AP_Pill_Amber_Base";
                Plugin.Logger.LogInfo("[LevelSelect] Successfully generated normalized grayscale sprite for Amber button!");
                return _cachedAmberButtonSprite;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[LevelSelect] Failed to create grayscale sprite: {ex}");
                return source;
            }
        }

        private static void ConfigureApButton(GameObject btnGo, string label, bool isAmber, float posX, Sprite vanillaSprite, float btnWidth, float btnHeight)
        {
            RectTransform bRT = btnGo.GetComponent<RectTransform>();
            if (bRT != null)
            {
                bRT.anchorMin = new Vector2(0.5f, 0.5f);
                bRT.anchorMax = new Vector2(0.5f, 0.5f);
                bRT.pivot = new Vector2(0.5f, 0.5f);
                bRT.sizeDelta = new Vector2(btnWidth, btnHeight);
                bRT.anchoredPosition = new Vector2(posX, 0f);
            }

            // Set up Image & Color
            Image img = btnGo.GetComponent<Image>();
            if (img != null)
            {
                var effect = btnGo.GetComponent<Coffee.UIEffects.UIEffect>();
                if (effect != null) UnityEngine.Object.Destroy(effect);

                if (isAmber)
                {
                    img.sprite = GetAmberButtonSprite(vanillaSprite);
                    img.color = new Color(0.98f, 0.62f, 0.12f, 1f); // Vibrant true amber/gold
                }
                else
                {
                    img.sprite = vanillaSprite;
                    img.color = Color.white; // Vanilla green sprite
                }
            }

            // Button selection colors (prevent ButtonTextColouriser or Button from forcing green on hover)
            Button btn = btnGo.GetComponent<Button>();
            if (btn != null && isAmber)
            {
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
                cb.selectedColor = Color.white;
                btn.colors = cb;
            }

            // Check if root has UIOutline
            var rootOutline = btnGo.GetComponent<UIOutline>();
            if (rootOutline != null)
            {
                rootOutline.enabled = true;
                rootOutline.SetVerticesDirty();
            }

            // Inspect and configure children
            Transform? outlineTransform = null;
            Transform? arrowTransform = null;
            TMP_Text? labelTMP = null;

            for (int i = 0; i < btnGo.transform.childCount; i++)
            {
                Transform child = btnGo.transform.GetChild(i);
                child.gameObject.SetActive(true);

                var tmp = child.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    labelTMP = tmp;
                    continue;
                }

                var uio = child.GetComponent<UIOutline>();
                string cName = child.name.ToLower();
                if (uio != null || cName.Contains("outline") || cName.Contains("border") || cName.Contains("contour"))
                {
                    outlineTransform = child;
                }
                else
                {
                    arrowTransform = child;
                }
            }

            // 1. Text Label
            if (labelTMP != null)
            {
                labelTMP.rectTransform.anchorMin = Vector2.zero;
                labelTMP.rectTransform.anchorMax = Vector2.one;
                labelTMP.rectTransform.offsetMin = new Vector2(30f, 0f);
                labelTMP.rectTransform.offsetMax = new Vector2(-80f, 0f);
                labelTMP.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                labelTMP.alignment = TextAlignmentOptions.Center;
                labelTMP.fontSize = 20f;
                labelTMP.enableWordWrapping = false;
                labelTMP.text = label;
            }

            // 2. Outline / Contour
            if (outlineTransform != null)
            {
                RectTransform oRT = outlineTransform.GetComponent<RectTransform>();
                if (oRT != null)
                {
                    oRT.anchorMin = Vector2.zero;
                    oRT.anchorMax = Vector2.one;
                    oRT.offsetMin = Vector2.zero;
                    oRT.offsetMax = Vector2.zero;
                    oRT.pivot = new Vector2(0.5f, 0.5f);
                }
                var uio = outlineTransform.GetComponent<UIOutline>();
                if (uio != null)
                {
                    uio.enabled = true;
                    uio.SetVerticesDirty();
                }
            }
            else if (rootOutline == null)
            {
                // Create AP_Outline with UIOutline so the button ALWAYS has the comic contour!
                Transform existingOutline = btnGo.transform.Find("AP_Outline");
                GameObject oGo = existingOutline != null ? existingOutline.gameObject : new GameObject("AP_Outline", typeof(RectTransform));
                oGo.transform.SetParent(btnGo.transform, false);
                oGo.transform.SetSiblingIndex(0); // Place behind text and arrow
                RectTransform oRT = oGo.GetComponent<RectTransform>();
                oRT.anchorMin = Vector2.zero;
                oRT.anchorMax = Vector2.one;
                oRT.offsetMin = Vector2.zero;
                oRT.offsetMax = Vector2.zero;
                oRT.pivot = new Vector2(0.5f, 0.5f);

                var uio = oGo.GetComponent<UIOutline>() ?? oGo.AddComponent<UIOutline>();
                uio._outlineWidth = 4f;
                uio.color = new Color(0.12f, 0.12f, 0.12f, 1f); // Dark contour matching the game
                uio.enabled = true;
                uio.SetVerticesDirty();
                Plugin.Logger.LogInfo($"[LevelSelect] Created AP_Outline with UIOutline for '{btnGo.name}'");
            }

            // 3. Arrow Icon
            if (arrowTransform != null)
            {
                RectTransform aRT = arrowTransform.GetComponent<RectTransform>();
                if (aRT != null)
                {
                    aRT.anchorMin = new Vector2(1f, 0.5f);
                    aRT.anchorMax = new Vector2(1f, 0.5f);
                    aRT.pivot = new Vector2(0.5f, 0.5f);
                    aRT.anchoredPosition = new Vector2(-45f, 0f);
                }
            }
        }

        private static void EnsureApButtons(Button vanillaPlayButton)
        {
            Transform parent = vanillaPlayButton.transform.parent;
            RectTransform playRT = vanillaPlayButton.GetComponent<RectTransform>();

            if (_apButtonsRoot == null && parent != null)
            {
                Transform existing = parent.Find("AP_ButtonsRoot");
                if (existing != null)
                {
                    _apButtonsRoot = existing.gameObject;
                    _apSingleButton = existing.Find("AP_SingleButton")?.GetComponent<Button>();
                    _apResumeButton = existing.Find("AP_ResumeButton")?.GetComponent<Button>();
                    _apNewRunButton = existing.Find("AP_NewRunButton")?.GetComponent<Button>();
                }
            }

            if (_apButtonsRoot == null)
            {
                _apButtonsRoot = new GameObject("AP_ButtonsRoot", typeof(RectTransform));
                _apButtonsRoot.transform.SetParent(parent, false);
                var rootRT = _apButtonsRoot.GetComponent<RectTransform>();
                if (playRT != null)
                {
                    rootRT.anchorMin = playRT.anchorMin;
                    rootRT.anchorMax = playRT.anchorMax;
                    rootRT.pivot = playRT.pivot;
                    rootRT.anchoredPosition = playRT.anchoredPosition;
                    rootRT.sizeDelta = playRT.sizeDelta;
                }
            }

            // 1. Single Action Button (clean clone of vanillaPlayButton)
            if (_apSingleButton == null)
            {
                Transform existingSingle = _apButtonsRoot.transform.Find("AP_SingleButton");
                if (existingSingle != null)
                {
                    _apSingleButton = existingSingle.GetComponent<Button>();
                }
                else
                {
                    GameObject singleGo = UnityEngine.Object.Instantiate(vanillaPlayButton.gameObject, _apButtonsRoot.transform);
                    singleGo.name = "AP_SingleButton";
                    _apSingleButton = singleGo.GetComponent<Button>();
                    var sRT = singleGo.GetComponent<RectTransform>();
                    if (sRT != null)
                    {
                        sRT.anchorMin = new Vector2(0.5f, 0.5f);
                        sRT.anchorMax = new Vector2(0.5f, 0.5f);
                        sRT.pivot = new Vector2(0.5f, 0.5f);
                        sRT.anchoredPosition = Vector2.zero;
                        sRT.sizeDelta = playRT != null ? playRT.sizeDelta : new Vector2(180f, 60f);
                    }
                }
            }

            // Dual button geometry
            float btnWidth = 460f;
            float btnHeight = playRT != null && playRT.sizeDelta.y > 0 ? playRT.sizeDelta.y : 60f;
            float spacing = 25f;
            float leftX = -(btnWidth / 2f + spacing / 2f);
            float rightX = (btnWidth / 2f + spacing / 2f);

            Image vImg = vanillaPlayButton.GetComponent<Image>();
            Sprite vanillaSprite = vImg != null ? vImg.sprite : null!;

            // 2. Dual Resume Button (460px wide, green)
            if (_apResumeButton == null)
            {
                Transform existingResume = _apButtonsRoot.transform.Find("AP_ResumeButton");
                if (existingResume != null)
                {
                    _apResumeButton = existingResume.GetComponent<Button>();
                }
                else
                {
                    GameObject resumeGo = UnityEngine.Object.Instantiate(vanillaPlayButton.gameObject, _apButtonsRoot.transform);
                    resumeGo.name = "AP_ResumeButton";
                    _apResumeButton = resumeGo.GetComponent<Button>();
                    ConfigureApButton(resumeGo, "Resume", isAmber: false, leftX, vanillaSprite, btnWidth, btnHeight);
                }
            }

            // 3. Dual New Run Button (460px wide, amber)
            if (_apNewRunButton == null)
            {
                Transform existingNewRun = _apButtonsRoot.transform.Find("AP_NewRunButton");
                if (existingNewRun != null)
                {
                    _apNewRunButton = existingNewRun.GetComponent<Button>();
                }
                else
                {
                    GameObject newRunGo = UnityEngine.Object.Instantiate(vanillaPlayButton.gameObject, _apButtonsRoot.transform);
                    newRunGo.name = "AP_NewRunButton";
                    _apNewRunButton = newRunGo.GetComponent<Button>();
                    ConfigureApButton(newRunGo, "New Run", isAmber: true, rightX, vanillaSprite, btnWidth, btnHeight);
                }
            }
        }

        public static void OnChangeDetails(LevelSelectScript __instance, int levelID)
        {
            HideApButtons(__instance);
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
            if (TitleLevelSelectPatches.IsApSelected)
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

    [HarmonyPatch(typeof(TitleScreenManager), "Start")]
    public static class TitleScreenManagerStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TitleScreenManager __instance)
        {
            // Reset AP run state on Main Menu: Main menu resume button is strictly for normal games!
            ArchipelagoClient.IsArchipelagoRun = false;
            TitleLevelSelectPatches.SetApSelected(false);
        }
    }

    [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.PlayGame), new Type[] { typeof(int) })]
    public static class TitleScreenPlayGamePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(int numberToPlay)
        {
            Plugin.Logger.LogInfo($"[TitleScreenPlayGamePatch] PlayGame called with rule #{numberToPlay} (IsApSelected: {TitleLevelSelectPatches.IsApSelected}, IsApRun: {ArchipelagoClient.IsArchipelagoRun}, Connected: {Plugin.Instance.Client.IsConnected})");

            // Only intercept if AP is selected in LevelSelect, or explicitly launched as an AP run
            if (TitleLevelSelectPatches.IsApSelected || ArchipelagoClient.IsArchipelagoRun)
            {
                if (!Plugin.Instance.Client.IsConnected)
                {
                    Plugin.Logger.LogWarning("[TitleScreenPlayGamePatch] HARD BLOCKED PlayGame because Archipelago is selected but NOT connected!");
                    return false; // Prevent launch!
                }
                ArchipelagoClient.IsArchipelagoRun = true;
            }
            else
            {
                // Normal game (including normal resume at numberToPlay == 0)
                ArchipelagoClient.IsArchipelagoRun = false;
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
