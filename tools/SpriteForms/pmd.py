#!/usr/bin/env python3
"""Builds the Poképark's PMD form table: which PMDCollab sprite folder belongs to each PKHeX
(species, form), and whether that folder has a shiny.

Why: PMDCollab numbers its forms per species in its own order (Raichu 0001 = Alola, Vivillon
0000 = Meadow), PKHeX in another (Vivillon form 0 = Icy Snow). The two share only the names, so
the mapping is made by name, PKHeX's English form names (tools/SpriteForms/FormDump) against
the subgroup names in PMDCollab's tracker.json, with the few spellings that differ in ALIASES
and the forms PMDCollab draws as its base sprite in BASE.

Usage:
    dotnet run --project tools/SpriteForms/FormDump -- /tmp/forms.tsv
    curl -o /tmp/tracker.json https://raw.githubusercontent.com/PMDCollab/SpriteCollab/master/tracker.json
    python3 tools/SpriteForms/pmd.py /tmp/forms.tsv /tmp/tracker.json [output]
        output defaults to src/PKForge.Domain/Resources/pmdforms.tsv

Output (UTF-8 TSV, sorted): key "<species>-<form>", the PMD folder ("0000" for the base
sprite), flags (1 sprite, 2 shiny sprite, 4 the female sprite of that folder). Forms with no PMD sprite are left out: the park
shows PKHeX's still sprite for them rather than the wrong creature.
"""
import csv
import json
import os
import re
import sys
import unicodedata

# PKHeX name -> PMDCollab name, where they spell it differently.
ALIASES = {
    (412, "Sandy"): "Sand", (413, "Sandy"): "Sand",
    (128, "Paldea Combat"): "Paldea",
    (666, "Savanna"): "Savannah",
    (888, "Crowned"): "Crowned_Sword",
    (889, "Crowned"): "Crowned_Shield",
    (892, "Rapid-Strike"): "Rapid_Strike",
    (898, "Ice"): "Ice_Rider",
    (898, "Shadow"): "Shadow_Rider",
    (800, "Dusk"): "Dusk_Mane",
    (800, "Dawn"): "Dawn_Wings",
    (201, "!"): "Exclamation",
    (201, "?"): "Question",
    (1017, "*Teal"): "Teal_Mask",
    (1017, "*Wellspring"): "Wellspring_Mask",
    (1017, "*Hearthflame"): "Hearthflame_Mask",
    (1017, "*Cornerstone"): "Cornerstone_Mask",
    (869, "Ruby Cream"): "Ruby_Cream_Strawberry_Sweet",
    (869, "Matcha Cream"): "Matcha_Cream_Strawberry_Sweet",
    (869, "Mint Cream"): "Mint_Cream_Strawberry_Sweet",
    (869, "Lemon Cream"): "Lemon_Cream_Strawberry_Sweet",
    (869, "Salted Cream"): "Salted_Cream_Strawberry_Sweet",
    (869, "Ruby Swirl"): "Ruby_Swirl_Strawberry_Sweet",
    (869, "Caramel Swirl"): "Caramel_Swirl_Strawberry_Sweet",
    (869, "Rainbow Swirl"): "Rainbow_Swirl_Strawberry_Sweet",
    (59, "Lord"): "Hisui", (101, "Lord"): "Hisui", (549, "Lady"): "Hisui", (713, "Lord"): "Hisui",
    (710, "Jumbo"): "Super", (711, "Jumbo"): "Super", (875, "Noice Face"): "Noice", (982, "Three-Segment"): "Three",
    (718, "10%-C"): "10",
    (774, "C-Red"): "Red", (774, "C-Orange"): "Orange", (774, "C-Yellow"): "Yellow", (774, "C-Green"): "Green",
    (774, "C-Blue"): "Blue", (774, "C-Indigo"): "Indigo", (774, "C-Violet"): "Violet",
}

# Forms that PMDCollab draws as the species' base sprite (its 0000 folder).
BASE = {
    "", "Normal", "Plant", "Meadow", "Midday", "Baile", "Teal", "Vanilla Cream", "A", "Hero", "Red Striped", "Altered",
    "Incarnate", "Land", "Ordinary", "Aria", "Shield", "Average", "Solo", "Disguised", "Full Belly", "Amped",
    "Ice Face", "Single Strike", "Family of Four", "Green Plumage", "Curly", "Zero", "Chest", "Overcast",
    "West", "Standard", "Red", "Spring", "Male", "Natural", "Confined", "50%", "Baile Style", "M-Red", "M-Orange",
    "M-Yellow", "M-Green", "M-Blue", "M-Indigo", "M-Violet", "Two-Segment", "Phony", "Unremarkable", "Terastal",
    # Looks the same as the base sprite: totem and noble sizes, Scatterbug's and Spewpa's
    # patterns (only Vivillon shows them), the teapots' marks, drives and ride modes.
    "Large", "Starter", "Legend", "Water", "Electric", "Fire", "Ice", "Dusk", "Antique", "Artisan", "Masterpiece",
    "Sprinting", "Swimming", "Gliding", "Drive", "Aquatic", "Glide", "50%-C", "Active", "Sandy", "Trash", "Lord",
}
# Species whose form names in BASE would be wrong outside them (Genesect's drives, Rockruff's
# Dusk, Xerneas' Active): BASE applies to these names only for these species.
BASE_ONLY = {"Water": {649}, "Electric": {649}, "Fire": {649}, "Ice": {649}, "Dusk": {744}, "Active": {716},
             "Starter": {133}, "Legend": {493}, "Large": {20, 105, 735, 738, 743, 752, 754, 758, 777, 778, 784},
             "Sandy": {414}, "Trash": {414}, "Lord": {900}, "Sprinting": {1007}, "Swimming": {1007}, "Gliding": {1007}, "Drive": {1008}, "Aquatic": {1008}, "Glide": {1008}}
# Female forms PKHeX counts as forms: PMDCollab keeps them as the female of the base sprite.
FEMALE = {(678, "F"), (876, "F"), (902, "F"), (916, "F")}
PATTERNED = {664, 665}


def norm(name):
    name = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9!?]", "", name.lower())


def is_base(species, form, name):
    if form == 0 or species in PATTERNED:
        return True
    return name in BASE and species in BASE_ONLY.get(name, {species})


def main():
    forms_path, tracker_path = sys.argv[1], sys.argv[2]
    out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.dirname(__file__), "../../src/PKForge.Domain/Resources/pmdforms.tsv")
    tracker = json.load(open(tracker_path, encoding="utf-8"))
    # The newest game's name for each form (Gen9 first); Gen6 cosplay Pikachu shares numbers
    # with the caps and is left to the still sprite.
    names = {}
    with open(forms_path, encoding="utf-8") as f:
        for row in csv.reader(f, delimiter="\t"):
            species, form, context = int(row[0]), int(row[1]), row[2]
            name = row[4] if len(row) > 4 else ""
            rank = int(re.sub(r"\D", "", context) or 0)
            if (species, form) not in names or rank > names[(species, form)][0]:
                names[(species, form)] = (rank, name)

    rows, unmatched = [], []
    for (species, form), (_, name) in sorted(names.items()):
        entry = tracker.get(f"{species:04d}")
        if entry is None or species <= 0:
            continue
        groups = entry.get("subgroups", {})
        wanted = ALIASES.get((species, name), name)
        folder = next((k for k, g in groups.items() if k != "0000" and norm(g["name"]) == norm(wanted)), None)
        female = (species, name) in FEMALE
        if folder is None and (female or is_base(species, form, name)):
            folder = "0000"
        if folder is None:
            unmatched.append(f"{species}-{form} {entry['name']} {name!r}")
            continue
        group = groups.get(folder, {})
        drawn = (entry if folder == "0000" else group).get("sprite_complete", 0) >= 1
        shiny = group.get("subgroups", {}).get("0001", {}).get("sprite_complete", 0) >= 1
        if female:
            # sprite/<species>/0000/<shiny 0000|0001>/0002
            drawn = group.get("subgroups", {}).get("0000", {}).get("subgroups", {}).get("0002", {}).get("sprite_complete", 0) >= 1
            shiny = group.get("subgroups", {}).get("0001", {}).get("subgroups", {}).get("0002", {}).get("sprite_complete", 0) >= 1
        if not drawn:
            continue
        rows.append((species, form, folder, 1 | (2 if shiny else 0) | (4 if female else 0)))

    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("# PKHeX form -> PMDCollab sprite folder. Generated by tools/SpriteForms/pmd.py; do not edit.\n")
        f.write("# Sprites: PMDCollab SpriteCollab (https://github.com/PMDCollab/SpriteCollab), credits per sprite.\n")
        f.write("# key\tfolder\tflags(1 sprite, 2 shiny, 4 female)\n")
        for species, form, folder, flags in rows:
            f.write(f"{species}-{form}\t{folder}\t{flags}\n")
    print(f"{len(rows)} forms with a PMD sprite, {len(unmatched)} without a PMD form", file=sys.stderr)
    for line in unmatched:
        print("  unmatched:", line, file=sys.stderr)


if __name__ == "__main__":
    main()
