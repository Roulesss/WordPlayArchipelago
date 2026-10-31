from typing import Dict, NamedTuple, Tuple
from BaseClasses import Item, ItemClassification
from .perk_tables import modifier_items, tile_change_items, tile_addition_items

class WordPlayItem(Item):
    game: str = "Word Play"

class ItemData(NamedTuple):
    code: int
    classification: ItemClassification
    quantity: int = 1

BASE_ITEM_ID = 91000

item_data_table: Dict[str, ItemData] = {
    # Progression Items
    "Progressive Word Length": ItemData(BASE_ITEM_ID + 1, ItemClassification.progression, 15),
    "Extra Board Slot":        ItemData(BASE_ITEM_ID + 2, ItemClassification.progression, 4),
    "Extra Plays (+2)":        ItemData(BASE_ITEM_ID + 3, ItemClassification.useful, 5),
    "Extra Refreshes (+2)":    ItemData(BASE_ITEM_ID + 4, ItemClassification.useful, 5),
    "Golden Tile Pack":        ItemData(BASE_ITEM_ID + 5, ItemClassification.useful, 3),
    "Diamond Tile Pack":       ItemData(BASE_ITEM_ID + 6, ItemClassification.useful, 3),
    "Emerald Tile Pack":       ItemData(BASE_ITEM_ID + 7, ItemClassification.useful, 2),
    "Dot Tile Pack":           ItemData(BASE_ITEM_ID + 8, ItemClassification.useful, 2),

    # Filler Items
    "+5 Word Score Bonus":     ItemData(BASE_ITEM_ID + 101, ItemClassification.filler, 0),
    "+10 Word Score Bonus":    ItemData(BASE_ITEM_ID + 102, ItemClassification.filler, 0),
    "+1 Play":                 ItemData(BASE_ITEM_ID + 103, ItemClassification.filler, 0),
    "+1 Refresh":              ItemData(BASE_ITEM_ID + 104, ItemClassification.filler, 0),
    "+1 Gold Tile":            ItemData(BASE_ITEM_ID + 105, ItemClassification.filler, 0),
    "+1 Diamond Tile":         ItemData(BASE_ITEM_ID + 106, ItemClassification.filler, 0),

    # Trap Items
    "Lose Play Trap":          ItemData(BASE_ITEM_ID + 201, ItemClassification.trap, 0),
    "Lose Refresh Trap":       ItemData(BASE_ITEM_ID + 202, ItemClassification.trap, 0),
    "Delete Bag Letter Trap":  ItemData(BASE_ITEM_ID + 203, ItemClassification.trap, 0),
    "Bad Letter Slot Trap":    ItemData(BASE_ITEM_ID + 204, ItemClassification.trap, 0),
    "Vowels Zero Score Trap":  ItemData(BASE_ITEM_ID + 205, ItemClassification.trap, 0),
    "Scramble Board Trap":     ItemData(BASE_ITEM_ID + 206, ItemClassification.trap, 0),
    "Screamer Trap":           ItemData(BASE_ITEM_ID + 207, ItemClassification.trap, 0),
}

# Add all individual Modifiers (0 quantity by default, added dynamically when option is enabled)
for game_name, (ap_name, code) in modifier_items.items():
    item_data_table[ap_name] = ItemData(code, ItemClassification.useful, 0)

# Add all individual Tile Changes (Upgrades)
for game_name, (ap_name, code) in tile_change_items.items():
    item_data_table[ap_name] = ItemData(code, ItemClassification.useful, 0)

# Add all individual Tile Additions (Gifts)
for game_name, (ap_name, code) in tile_addition_items.items():
    item_data_table[ap_name] = ItemData(code, ItemClassification.useful, 0)

filler_items = [
    "+5 Word Score Bonus",
    "+10 Word Score Bonus",
    "+1 Play",
    "+1 Refresh",
    "+1 Gold Tile",
    "+1 Diamond Tile",
]

trap_items = [
    "Lose Play Trap",
    "Lose Refresh Trap",
    "Delete Bag Letter Trap",
    "Bad Letter Slot Trap",
    "Vowels Zero Score Trap",
    "Scramble Board Trap",
    "Screamer Trap",
]
