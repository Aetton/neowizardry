# Iteration 012 — native combination data

Located the original item-combination scan in the supplied executable through its merge-item assertion. Recovered two ingredient IDs and a skill threshold from fixed item-record offsets, including order-independent comparison. Added the read-only `recipes` SLF command with patch/loose provenance, raw bytes/hashes and anomaly reporting. See [recipe evidence and limits](recipe-format.md).

The supplied patched table yields 36 candidates: 35 named outputs and one empty-name anomaly. Same-item pairs and zero skill thresholds survive export. Uniform `-1` and `-2` pairs are counted separately. No original databases, executable bytes or full recipe list are committed. Gameplay remains on synthetic crafting until consumption and actor requirements are established.

Validation: 24 Python tests passed, including four new recipe tests covering patch selection, repeated ingredients, raw hashes, sentinels, anomalous references, unknown skills, duplicate pairs and malformed lengths. A real-data CLI export succeeded against `DATA.SLF` plus `PATCH.001`. No client or core behavior changed; this iteration does not require a new Windows download.
