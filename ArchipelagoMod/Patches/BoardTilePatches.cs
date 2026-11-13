using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using WordPlayArchipelago.Archipelago;

namespace WordPlayArchipelago.Patches
{
    public static class BoardTilePatches
    {
        public const int MaxBoardTiles = 19;

        [HarmonyPatch(typeof(ToolsScript), nameof(ToolsScript.AddBonusTile))]
        public static class ToolsScriptAddBonusTilePatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (GridManagerScript.Instance != null && GridManagerScript.Instance.letterSpaces != null)
                {
                    if (GridManagerScript.Instance.letterSpaces.Count >= MaxBoardTiles)
                    {
                        Plugin.Logger?.LogWarning($"[BoardTileCap] Prevented adding board tile: already at maximum limit ({GridManagerScript.Instance.letterSpaces.Count}/{MaxBoardTiles})");
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GridManagerScript), nameof(GridManagerScript.AddBonusTile))]
        public static class GridManagerAddBonusTilePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(TileHolderScript toolHolder)
            {
                if (GridManagerScript.Instance != null && GridManagerScript.Instance.letterSpaces != null)
                {
                    if (GridManagerScript.Instance.letterSpaces.Count >= MaxBoardTiles)
                    {
                        Plugin.Logger?.LogWarning($"[BoardTileCap] GridManagerScript.AddBonusTile prevented: already at limit ({GridManagerScript.Instance.letterSpaces.Count}/{MaxBoardTiles})");
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(uBonusTile), nameof(uBonusTile.OnBonusPurchased))]
        public static class UBonusTilePurchasedPatch
        {
            private static readonly ConditionalWeakTable<uBonusTile, StrongBox<int>> AddedSlotsMap =
                new ConditionalWeakTable<uBonusTile, StrongBox<int>>();

            public static int GetAddedSlots(uBonusTile instance)
            {
                return AddedSlotsMap.TryGetValue(instance, out var box) ? box.Value : 0;
            }

            [HarmonyPrefix]
            public static bool Prefix(uBonusTile __instance)
            {
                var box = AddedSlotsMap.GetOrCreateValue(__instance);
                box.Value = 0;

                int numberToAdd = Traverse.Create(__instance).Field("numberToAdd").GetValue<int>();
                if (numberToAdd <= 0) numberToAdd = 1;

                for (int i = 0; i < numberToAdd; i++)
                {
                    if (GridManagerScript.Instance != null && GridManagerScript.Instance.letterSpaces != null &&
                        GridManagerScript.Instance.letterSpaces.Count >= MaxBoardTiles)
                    {
                        Plugin.Logger?.LogWarning($"[uBonusTile] Reached glass limit of {MaxBoardTiles} board tiles. Tile not added.");
                        continue;
                    }

                    if (ToolsScript.Instance != null && ToolsScript.Instance.HowManyAvailableSlots() > 0)
                    {
                        ToolsScript.Instance.AddBonusTile();
                        ToolsScript.Instance.permaSlots++;
                        box.Value++;
                    }
                }
                return false; // Skip original method to avoid desync
            }
        }

        [HarmonyPatch(typeof(uBonusTile), nameof(uBonusTile.OnBonusSold))]
        public static class UBonusTileSoldPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(uBonusTile __instance)
            {
                int added = UBonusTilePurchasedPatch.GetAddedSlots(__instance);
                if (added <= 0)
                {
                    Plugin.Logger?.LogInfo("[uBonusTile] Sold bonus tile that provided 0 slots due to 19-tile cap. Skipping removal.");
                    return false;
                }

                if (ToolsScript.Instance != null)
                {
                    if (ToolsScript.Instance.FirstRoundExtraTileInEffect)
                    {
                        for (int i = 0; i < added; i++)
                        {
                            ToolsScript.Instance.permaSlots--;
                        }
                        return false;
                    }

                    for (int j = 0; j < added; j++)
                    {
                        int toolSlot = ToolsScript.Instance.FindLastFilledSlot();
                        if (toolSlot >= 0)
                        {
                            ToolsScript.Instance.RemoveBonusTile(toolSlot);
                            ToolsScript.Instance.permaSlots--;
                        }
                    }
                }
                return false;
            }
        }
    }
}
