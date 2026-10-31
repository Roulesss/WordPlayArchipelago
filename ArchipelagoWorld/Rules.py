from BaseClasses import CollectionState
from worlds.generic.Rules import set_rule

def set_rules(world) -> None:
    player = world.player
    total_rounds = world.options.difficulty.total_rounds

    def set_location_rule(loc_name: str, rule):
        try:
            loc = world.get_location(loc_name)
            set_rule(loc, rule)
        except KeyError:
            pass

    # Progressive requirements for rounds (requiring longer words for later rounds)
    round_reqs = [
        (4, 1),
        (6, 2),
        (8, 3),
        (10, 4),
        (12, 5),
        (14, 6),
        (18, 7),
    ]
    for r, req in round_reqs:
        if total_rounds >= r:
            set_location_rule(f"Round {r} Clear", lambda state, req=req: state.has("Progressive Word Length", player, req))

    # Single-Word Score Milestones requirements
    word_reqs = [
        ("Word Score: 80 Points", 1),
        ("Word Score: 150 Points", 2),
        ("Word Score: 300 Points", 3),
        ("Word Score: 500 Points", 4),
        ("Word Score: 650 Points", 5),
        ("Word Score: 800 Points", 6),
        ("Word Score: 1000 Points", 7),
    ]
    for loc_name, req in word_reqs:
        set_location_rule(loc_name, lambda state, req=req: state.has("Progressive Word Length", player, req))

    # Goal Rule: Reaching the victory round of the chosen difficulty
    final_round_loc = f"Round {total_rounds} Clear"
    world.multiworld.completion_condition[player] = (
        lambda state: state.can_reach_location(final_round_loc, player)
    )
