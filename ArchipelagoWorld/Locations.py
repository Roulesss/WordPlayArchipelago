from typing import Dict, NamedTuple, List
from BaseClasses import Location

class WordPlayLocation(Location):
    game: str = "Word Play"

class LocationData(NamedTuple):
    code: int

BASE_LOCATION_ID = 91000

# Complete Master Location Table (All possible locations across all gamemodes)
all_location_data_table: Dict[str, LocationData] = {}

# 1. Round Clears (Rounds 1 to 20)
for r in range(1, 21):
    all_location_data_table[f"Round {r} Clear"] = LocationData(BASE_LOCATION_ID + r)

# 2. Shop Draft Purchases (Rounds 1 to 19, 3 cards each)
for r in range(1, 20):
    for c in range(1, 4):
        code = BASE_LOCATION_ID + 100 + ((r - 1) * 3) + c
        all_location_data_table[f"Round Shop Choice #{((r - 1) * 3) + c}"] = LocationData(code)

# 3. Word Score Milestones (Points on a single word)
word_score_milestones = [
    ("Word Score: 30 Points", BASE_LOCATION_ID + 201),
    ("Word Score: 50 Points", BASE_LOCATION_ID + 202),
    ("Word Score: 80 Points", BASE_LOCATION_ID + 203),
    ("Word Score: 150 Points", BASE_LOCATION_ID + 204),
    ("Word Score: 300 Points", BASE_LOCATION_ID + 205),
    ("Word Score: 500 Points", BASE_LOCATION_ID + 206),
    ("Word Score: 650 Points", BASE_LOCATION_ID + 207),
    ("Word Score: 800 Points", BASE_LOCATION_ID + 208),
    ("Word Score: 1000 Points", BASE_LOCATION_ID + 209),
]
for name, code in word_score_milestones:
    all_location_data_table[name] = LocationData(code)

# 4. Run Score Milestones (Cumulative score in a single run)
run_score_milestones = [
    ("Run Score: 250 Points", BASE_LOCATION_ID + 301),
    ("Run Score: 500 Points", BASE_LOCATION_ID + 302),
    ("Run Score: 1000 Points", BASE_LOCATION_ID + 303),
    ("Run Score: 1500 Points", BASE_LOCATION_ID + 304),
    ("Run Score: 2000 Points", BASE_LOCATION_ID + 305),
    ("Run Score: 3000 Points", BASE_LOCATION_ID + 306),
    ("Run Score: 4000 Points", BASE_LOCATION_ID + 307),
    ("Run Score: 5000 Points", BASE_LOCATION_ID + 308),
    ("Run Score: 7500 Points", BASE_LOCATION_ID + 309),
]
for name, code in run_score_milestones:
    all_location_data_table[name] = LocationData(code)


def get_locations_for_difficulty(difficulty_name: str) -> Dict[str, LocationData]:
    """Returns the subset of active locations for a specific game difficulty."""
    diff = difficulty_name.lower()

    if diff == "easy":
        rounds = 10
        word_count = 6   # up to 500
        run_count = 6    # up to 3000
    elif diff in ("normal", "hard"):
        rounds = 12
        word_count = 7   # up to 650
        run_count = 7    # up to 4000
    elif diff == "legendary":
        rounds = 14
        word_count = 8   # up to 800
        run_count = 8    # up to 5000
    elif diff == "marathon":
        rounds = 20
        word_count = 9   # up to 1000
        run_count = 9    # up to 7500
    else:
        rounds = 12
        word_count = 7
        run_count = 7

    active_locations: Dict[str, LocationData] = {}

    # Round Clears
    for r in range(1, rounds + 1):
        name = f"Round {r} Clear"
        active_locations[name] = all_location_data_table[name]

    # Shop Purchases (Rounds 1 to rounds - 1)
    for r in range(1, rounds):
        for c in range(1, 4):
            name = f"Round Shop Choice #{((r - 1) * 3) + c}"
            active_locations[name] = all_location_data_table[name]

    # Word Score Milestones
    for i in range(word_count):
        name = word_score_milestones[i][0]
        active_locations[name] = all_location_data_table[name]

    # Run Score Milestones
    for i in range(run_count):
        name = run_score_milestones[i][0]
        active_locations[name] = all_location_data_table[name]

    return active_locations
