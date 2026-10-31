using UnityEngine;

namespace WordPlayArchipelago.Archipelago
{
    public class FillerManager
    {
        public enum FillerType
        {
            WordScoreBonus5,
            WordScoreBonus10,
            ExtraPlay,
            ExtraRefresh,
            AddGoldTile,
            AddDiamondTile
        }

        // Pending bonus points to add to the next submitted word
        public static int PendingWordBonusPoints { get; set; }

        public static void GrantFiller(FillerType type)
        {
            switch (type)
            {
                case FillerType.WordScoreBonus5:
                    PendingWordBonusPoints += 5;
                    Plugin.Logger.LogInfo("[Filler] +5 bonus points added to next word!");
                    Plugin.Instance.UI.ShowBanner("+5 Word Score Bonus!", new Color(0.4f, 1f, 0.4f), 3f);
                    break;

                case FillerType.WordScoreBonus10:
                    PendingWordBonusPoints += 10;
                    Plugin.Logger.LogInfo("[Filler] +10 bonus points added to next word!");
                    Plugin.Instance.UI.ShowBanner("+10 Word Score Bonus!", new Color(0.4f, 1f, 0.4f), 3f);
                    break;

                case FillerType.ExtraPlay:
                    if (LogicManagerScript.Instance != null)
                    {
                        LogicManagerScript.Instance.AddLives(1);
                        Plugin.Logger.LogInfo("[Filler] +1 Play added!");
                        Plugin.Instance.UI.ShowBanner("+1 Play!", new Color(0.3f, 0.9f, 1f), 3f);
                    }
                    break;

                case FillerType.ExtraRefresh:
                    if (LogicManagerScript.Instance != null)
                    {
                        LogicManagerScript.Instance.ChangeNumberOfRefreshes(1);
                        Plugin.Logger.LogInfo("[Filler] +1 Refresh added!");
                        Plugin.Instance.UI.ShowBanner("+1 Refresh (Discard)!", new Color(0.9f, 0.8f, 0.3f), 3f);
                    }
                    break;

                case FillerType.AddGoldTile:
                    AddSpecialTileToBag(SpecialTileManager.TileTypes.Golden, "Golden Tile");
                    break;

                case FillerType.AddDiamondTile:
                    AddSpecialTileToBag(SpecialTileManager.TileTypes.Diamond, "Diamond Tile");
                    break;
            }
        }

        private static void AddSpecialTileToBag(SpecialTileManager.TileTypes type, string label)
        {
            if (LetterBagManager.Instance == null) return;

            string[] commonLetters = { "A", "E", "I", "O", "R", "S", "T", "L", "N" };
            string chosenLetter = commonLetters[UnityEngine.Random.Range(0, commonLetters.Length)];
            string typeID = SpecialTileManager.Instance.GetTypeID(type);
            string tileCode = $"{chosenLetter}{typeID}0000";

            LetterBagManager.Instance.AddExtraLetters(tileCode, 1, showToaster: false);
            Plugin.Logger.LogInfo($"[Filler] Added {label} '{chosenLetter}' to Letter Bag!");
            Plugin.Instance.UI.ShowBanner($"+1 {label} ({chosenLetter}) added to Bag!", new Color(1f, 0.84f, 0f), 3.5f);
        }
    }
}
