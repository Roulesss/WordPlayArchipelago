using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using UnityEngine;
using Color = UnityEngine.Color;

namespace WordPlayArchipelago.Archipelago
{
    public class ArchipelagoClient
    {
        public ArchipelagoSession? Session { get; private set; }
        public DeathLinkHandler DeathLink { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsConnecting { get; private set; }
        public string? LastError { get; private set; }
        public string SlotName { get; private set; } = "";
        public int SlotNumber { get; private set; } = -1;
        public Dictionary<long, ScoutedItemInfo> ScoutedLocations { get; } = new Dictionary<long, ScoutedItemInfo>();

        // Global Run State
        public static bool IsArchipelagoRun { get; set; } = false;

        public static int LastProcessedItemIndex { get; private set; } = 0;

        public class PendingIncomingItem
        {
            public int ItemIndex;
            public long ItemId;
            public string ItemName = "";
        }

        private static readonly Queue<PendingIncomingItem> _pendingGameItems = new Queue<PendingIncomingItem>();

        public string SeedName { get; private set; } = "";

        private string GetItemIndexPrefKey()
        {
            string seed = !string.IsNullOrEmpty(SeedName) ? SeedName : "UnknownSeed";
            return $"AP_LastItemIndex_{seed}_{SlotName}";
        }

        private string GetCompletedWinsPrefKey()
        {
            string seed = !string.IsNullOrEmpty(SeedName) ? SeedName : "UnknownSeed";
            return $"AP_CompletedWins_{seed}_{SlotName}";
        }

        public bool IsLocationChecked(long locationId) => _checkedLocations.Contains(locationId);

        public event Action? OnConnectionStatusChanged;
        public string Difficulty { get; private set; } = "normal";
        public int DifficultyId => Difficulty switch
        {
            "easy" => 1,
            "normal" => 2,
            "hard" => 3,
            "legendary" => 4,
            "marathon" => 8,
            _ => 2
        };
        public int TotalRoundsForDifficulty { get; private set; } = 12;
        public int WinsRequired { get; private set; } = 1;
        public int CompletedWins { get; private set; } = 0;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private readonly HashSet<long> _checkedLocations = new HashSet<long>();

        private readonly HashSet<int> _processedItemIndices = new HashSet<int>();

        public ArchipelagoClient()
        {
            DeathLink = new DeathLinkHandler(this);
        }

        public async void Connect(string host, int port, string slotName, string password, Action<bool, string?>? onComplete = null)
        {
            if (IsConnecting)
            {
                Plugin.Logger.LogWarning("Connect requested while already connecting.");
                return;
            }

            IsConnecting = true;
            LastError = null;
            SlotName = slotName;
            OnConnectionStatusChanged?.Invoke();

            try
            {
                Plugin.Logger.LogInfo($"[ArchipelagoClient] Connecting to WebSocket at {host}:{port} as '{slotName}'...");

                var session = ArchipelagoSessionFactory.CreateSession(new Uri($"ws://{host}:{port}"));

                var roomInfo = await session.ConnectAsync();
                SeedName = roomInfo.SeedName ?? "";
                Plugin.Logger.LogInfo($"[ArchipelagoClient] WebSocket connected! Server reports version: {roomInfo.Version}, SeedName: {SeedName}. Logging in as '{slotName}'...");

                LoginResult result = await session.LoginAsync(
                    "Word Play",
                    slotName,
                    ItemsHandlingFlags.AllItems,
                    new Version(0, 6, 8),
                    tags: Plugin.Instance.EnableDeathLink.Value ? new[] { "DeathLink" } : Array.Empty<string>(),
                    uuid: null,
                    password: password,
                    requestSlotData: true
                );

                Plugin.Logger.LogInfo($"[ArchipelagoClient] LoginResult received: {result.GetType().Name}");

                IsConnecting = false;

                if (result is LoginSuccessful successful)
                {
                    Session = session;
                    IsConnected = true;
                    LastError = null;
                    Plugin.Logger.LogInfo("Successfully connected and logged into Archipelago!");

                    // Check slot data for Death Link option
                    bool deathLinkOpt = Plugin.Instance.EnableDeathLink.Value;
                    if (successful.SlotData.TryGetValue("death_link", out object? dlVal))
                    {
                        if (dlVal is bool dlBool) deathLinkOpt = dlBool;
                        else if (dlVal is long dlLong) deathLinkOpt = dlLong != 0;
                    }
                    DeathLink.Initialize(deathLinkOpt);

                    // Check slot data for Difficulty & Wins Required
                    if (successful.SlotData.TryGetValue("difficulty", out object? diffVal))
                    {
                        Difficulty = diffVal?.ToString()?.ToLower() ?? "normal";
                    }
                    TotalRoundsForDifficulty = Difficulty switch
                    {
                        "easy" => 10,
                        "normal" => 12,
                        "hard" => 12,
                        "legendary" => 14,
                        "marathon" => 20,
                        _ => 12
                    };

                    if (successful.SlotData.TryGetValue("wins_required", out object? winVal))
                    {
                        if (winVal is long winLong) WinsRequired = (int)winLong;
                        else if (int.TryParse(winVal?.ToString(), out int parsedWins)) WinsRequired = parsedWins;
                    }

                    if (successful.SlotData.TryGetValue("starting_word_length", out object? swlVal))
                    {
                        if (swlVal is long swlLong) ItemManager.StartingWordLength = (int)swlLong;
                        else if (int.TryParse(swlVal?.ToString(), out int parsedSwl)) ItemManager.StartingWordLength = parsedSwl;
                    }

                    UnlockManager.ResetState();

                    if (successful.SlotData.TryGetValue("lock_modifiers", out object? lmVal))
                    {
                        if (lmVal is bool lmBool) UnlockManager.LockModifiers = lmBool;
                        else if (bool.TryParse(lmVal?.ToString(), out bool parsedLm)) UnlockManager.LockModifiers = parsedLm;
                        else if (long.TryParse(lmVal?.ToString(), out long parsedLmLong)) UnlockManager.LockModifiers = parsedLmLong != 0;
                    }
                    if (successful.SlotData.TryGetValue("lock_tile_changes", out object? ltcVal))
                    {
                        if (ltcVal is bool ltcBool) UnlockManager.LockTileChanges = ltcBool;
                        else if (bool.TryParse(ltcVal?.ToString(), out bool parsedLtc)) UnlockManager.LockTileChanges = parsedLtc;
                        else if (long.TryParse(ltcVal?.ToString(), out long parsedLtcLong)) UnlockManager.LockTileChanges = parsedLtcLong != 0;
                    }
                    if (successful.SlotData.TryGetValue("lock_tile_additions", out object? ltaVal))
                    {
                        if (ltaVal is bool ltaBool) UnlockManager.LockTileAdditions = ltaBool;
                        else if (bool.TryParse(ltaVal?.ToString(), out bool parsedLta)) UnlockManager.LockTileAdditions = parsedLta;
                        else if (long.TryParse(ltaVal?.ToString(), out long parsedLtaLong)) UnlockManager.LockTileAdditions = parsedLtaLong != 0;
                    }

                    void ParseStartingList(string key)
                    {
                        if (!successful.SlotData.TryGetValue(key, out object? listObj) || listObj == null) return;
                        if (listObj is Newtonsoft.Json.Linq.JArray jArray)
                        {
                            foreach (var elem in jArray)
                            {
                                string? name = elem.ToString();
                                if (!string.IsNullOrEmpty(name)) UnlockManager.RegisterUnlock(name!);
                            }
                        }
                        else if (listObj is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var elem in enumerable)
                            {
                                string? name = elem?.ToString();
                                if (!string.IsNullOrEmpty(name)) UnlockManager.RegisterUnlock(name!);
                            }
                        }
                    }

                    ParseStartingList("starting_unlocked_modifiers");
                    ParseStartingList("starting_unlocked_tile_changes");
                    ParseStartingList("starting_unlocked_tile_additions");

                    Plugin.Logger.LogInfo($"[ArchipelagoClient] Settings: Difficulty={Difficulty.ToUpper()} ({TotalRoundsForDifficulty} rounds), Wins Required={WinsRequired}, Starting Word Length={ItemManager.StartingWordLength}, LockMods={UnlockManager.LockModifiers}, LockTileChanges={UnlockManager.LockTileChanges}, LockTileAdditions={UnlockManager.LockTileAdditions}. Starting unlocked perks: {UnlockManager.UnlockedBonuses.Count}");

                    string prefKey = GetItemIndexPrefKey();
                    LastProcessedItemIndex = PlayerPrefs.GetInt(prefKey, 0);
                    Plugin.Logger.LogInfo($"[ArchipelagoClient] Loaded LastProcessedItemIndex = {LastProcessedItemIndex} for {prefKey}");

                    string winsPrefKey = GetCompletedWinsPrefKey();
                    CompletedWins = PlayerPrefs.GetInt(winsPrefKey, 0);
                    Plugin.Logger.LogInfo($"[ArchipelagoClient] Loaded CompletedWins = {CompletedWins}/{WinsRequired} from {winsPrefKey}");

                    ItemManager.ResetAllState();
                    _pendingGameItems.Clear();
                    _processedItemIndices.Clear();

                    SlotNumber = successful.Slot;

                    // Listen for items
                    session.Items.ItemReceived += OnItemReceived;

                    // Immediately process all items received from server
                    ProcessAllReceivedItems(session.Items);

                    // Re-sync any previously acquired locations
                    foreach (long loc in session.Locations.AllLocationsChecked)
                    {
                        _checkedLocations.Add(loc);
                    }

                    // Scout active locations for shop cards and round clears
                    try
                    {
                        var validIds = session.Locations.AllLocations.ToArray();
                        Plugin.Logger.LogInfo($"[ArchipelagoClient] Scouting all {validIds.Length} locations for this slot...");
                        session.Locations.ScoutLocationsAsync(validIds).ContinueWith(t =>
                        {
                            if (t.IsCompletedSuccessfully && t.Result != null)
                            {
                                EnqueueMainThreadAction(() =>
                                {
                                    foreach (var kvp in t.Result)
                                    {
                                        ScoutedLocations[kvp.Key] = kvp.Value;
                                    }
                                    Plugin.Logger.LogInfo($"[ArchipelagoClient] Successfully scouted {ScoutedLocations.Count} locations!");
                                    ArchipelagoGameController.Instance?.RefreshShopCardDisplays();
                                });
                            }
                            else if (t.IsFaulted)
                            {
                                Plugin.Logger.LogError($"[ArchipelagoClient] Failed to scout locations: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"[ArchipelagoClient] Error initiating location scout: {ex.Message}");
                    }

                    OnConnectionStatusChanged?.Invoke();
                    onComplete?.Invoke(true, null);
                }
                else if (result is LoginFailure failure)
                {
                    IsConnected = false;
                    LastError = string.Join("; ", failure.Errors);
                    Plugin.Logger.LogError($"[ArchipelagoClient] Failed to login to Archipelago: {LastError}");
                    OnConnectionStatusChanged?.Invoke();
                    onComplete?.Invoke(false, LastError);
                }
            }
            catch (Exception ex)
            {
                IsConnecting = false;
                IsConnected = false;
                LastError = ex.Message;
                Plugin.Logger.LogError($"[ArchipelagoClient] Exception during Archipelago connection: {ex.Message}");
                OnConnectionStatusChanged?.Invoke();
                onComplete?.Invoke(false, LastError);
            }
        }

        public void Disconnect()
        {
            IsConnecting = false;
            if (Session != null && IsConnected)
            {
                Session.Socket.DisconnectAsync();
                IsConnected = false;
                Plugin.Logger.LogInfo("Disconnected from Archipelago.");
                EnqueueMainThreadAction(() => OnConnectionStatusChanged?.Invoke());
            }
        }

        public void CheckLocation(long locationId, string description = "")
        {
            if (!IsConnected || Session == null) return;

            if (_checkedLocations.Contains(locationId)) return;

            _checkedLocations.Add(locationId);
            Session.Locations.CompleteLocationChecksAsync(locationId);
            Plugin.Logger.LogInfo($"Checked location #{locationId} ({description})");
            Plugin.Instance.UI.ShowBanner($"Location Checked: {description}!", new Color(0.3f, 1f, 0.7f), 3f);
        }

        public void ScoutSpecificLocations(long[] locationIds, Action? onComplete = null)
        {
            if (Session == null || !IsConnected) return;

            var toScout = locationIds.Where(id => Session.Locations.AllLocations.Contains(id) && !ScoutedLocations.ContainsKey(id)).ToArray();
            if (toScout.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            Session.Locations.ScoutLocationsAsync(toScout).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    EnqueueMainThreadAction(() =>
                    {
                        foreach (var kvp in t.Result)
                        {
                            ScoutedLocations[kvp.Key] = kvp.Value;
                        }
                        Plugin.Logger.LogInfo($"[ArchipelagoClient] On-demand scout retrieved {t.Result.Count} locations.");
                        onComplete?.Invoke();
                    });
                }
                else if (t.IsFaulted)
                {
                    Plugin.Logger.LogWarning($"[ArchipelagoClient] On-demand scout failed: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}");
                }
            });
        }

        public void RecordRunWin()
        {
            CompletedWins++;
            string winsPrefKey = GetCompletedWinsPrefKey();
            PlayerPrefs.SetInt(winsPrefKey, CompletedWins);
            PlayerPrefs.Save();
            Plugin.Logger.LogInfo($"Run Won! Progress: {CompletedWins}/{WinsRequired} wins on {Difficulty.ToUpper()}. (Saved to {winsPrefKey})");

            if (CompletedWins >= WinsRequired)
            {
                SendGoalCompletion();
            }
            else
            {
                Plugin.Instance.UI.ShowBanner($"VICTORY! ({CompletedWins}/{WinsRequired} wins required for AP Goal)", Color.yellow, 6f);
            }
        }

        public void SendGoalCompletion()
        {
            if (!IsConnected || Session == null) return;

            Plugin.Logger.LogInfo("Sending Goal Completion to Archipelago!");
            Session.SetGoalAchieved();
            Plugin.Instance.UI.ShowBanner("GOAL ACHIEVED! Archipelago Multiworld Completed!", Color.magenta, 8f);
        }

        private void OnItemReceived(ReceivedItemsHelper helper)
        {
            Plugin.Logger.LogInfo($"[ArchipelagoClient] OnItemReceived callback triggered by server! Total items: {helper.AllItemsReceived.Count}");
            ProcessAllReceivedItems(helper);
        }

        private void ProcessAllReceivedItems(IReceivedItemsHelper helper)
        {
            var allItems = helper.AllItemsReceived;
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                HandleIncomingItem(i, item.ItemId, item.ItemDisplayName);
            }
        }

        private void HandleIncomingItem(int itemIndex, long itemId, string itemName)
        {
            if (_processedItemIndices.Contains(itemIndex))
            {
                Plugin.Logger.LogWarning($"[ArchipelagoClient] Duplicate itemIndex #{itemIndex} ('{itemName}') ignored!");
                return;
            }
            _processedItemIndices.Add(itemIndex);

            Plugin.Logger.LogInfo($"[ArchipelagoClient] Incoming item: #{itemIndex} '{itemName}' (ID: {itemId}), LastProcessed={LastProcessedItemIndex}");

            if (itemIndex < LastProcessedItemIndex)
            {
                // This item was already received and processed in a prior session / run!
                // Re-apply permanent progression silently so character stats are restored,
                // but DO NOT show banners, DO NOT trigger traps, DO NOT re-grant consumables!
                EnqueueMainThreadAction(() =>
                {
                    ItemManager.HandleReceivedItem(itemId, itemName, silentReplay: true);
                });
                return;
            }

            // This is a NEW item waiting on the server!
            EnqueueMainThreadAction(() =>
            {
                // If we are currently actively playing the game, execute immediately:
                if (IsArchipelagoRun && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game")
                {
                    ExecuteNewItem(itemIndex, itemId, itemName);
                }
                else
                {
                    // Otherwise, we are in the lobby/menu!
                    // If it is a permanent perk unlock, register it with UnlockManager so shop/rules know about it,
                    // but do NOT show banners or trigger traps/consumables until game starts.
                    if (itemId >= 92000 && itemId <= 94017)
                    {
                        UnlockManager.RegisterUnlock(itemId);
                    }

                    // Queue for execution once the game starts!
                    _pendingGameItems.Enqueue(new PendingIncomingItem
                    {
                        ItemIndex = itemIndex,
                        ItemId = itemId,
                        ItemName = itemName
                    });
                    Plugin.Logger.LogInfo($"[ArchipelagoClient] Queued item #{itemIndex} '{itemName}' until game start.");
                }
            });
        }

        private void ExecuteNewItem(int itemIndex, long itemId, string itemName)
        {
            ItemManager.HandleReceivedItem(itemId, itemName, silentReplay: false);
            if (itemIndex >= LastProcessedItemIndex)
            {
                LastProcessedItemIndex = itemIndex + 1;
                string prefKey = GetItemIndexPrefKey();
                PlayerPrefs.SetInt(prefKey, LastProcessedItemIndex);
                PlayerPrefs.Save();
                Plugin.Logger.LogInfo($"[ArchipelagoClient] Saved LastProcessedItemIndex = {LastProcessedItemIndex}");
            }
            try
            {
                SaveData.Instance?.Save();
            }
            catch { }
        }

        public void ProcessPendingGameItems()
        {
            while (_pendingGameItems.Count > 0)
            {
                var pending = _pendingGameItems.Dequeue();
                ExecuteNewItem(pending.ItemIndex, pending.ItemId, pending.ItemName);
            }
        }

        public void EnqueueMainThreadAction(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        public void Update()
        {
            while (_mainThreadQueue.TryDequeue(out Action? action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error executing main thread action: {ex}");
                }
            }

            if (IsArchipelagoRun && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game")
            {
                ProcessPendingGameItems();
            }
        }
    }
}
