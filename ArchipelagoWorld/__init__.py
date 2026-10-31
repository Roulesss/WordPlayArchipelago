from typing import Dict, Any, List

from BaseClasses import Region, Entrance, ItemClassification
from worlds.AutoWorld import World, WebWorld
from .Items import (
    WordPlayItem, item_data_table, filler_items, trap_items
)
from .Locations import (
    WordPlayLocation, all_location_data_table, get_locations_for_difficulty
)
from .Options import WordPlayOptions
from .Rules import set_rules
from .perk_tables import modifier_items, tile_change_items, tile_addition_items

class WordPlayWebWorld(WebWorld):
    theme = "party"
    options_presets = {
        "Normal (Recommended)": {
            "difficulty": "normal",
            "wins_required": 1,
            "starting_word_length": 5,
            "trap_chance": 20,
            "death_link": False,
        },
        "Casual (Easy 10-Round)": {
            "difficulty": "easy",
            "wins_required": 1,
            "starting_word_length": 6,
            "trap_chance": 10,
            "death_link": False,
        },
        "Hardcore Marathon": {
            "difficulty": "marathon",
            "wins_required": 1,
            "starting_word_length": 4,
            "trap_chance": 30,
            "death_link": True,
        },
    }

class WordPlayWorld(World):
    """
    Word Play is a rogue-lite word-building deckbuilder where players spell words,
    manage tiles, leverage modifiers, and survive increasingly challenging score targets.
    """
    game = "Word Play"
    options_dataclass = WordPlayOptions
    options: WordPlayOptions
    topology_present = False
    web = WordPlayWebWorld()

    item_name_to_id: Dict[str, int] = {name: data.code for name, data in item_data_table.items()}
    location_name_to_id: Dict[str, int] = {name: data.code for name, data in all_location_data_table.items()}

    def generate_early(self) -> None:
        self.starting_unlocked_modifiers: List[str] = []
        self.starting_unlocked_tile_changes: List[str] = []
        self.starting_unlocked_tile_additions: List[str] = []

        all_mods = list(modifier_items.keys())
        if self.options.lock_modifiers:
            cnt = min(self.options.starting_modifiers.value, len(all_mods))
            self.starting_unlocked_modifiers = list(self.random.sample(all_mods, cnt))

        all_changes = list(tile_change_items.keys())
        if self.options.lock_tile_changes:
            cnt = min(self.options.starting_tile_changes.value, len(all_changes))
            self.starting_unlocked_tile_changes = list(self.random.sample(all_changes, cnt))

        all_additions = list(tile_addition_items.keys())
        if self.options.lock_tile_additions:
            cnt = min(self.options.starting_tile_additions.value, len(all_additions))
            self.starting_unlocked_tile_additions = list(self.random.sample(all_additions, cnt))

    def create_regions(self) -> None:
        menu_region = Region("Menu", self.player, self.multiworld)
        board_region = Region("Game Board", self.player, self.multiworld)

        active_locations = get_locations_for_difficulty(self.options.difficulty.current_key)

        for loc_name, loc_data in active_locations.items():
            location = WordPlayLocation(self.player, loc_name, loc_data.code, board_region)
            board_region.locations.append(location)

        menu_to_board = Entrance(self.player, "Start Game", menu_region)
        menu_to_board.connect(board_region)
        menu_region.exits.append(menu_to_board)

        self.multiworld.regions.append(menu_region)
        self.multiworld.regions.append(board_region)

    def create_items(self) -> None:
        itempool: List[WordPlayItem] = []

        # Add fixed progression & useful items (quantity > 0)
        for name, data in item_data_table.items():
            if data.quantity > 0:
                for _ in range(data.quantity):
                    itempool.append(self.create_item(name))

        # Collect candidate unlock items
        candidate_unlock_items: List[str] = []
        if self.options.lock_modifiers:
            for gm in modifier_items:
                if gm not in self.starting_unlocked_modifiers:
                    candidate_unlock_items.append(modifier_items[gm][0])

        if self.options.lock_tile_changes:
            for gc in tile_change_items:
                if gc not in self.starting_unlocked_tile_changes:
                    candidate_unlock_items.append(tile_change_items[gc][0])

        if self.options.lock_tile_additions:
            for ga in tile_addition_items:
                if ga not in self.starting_unlocked_tile_additions:
                    candidate_unlock_items.append(tile_addition_items[ga][0])

        self.random.shuffle(candidate_unlock_items)

        # Place candidate unlock items up to remaining_slots
        active_locations = get_locations_for_difficulty(self.options.difficulty.current_key)
        total_locations = len(active_locations)
        remaining_slots = total_locations - len(itempool)

        unlocks_to_add = candidate_unlock_items[:remaining_slots]
        for name in unlocks_to_add:
            itempool.append(self.create_item(name))

        # Fill any remaining slots with filler and traps
        remaining_slots = total_locations - len(itempool)
        trap_percentage = self.options.trap_chance.value

        for _ in range(max(0, remaining_slots)):
            if self.random.randint(1, 100) <= trap_percentage:
                chosen_trap = self.random.choice(trap_items)
                itempool.append(self.create_item(chosen_trap))
            else:
                chosen_filler = self.random.choice(filler_items)
                itempool.append(self.create_item(chosen_filler))

        self.multiworld.itempool += itempool

    def set_rules(self) -> None:
        set_rules(self)

    def create_item(self, name: str) -> WordPlayItem:
        data = item_data_table[name]
        return WordPlayItem(name, data.classification, data.code, self.player)

    def fill_slot_data(self) -> Dict[str, Any]:
        return {
            "difficulty": self.options.difficulty.current_key,
            "total_rounds": self.options.difficulty.total_rounds,
            "wins_required": self.options.wins_required.value,
            "death_link": self.options.death_link.value,
            "starting_word_length": self.options.starting_word_length.value,
            "lock_modifiers": bool(self.options.lock_modifiers.value),
            "starting_modifiers": self.options.starting_modifiers.value,
            "lock_tile_changes": bool(self.options.lock_tile_changes.value),
            "starting_tile_changes": self.options.starting_tile_changes.value,
            "lock_tile_additions": bool(self.options.lock_tile_additions.value),
            "starting_tile_additions": self.options.starting_tile_additions.value,
            "starting_unlocked_modifiers": self.starting_unlocked_modifiers,
            "starting_unlocked_tile_changes": self.starting_unlocked_tile_changes,
            "starting_unlocked_tile_additions": self.starting_unlocked_tile_additions,
        }
