from dataclasses import dataclass
from Options import Choice, Range, Toggle, DeathLink, PerGameCommonOptions

class Difficulty(Choice):
    """
    Choose the game mode difficulty. Each difficulty has a different number of rounds and locations:
    - Easy: 10 Rounds
    - Normal: 12 Rounds
    - Hard: 12 Rounds
    - Legendary: 14 Rounds
    - Marathon: 20 Rounds
    """
    display_name = "Difficulty Game Mode"
    option_easy = 0
    option_normal = 1
    option_hard = 2
    option_legendary = 3
    option_marathon = 4
    default = 1

    @property
    def total_rounds(self) -> int:
        return {
            self.option_easy: 10,
            self.option_normal: 12,
            self.option_hard: 12,
            self.option_legendary: 14,
            self.option_marathon: 20,
        }[self.value]

class WinsRequired(Range):
    """
    Number of completed run victories on the chosen difficulty required to win the Archipelago multiworld.
    """
    display_name = "Wins Required for Goal"
    range_start = 1
    range_end = 10
    default = 1

class StartingWordLength(Range):
    """The maximum word length the player starts with before collecting Progressive Word Length items."""
    display_name = "Starting Word Length"
    range_start = 4
    range_end = 8
    default = 5

class TrapChance(Range):
    """Percentage of filler items that are converted into traps."""
    display_name = "Trap Chance"
    range_start = 0
    range_end = 100
    default = 20

class LockModifiers(Toggle):
    """
    If enabled, all modifiers are individually locked behind Archipelago checks.
    The shop will only offer modifiers that you have unlocked.
    """
    display_name = "Lock Modifiers"
    default = False

class StartingModifiers(Range):
    """
    If Lock Modifiers is enabled, the number of random modifiers you start with unlocked (0 to 118).
    """
    display_name = "Starting Modifiers Count"
    range_start = 0
    range_end = 118
    default = 0

class LockTileChanges(Toggle):
    """
    If enabled, all tile changes (upgrades) are individually locked behind Archipelago checks.
    The shop will only offer tile changes that you have unlocked.
    """
    display_name = "Lock Tile Changes"
    default = False

class StartingTileChanges(Range):
    """
    If Lock Tile Changes is enabled, the number of random tile changes you start with unlocked (0 to 27).
    """
    display_name = "Starting Tile Changes Count"
    range_start = 0
    range_end = 27
    default = 0

class LockTileAdditions(Toggle):
    """
    If enabled, all tile additions (gifts) are individually locked behind Archipelago checks.
    The shop will only offer tile additions that you have unlocked.
    """
    display_name = "Lock Tile Additions"
    default = False

class StartingTileAdditions(Range):
    """
    If Lock Tile Additions is enabled, the number of random tile additions you start with unlocked (0 to 18).
    """
    display_name = "Starting Tile Additions Count"
    range_start = 0
    range_end = 18
    default = 0

@dataclass
class WordPlayOptions(PerGameCommonOptions):
    difficulty: Difficulty
    wins_required: WinsRequired
    starting_word_length: StartingWordLength
    trap_chance: TrapChance
    death_link: DeathLink
    lock_modifiers: LockModifiers
    starting_modifiers: StartingModifiers
    lock_tile_changes: LockTileChanges
    starting_tile_changes: StartingTileChanges
    lock_tile_additions: LockTileAdditions
    starting_tile_additions: StartingTileAdditions
