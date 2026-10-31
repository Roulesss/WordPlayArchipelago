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

        public ArchipelagoClient()
        {
            DeathLink = new DeathLinkHandler(this);
        }

        public async void Connect(string host, int port, string slotName, string password)
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
                Plugin.Logger.LogInfo($"[ArchipelagoClient] WebSocket connected! Server reports version: {roomInfo.Version}. Logging in as '{slotName}'...");

                LoginResult result = await session.LoginAsync(
                    "Word Play",
                    slotName,
                    ItemsHandlingFlags.AllItems,
                    new Version(0, 5, 0),
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

                    ItemManager.ResetRunState();

                    SlotNumber = successful.Slot;

                    // Listen for items
                    session.Items.ItemReceived += OnItemReceived;

                    // Immediately process any items already in the queue from server
                    while (session.Items.Any())
                    {
                        ItemInfo initialItem = session.Items.DequeueItem();
                        long itemId = initialItem.ItemId;
                        string itemName = initialItem.ItemDisplayName;
                        Plugin.Logger.LogInfo($"[ArchipelagoClient] Dequeued initial received item: {itemName} (ID: {itemId})");
                        EnqueueMainThreadAction(() =>
                        {
                            ItemManager.HandleReceivedItem(itemId, itemName);
                        });
                    }

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
                }
                else if (result is LoginFailure failure)
                {
                    IsConnected = false;
                    LastError = string.Join("; ", failure.Errors);
                    Plugin.Logger.LogError($"[ArchipelagoClient] Failed to login to Archipelago: {LastError}");
                    OnConnectionStatusChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                IsConnecting = false;
                IsConnected = false;
                LastError = ex.Message;
                Plugin.Logger.LogError($"[ArchipelagoClient] Exception during Archipelago connection: {ex.Message}");
                OnConnectionStatusChanged?.Invoke();
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
            Plugin.Logger.LogInfo($"Run Won! Progress: {CompletedWins}/{WinsRequired} wins on {Difficulty.ToUpper()}.");

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
            Plugin.Logger.LogInfo($"[ArchipelagoClient] OnItemReceived callback triggered by server!");
            while (helper.Any())
            {
                ItemInfo item = helper.DequeueItem();
                long itemId = item.ItemId;
                string itemName = item.ItemDisplayName;

                Plugin.Logger.LogInfo($"[ArchipelagoClient] Dequeued incoming item: {itemName} (ID: {itemId})");
                EnqueueMainThreadAction(() =>
                {
                    ItemManager.HandleReceivedItem(itemId, itemName);
                });
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
        }
    }
}
