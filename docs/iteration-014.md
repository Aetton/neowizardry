# Iteration 014 — native item-instance data

Recovered local item initialization from the original executable and extended the patched item importer with mode, signed initial amount/dice, maximum stack, flags and raw record evidence. New item exports use schema 3; schema 2 remains readable. All 819 definitions were imported, validated and passed through maximum-amount local initialization and a 12-byte codec roundtrip. See [item-instance format](item-instance-format.md).

Added a lossless local slot codec, adaptation to the independent combination planner, and result initialization that preserves dice requirements before a stack output's count is overwritten with batch size. Quantity and charges are independent; unidentified/unknown flag values and unused slot bytes survive decoding. No complete original save parser, inventory migration or live native crafting is claimed. Prototype gameplay and the deferred rework features retain their behavior.

Local validation: 25 Python tests, 308 portable C# checks (314 with original item data), warning-free client build, real-data Godot gameplay smoke with 819 schema-3 items/150 spell references/36 combination references, and synthetic mobile smoke passed. Public fixtures and packages contain no original item records or executable data.
