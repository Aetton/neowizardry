# Character initiative and ordinary single-hand queue

Iteration 033; static reconstruction of the supplied executable, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. No original-runtime comparison is claimed.

`0x4EE000..0x4EE118` builds the character's base initiative from effective Speed (actor +0x14D), Senses (+0x161), total level (+0x89), enabled Snakespeed (+0x767/+0x76D), signed runtime adjustment (+0x1770) and resolved encumbrance (+0xBC9):

```
base = floor((level+1)/2) + floor(Speed/5) + floor(Senses/5) - 10
     + (Snakespeed enabled ? floor(skill/10)+1 : 0)
     + signedAdjustment - [0,1,2,4,8][loadCategory]
```

The multiply/shift divisions are unsigned integer divisions, not floating point or truncation of `(attribute-50)/5`. Low attributes can give negative initiative. The class-level contribution to attack budgets is a different calculation.

At `0x4E8A23..0x4E8A9A`, each eligible character receives one exclusive-4 draw. Weapon orders add the selected active hand's initiative; other orders do not. Hand preparation initializes +0x1D then adds the signed source weapon byte +0x48 at `0x4EE653..0x4EE66E`; this is runtime character +0x1166. Scores clamp to [-10,89], then queue time is `90-score` (1..100). Initial placement happens before action execution. Casts therefore do not inherit an equipped weapon's initiative.

For the ordinary single active hand, `0x4EAD6D..0x4EAD9B` computes continuation after a group of swings:

```
next = scheduledTime + floor((timelineAnchor-currentTime+100)/(remainingAttacks+1))
```

Values above 100 leave the round. The context anchor is native context +0x9C; the character round setup clears it through global context +0xB4 (`0x4E8593`, character context starts at +0x18). It remains zero in this neutral immediate-execution scenario; it is not updated to the preceding attack's time. Pause/resume/animation adjustments write this field elsewhere (`0x4E8201`) and are outside the scenario. Dual-hand selection and timing adjustment at `0x4EACF3..0x4EADE1` are excluded. Global +0x910 enables a separate quantized scheduling policy in `0x4F0480`; the live subset uses its zero branch only.

New training parties store explicit initiative facts and interleave attack groups in the shared executor. Stable hero/opponent/ID ties, first eligible target selection, immediate delivery, neutral conditions and prototype RNG remain authored scenario policies. No complete native animation scheduler or monster initiative producer is asserted. Earlier saved encounters with no initiative facts retain their prior Speed ordering; mixed partial facts are rejected.
