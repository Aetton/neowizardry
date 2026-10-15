# Iteration 011 — original spell metadata import

Added `python -m tools.slf spells ...` over the existing patch/loose-file resolver. It exports native names, descriptions, costs, levels, raw school/realm/range/target/usability codes, raw duration/damage fields, independent source hashes and record hashes/raw bytes. The original 150-record table and 160-entry description resource were decoded and loaded into Godot. Details and evidence: [spell-format.md](spell-format.md).

Menu → Spell reference opens a searchable read-only reference. Level-zero entries can be included explicitly. This does not grant spell ownership or execute original spells; the casting prototype stays separate. Public CI/builds use an invented one-entry reference fixture. Original full tables and descriptions are not committed or bundled.

Validation: 20 Python tests, 242 C# checks including controls, warning-free client build and gameplay smoke loading all 150 real records passed locally. Graphical reference UI inspected. Final Linux and Windows verification pending.
