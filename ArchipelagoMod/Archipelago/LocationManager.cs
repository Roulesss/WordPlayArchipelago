using System;
using System.Collections.Generic;
using System.Linq;

namespace WordPlayArchipelago.Archipelago
{
    public class LocationManager
    {
        public const long BaseLocationId = 91000;

        // Currently assigned shop check locations for the active shop draft [card 0, 1, 2]
        public static long[] CurrentShopLocations { get; } = new long[3];

        // Tracks locations shown in recent shops to avoid immediate repetition on reroll/retry
        private static readonly HashSet<long> PreviousShopLocations = new HashSet<long>();
        private static int _runCounter = 0;
        private static readonly System.Random _rng = new System.Random();

        // Round completion location ID (supports up to Marathon 20 rounds)
        public static long GetRoundClearLocationId(int round)
        {
            return BaseLocationId + round; // 91001 - 91020
        }

        // Shop card purchase location ID (round 1 to 19, card 0 to 2)
        public static long GetShopPurchaseLocationId(int round, int cardIndex)
        {
            return BaseLocationId + 100 + ((round - 1) * 3) + (cardIndex + 1); // 91101 - 91157
        }

        public static bool IsShopLocation(long locationId)
        {
            return locationId >= 91101 && locationId <= 91157;
        }

        public static string? GetLocationName(long locationId)
        {
            if (locationId >= 91001 && locationId <= 91020)
            {
                return $"Round {locationId - 91000} Clear";
            }
            if (locationId >= 91101 && locationId <= 91157)
            {
                return "Round Shop Choice";
            }
            foreach (var kvp in WordScoreMilestones)
            {
                if (kvp.Value == locationId) return $"Word Score: {kvp.Key} Points";
            }
            foreach (var kvp in RunScoreMilestones)
            {
                if (kvp.Value == locationId) return $"Run Score: {kvp.Key} Points";
            }
            return null;
        }

        public static void OnNewRun()
        {
            _runCounter++;
            for (int i = 0; i < CurrentShopLocations.Length; i++)
            {
                CurrentShopLocations[i] = 0;
            }
            Plugin.Logger?.LogInfo($"[LocationManager] New Run #{_runCounter} started. Shop checks rotated.");
        }

        public static void RestoreShopLocations(IList<long> locations)
        {
            if (locations == null) return;
            for (int i = 0; i < CurrentShopLocations.Length; i++)
            {
                CurrentShopLocations[i] = i < locations.Count ? locations[i] : 0;
            }
            Plugin.Logger?.LogInfo($"[LocationManager] Restored shop locations: [{string.Join(", ", CurrentShopLocations)}]");
        }

        public static long GetCurrentShopLocation(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= CurrentShopLocations.Length)
            {
                return 0;
            }

            if (CurrentShopLocations[cardIndex] <= 0)
            {
                RollShopLocations(false, 3);
            }

            return CurrentShopLocations[cardIndex];
        }

        public static List<long> GetActiveShopLocationIds()
        {
            var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
            if (client?.Session != null)
            {
                var valid = client.Session.Locations.AllLocations
                    .Where(IsShopLocation)
                    .OrderBy(id => id)
                    .ToList();
                if (valid.Count > 0) return valid;
            }

            int totalRounds = client?.TotalRoundsForDifficulty ?? 12;
            var list = new List<long>();
            for (int r = 1; r < totalRounds; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    list.Add(GetShopPurchaseLocationId(r, c));
                }
            }
            return list;
        }

        public static void RollShopLocations(bool reroll, int count = 3)
        {
            var client = Plugin.StaticClient ?? Plugin.Instance?.Client;
            var allShop = GetActiveShopLocationIds();
            if (allShop.Count == 0) return;

            var uncheckedLocations = allShop.Where(id => client == null || !client.IsLocationChecked(id)).ToList();
            var checkedLocations = allShop.Where(id => client != null && client.IsLocationChecked(id)).ToList();

            var currentlyDisplayed = new HashSet<long>();
            foreach (var loc in CurrentShopLocations)
            {
                if (loc > 0) currentlyDisplayed.Add(loc);
            }

            // On reroll: avoid currentlyDisplayed.
            // On new round or new run (reroll == false): avoid PreviousShopLocations if possible.
            var toAvoid = reroll ? currentlyDisplayed : (PreviousShopLocations.Count > 0 ? PreviousShopLocations : currentlyDisplayed);

            // Use deterministic RNG for new round shops so that same seed + round produces identical locations
            System.Random rng;
            if (!reroll && LogicManagerScript.Instance != null)
            {
                int round = LogicManagerScript.Instance.currentRound;
                int seed = LogicManagerScript.Instance.gameSeed ^ (round * 10007);
                rng = new System.Random(seed);
            }
            else
            {
                rng = _rng;
            }

            List<long> picked = new List<long>();

            // 1. Fresh unchecked locations (not in toAvoid)
            var freshUnchecked = uncheckedLocations.Where(id => !toAvoid.Contains(id)).ToList();
            Shuffle(freshUnchecked, rng);
            foreach (var loc in freshUnchecked)
            {
                if (picked.Count >= count) break;
                picked.Add(loc);
            }

            // 2. If more are needed, take from remaining unchecked locations
            if (picked.Count < count && uncheckedLocations.Count > picked.Count)
            {
                var remainingUnchecked = uncheckedLocations.Where(id => !picked.Contains(id)).ToList();
                Shuffle(remainingUnchecked, rng);
                foreach (var loc in remainingUnchecked)
                {
                    if (picked.Count >= count) break;
                    picked.Add(loc);
                }
            }

            // 3. If unchecked locations are exhausted, fill from checked locations (avoiding previous if possible)
            if (picked.Count < count && checkedLocations.Count > 0)
            {
                var freshChecked = checkedLocations.Where(id => !toAvoid.Contains(id) && !picked.Contains(id)).ToList();
                Shuffle(freshChecked, rng);
                foreach (var loc in freshChecked)
                {
                    if (picked.Count >= count) break;
                    picked.Add(loc);
                }

                if (picked.Count < count)
                {
                    var remainingChecked = checkedLocations.Where(id => !picked.Contains(id)).ToList();
                    Shuffle(remainingChecked, rng);
                    foreach (var loc in remainingChecked)
                    {
                        if (picked.Count >= count) break;
                        picked.Add(loc);
                    }
                }
            }

            // 4. Update PreviousShopLocations and CurrentShopLocations
            PreviousShopLocations.Clear();
            foreach (var loc in currentlyDisplayed)
            {
                PreviousShopLocations.Add(loc);
            }

            for (int i = 0; i < CurrentShopLocations.Length; i++)
            {
                CurrentShopLocations[i] = i < picked.Count ? picked[i] : (allShop.Count > i ? allShop[i] : 0);
                PreviousShopLocations.Add(CurrentShopLocations[i]);
            }

            Plugin.Logger?.LogInfo($"[LocationManager] Rolled shop checks (reroll: {reroll}): [{string.Join(", ", CurrentShopLocations.Select(id => $"{id} ({GetLocationName(id)})"))}]. Remaining unchecked: {uncheckedLocations.Count}");

            // Proactively scout any unscouted picked locations
            if (client != null && client.IsConnected)
            {
                var unscouted = CurrentShopLocations.Where(id => id > 0 && !client.ScoutedLocations.ContainsKey(id)).ToArray();
                if (unscouted.Length > 0)
                {
                    client.ScoutSpecificLocations(unscouted);
                }
            }
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        // Word Score Milestones (points on a single word)
        public static readonly Dictionary<int, long> WordScoreMilestones = new Dictionary<int, long>
        {
            { 30,   BaseLocationId + 201 },
            { 50,   BaseLocationId + 202 },
            { 80,   BaseLocationId + 203 },
            { 150,  BaseLocationId + 204 },
            { 300,  BaseLocationId + 205 },
            { 500,  BaseLocationId + 206 },
            { 650,  BaseLocationId + 207 },
            { 800,  BaseLocationId + 208 },
            { 1000, BaseLocationId + 209 }
        };

        // Run Score Milestones (cumulative score in a run)
        public static readonly Dictionary<int, long> RunScoreMilestones = new Dictionary<int, long>
        {
            { 250,  BaseLocationId + 301 },
            { 500,  BaseLocationId + 302 },
            { 1000, BaseLocationId + 303 },
            { 1500, BaseLocationId + 304 },
            { 2000, BaseLocationId + 305 },
            { 3000, BaseLocationId + 306 },
            { 4000, BaseLocationId + 307 },
            { 5000, BaseLocationId + 308 },
            { 7500, BaseLocationId + 309 }
        };

        public static List<long> GetAllLocationIds(int rounds = 20)
        {
            var list = new List<long>();
            for (int r = 1; r <= rounds; r++)
            {
                list.Add(GetRoundClearLocationId(r));
            }
            for (int r = 1; r < rounds; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    list.Add(GetShopPurchaseLocationId(r, c));
                }
            }
            foreach (var id in WordScoreMilestones.Values) list.Add(id);
            foreach (var id in RunScoreMilestones.Values) list.Add(id);
            return list;
        }
    }
}
