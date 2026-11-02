using System;
using UnityEngine;
using WordPlayArchipelago.Patches;

namespace WordPlayArchipelago.Archipelago
{
    public class ArchipelagoUI : MonoBehaviour
    {
        private float _pollTimer = 0f;
        private bool _showTrapPanel = true;
        private bool _showFillerPanel = true;

        private static Texture2D? _redTexture;
        private static Texture2D? _panelBgTexture;

        private static Texture2D MakeSolidTexture(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private string? _bannerText;
        private Color _bannerColor = Color.white;
        private float _bannerTimer = 0f;

        private void Awake()
        {
            Plugin.Logger?.LogInfo("[ArchipelagoUI] Initialized.");
        }

        private void Update()
        {
            // Active poller: detect when LevelSelectScript appears and ensure AP tile is spawned
            _pollTimer += Time.deltaTime;
            if (_pollTimer >= 0.5f)
            {
                _pollTimer = 0f;
                var allLS = FindObjectsOfType<LevelSelectScript>();
                foreach (var ls in allLS)
                {
                    if (ls != null && ls.gameObject.activeInHierarchy && (TitleLevelSelectPatches.ApTileGameObject == null || !TitleLevelSelectPatches.ApTileGameObject.activeInHierarchy))
                    {
                        Plugin.Logger?.LogInfo($"[ArchipelagoUI] Active LevelSelectScript '{ls.name}' found by poller! Triggering AP tile setup...");
                        TitleLevelSelectPatches.OnLevelSelectStart(ls);
                    }
                }
            }

            // Banner timer countdown
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
                if (_bannerTimer <= 0f)
                {
                    _bannerText = null;
                }
            }
        }

        public void ShowBanner(string message, Color color, float duration = 4f)
        {
            Plugin.Logger?.LogInfo($"[Archipelago Notification] {message}");
            _bannerText = message;
            _bannerColor = color;
            _bannerTimer = duration;
        }

        private void OnGUI()
        {
            // 1. Draw Notification Banner
            if (!string.IsNullOrEmpty(_bannerText) && _bannerTimer > 0f)
            {
                GUI.depth = -500;
                float bannerWidth = 480f;
                float bannerHeight = 44f;
                Rect bannerRect = new Rect((Screen.width - bannerWidth) / 2f, 20f, bannerWidth, bannerHeight);

                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = _bannerColor;
                GUI.Box(bannerRect, GUIContent.none);
                GUI.backgroundColor = oldBg;

                var bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                bannerStyle.normal.textColor = Color.white;
                GUI.Label(bannerRect, _bannerText, bannerStyle);
            }

            // 1b. Draw Active AP Effects HUD (Badges for active traps/bonuses during run)
            if (ArchipelagoClient.IsArchipelagoRun && LogicManagerScript.Instance != null && SubmissionScript.Instance != null)
            {
                float badgeX = 16f;
                float badgeY = 16f;
                float badgeWidth = 310f;
                float badgeHeight = 28f;
                float spacing = 6f;

                if (TrapsManager.BadSlotIndex >= 0)
                {
                    Rect r = new Rect(badgeX, badgeY, badgeWidth, badgeHeight);
                    Color oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.55f, 0.12f, 0.12f, 0.92f);
                    GUI.Box(r, GUIContent.none);
                    GUI.backgroundColor = oldBg;

                    var style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(8, 8, 0, 0)
                    };
                    style.normal.textColor = new Color(1f, 0.5f, 0.4f);
                    GUI.Label(r, $"☠ Trap: Letter Slot #{TrapsManager.BadSlotIndex + 1} Scores 0 Pts!", style);
                    badgeY += badgeHeight + spacing;
                }

                if (TrapsManager.VowelsScoreZeroActive)
                {
                    Rect r = new Rect(badgeX, badgeY, badgeWidth, badgeHeight);
                    Color oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.55f, 0.12f, 0.12f, 0.92f);
                    GUI.Box(r, GUIContent.none);
                    GUI.backgroundColor = oldBg;

                    var style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(8, 8, 0, 0)
                    };
                    style.normal.textColor = new Color(1f, 0.5f, 0.4f);
                    GUI.Label(r, "☠ Trap: All Vowels Score 0 Points!", style);
                    badgeY += badgeHeight + spacing;
                }

                // Next Word Bonus notice - Bottom Right just above 'Plays'
                if (FillerManager.PendingWordBonusPoints > 0)
                {
                    float bonusWidth = 230f;
                    float bonusHeight = 32f;
                    float bonusX = Screen.width - bonusWidth - 25f;
                    float bonusY = Screen.height - bonusHeight - 100f; // fallback

                    var lifeBar = UnityEngine.Object.FindObjectOfType<LifeBarScript>();
                    if (lifeBar != null)
                    {
                        var rt = lifeBar.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            Vector3[] corners = new Vector3[4];
                            rt.GetWorldCorners(corners);
                            Canvas canvas = lifeBar.GetComponentInParent<Canvas>();
                            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
                            Vector2 screenTopLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
                            Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

                            float barGuiTop = Screen.height - screenTopLeft.y;
                            float barGuiRight = screenTopRight.x;

                            bonusX = barGuiRight - bonusWidth;
                            bonusY = barGuiTop - bonusHeight - 6f;
                        }
                    }

                    Rect bonusRect = new Rect(bonusX, bonusY, bonusWidth, bonusHeight);

                    if (_redTexture == null)
                    {
                        _redTexture = MakeSolidTexture(1, 1, new Color(0.85f, 0.12f, 0.12f, 0.96f));
                    }
                    GUI.DrawTexture(bonusRect, _redTexture);

                    Color oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 1f);
                    GUI.Box(bonusRect, GUIContent.none);
                    GUI.backgroundColor = oldBg;

                    var bonusStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 16,
                        fontStyle = FontStyle.Bold
                    };
                    bonusStyle.normal.textColor = Color.white;
                    GUI.Label(bonusRect, $"Next Word Bonus: +{FillerManager.PendingWordBonusPoints}", bonusStyle);
                }
            }

            // 2. Traps & Fillers panels in fixed positions
            GUI.depth = -100;
            float panelWidth = 180f;
            float trapX = Screen.width - panelWidth - 12f;
            float fillerX = trapX - panelWidth - 10f;

            if (_panelBgTexture == null)
            {
                _panelBgTexture = MakeSolidTexture(1, 1, new Color(0.12f, 0.12f, 0.15f, 0.96f));
            }

            // AP Traps Panel
            if (_showTrapPanel)
            {
                float panelHeight = 245f;
                Rect panelRect = new Rect(trapX, 10f, panelWidth, panelHeight);

                GUI.DrawTexture(panelRect, _panelBgTexture);
                GUI.Box(panelRect, "AP Traps");

                GUILayout.BeginArea(new Rect(panelRect.x + 6f, panelRect.y + 24f, panelWidth - 12f, panelHeight - 28f));

                if (GUILayout.Button("🔊 Screamer", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Screamer clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.Screamer);
                }

                if (GUILayout.Button("🔤 Vowels 0 Score", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Vowels 0 Score clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.VowelsZeroScore);
                }

                if (GUILayout.Button("🎯 Bad Slot", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Bad Slot clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.BadLetterSlot);
                }

                if (GUILayout.Button("💔 Lose Play", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Lose Play clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.LosePlay);
                }

                if (GUILayout.Button("🔄 Lose Refresh", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Lose Refresh clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.LoseRefresh);
                }

                if (GUILayout.Button("🗑️ Delete Bag Tile", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Delete Bag Tile clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.DeleteBagLetter);
                }

                if (GUILayout.Button("🎲 Scramble Board", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Scramble Board clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.ScrambleBoard);
                }

                if (GUILayout.Button("[-] Hide Traps", GUILayout.Height(22f)))
                {
                    _showTrapPanel = false;
                }

                GUILayout.EndArea();
            }
            else
            {
                if (GUI.Button(new Rect(trapX, 10f, panelWidth, 26f), "[+] AP Traps"))
                {
                    _showTrapPanel = true;
                }
            }

            // AP Filler Items Panel
            if (_showFillerPanel)
            {
                float panelHeight = 375f;
                Rect fillerRect = new Rect(fillerX, 10f, panelWidth, panelHeight);

                GUI.DrawTexture(fillerRect, _panelBgTexture);
                GUI.Box(fillerRect, "AP Filler items");

                GUILayout.BeginArea(new Rect(fillerRect.x + 6f, fillerRect.y + 24f, panelWidth - 12f, panelHeight - 28f));

                if (GUILayout.Button("+5 Score Bonus", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +5 Score Bonus clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus5);
                }

                if (GUILayout.Button("+10 Score Bonus", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +10 Score Bonus clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus10);
                }

                if (GUILayout.Button("+1 Play", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Play clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.ExtraPlay);
                }

                if (GUILayout.Button("+1 Refresh", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Refresh clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.ExtraRefresh);
                }

                if (GUILayout.Button("+1 Gold Tile", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Gold Tile clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.AddGoldTile);
                }

                if (GUILayout.Button("+1 Diamond Tile", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Diamond Tile clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.AddDiamondTile);
                }

                if (GUILayout.Button("Word Length (+1)", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Progressive Word Length clicked!");
                    ItemManager.HandleReceivedItem(91001, "Progressive Word Length");
                }

                if (GUILayout.Button("Board Slot (+1)", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Extra Board Slot clicked!");
                    ItemManager.HandleReceivedItem(91002, "Extra Board Slot");
                }

                if (GUILayout.Button("Tile Pack: Gold (3x)", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Golden Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91005, "Golden Tile Pack");
                }

                if (GUILayout.Button("Tile Pack: Diamond (3x)", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Diamond Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91006, "Diamond Tile Pack");
                }

                if (GUILayout.Button("Tile Pack: Emerald (2x)", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Emerald Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91007, "Emerald Tile Pack");
                }

                if (GUILayout.Button("Tile Pack: Dot (2x)", GUILayout.Height(22f)))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Dot Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91008, "Dot Tile Pack");
                }

                if (GUILayout.Button("[-] Hide Fillers", GUILayout.Height(22f)))
                {
                    _showFillerPanel = false;
                }

                GUILayout.EndArea();
            }
            else
            {
                if (GUI.Button(new Rect(fillerX, 10f, panelWidth, 26f), "[+] AP Filler items"))
                {
                    _showFillerPanel = true;
                }
            }
        }
    }
}
