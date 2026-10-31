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

        private Vector2 _fillerScroll = Vector2.zero;
        private Vector2 _trapScroll = Vector2.zero;

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

            // 2. Draw Trap Tester Panel
            GUI.depth = -100;
            float panelWidth = 155f;
            float trapX = Screen.width - panelWidth - 10f;

            if (_showTrapPanel)
            {
                float panelHeight = 270f;
                Rect panelRect = new Rect(trapX, 10f, panelWidth, panelHeight);

                GUI.Box(panelRect, "AP Trap Tester");

                GUILayout.BeginArea(new Rect(panelRect.x + 6f, panelRect.y + 22f, panelWidth - 12f, panelHeight - 26f));
                _trapScroll = GUILayout.BeginScrollView(_trapScroll, false, false);

                if (GUILayout.Button("🔊 Screamer"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Screamer clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.Screamer);
                }

                if (GUILayout.Button("🔤 Vowels 0 Score"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Vowels 0 Score clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.VowelsZeroScore);
                }

                if (GUILayout.Button("🎯 Bad Slot"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Bad Slot clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.BadLetterSlot);
                }

                if (GUILayout.Button("💔 Lose Play"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Lose Play clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.LosePlay);
                }

                if (GUILayout.Button("🔄 Lose Refresh"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Lose Refresh clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.LoseRefresh);
                }

                if (GUILayout.Button("🗑️ Delete Bag Tile"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Delete Bag Tile clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.DeleteBagLetter);
                }

                if (GUILayout.Button("🎲 Scramble Board"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Scramble Board clicked!");
                    TrapsManager.TriggerTrap(TrapsManager.TrapType.ScrambleBoard);
                }

                if (GUILayout.Button("[-] Hide Traps"))
                {
                    _showTrapPanel = false;
                }

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            // 3. Draw Filler / Item Tester Panel
            float fillerX = _showTrapPanel ? (trapX - panelWidth - 8f) : trapX;

            if (_showFillerPanel)
            {
                float panelHeight = 310f;
                Rect fillerRect = new Rect(fillerX, 10f, panelWidth, panelHeight);

                GUI.Box(fillerRect, "AP Filler / Items");

                GUILayout.BeginArea(new Rect(fillerRect.x + 6f, fillerRect.y + 22f, panelWidth - 12f, panelHeight - 26f));
                _fillerScroll = GUILayout.BeginScrollView(_fillerScroll, false, false);

                if (GUILayout.Button("+5 Score Bonus"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +5 Score Bonus clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus5);
                }

                if (GUILayout.Button("+10 Score Bonus"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +10 Score Bonus clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.WordScoreBonus10);
                }

                if (GUILayout.Button("+1 Play"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Play clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.ExtraPlay);
                }

                if (GUILayout.Button("+1 Refresh"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Refresh clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.ExtraRefresh);
                }

                if (GUILayout.Button("+1 Gold Tile"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Gold Tile clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.AddGoldTile);
                }

                if (GUILayout.Button("+1 Diamond Tile"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: +1 Diamond Tile clicked!");
                    FillerManager.GrantFiller(FillerManager.FillerType.AddDiamondTile);
                }

                if (GUILayout.Button("Word Length (+1)"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Progressive Word Length clicked!");
                    ItemManager.HandleReceivedItem(91001, "Progressive Word Length");
                }

                if (GUILayout.Button("Board Slot (+1)"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Extra Board Slot clicked!");
                    ItemManager.HandleReceivedItem(91002, "Extra Board Slot");
                }

                if (GUILayout.Button("Tile Pack: Gold (3x)"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Golden Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91005, "Golden Tile Pack");
                }

                if (GUILayout.Button("Tile Pack: Diamond (3x)"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Diamond Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91006, "Diamond Tile Pack");
                }

                if (GUILayout.Button("Tile Pack: Emerald (2x)"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Emerald Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91007, "Emerald Tile Pack");
                }

                if (GUILayout.Button("Tile Pack: Dot (2x)"))
                {
                    Plugin.Logger?.LogInfo("[ArchipelagoUI] Button: Dot Tile Pack clicked!");
                    ItemManager.HandleReceivedItem(91008, "Dot Tile Pack");
                }

                if (GUILayout.Button("[-] Hide Fillers"))
                {
                    _showFillerPanel = false;
                }

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            // 4. Toggle buttons for collapsed panels
            if (!_showTrapPanel && !_showFillerPanel)
            {
                if (GUI.Button(new Rect(Screen.width - 200f, 10f, 92f, 25f), "[+] Traps"))
                {
                    _showTrapPanel = true;
                }
                if (GUI.Button(new Rect(Screen.width - 102f, 10f, 92f, 25f), "[+] Fillers"))
                {
                    _showFillerPanel = true;
                }
            }
            else if (!_showTrapPanel)
            {
                if (GUI.Button(new Rect(Screen.width - 92f, 10f, 82f, 25f), "[+] Traps"))
                {
                    _showTrapPanel = true;
                }
            }
            else if (!_showFillerPanel)
            {
                if (GUI.Button(new Rect(trapX - 90f, 10f, 82f, 25f), "[+] Fillers"))
                {
                    _showFillerPanel = true;
                }
            }
        }
    }
}
