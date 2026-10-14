# Original-layout HUD

The desktop HUD follows the original Wizardry 8 screen organization: character portraits on both sides of the world view, a lower-center message journal, a lower-left compass/map and a lower-right realm dial, with commands along the bottom. The reference was inspected visually: https://grueatemyhomework.wordpress.com/2011/06/21/retro-mondays-2-wizardry-8/ (wiz81.jpg); the original Steam store also provides screenshots at https://store.steampowered.com/app/245450/Wizardry_8/.

This is a layout implementation using new procedural UI, not imported original artwork. The current portraits are temporary silhouettes, and the dark frame is drawn by code. No original portraits, icons or textures are bundled.

## Working controls

- Click a portrait to select the hero. The selection is shared by Magic, Items and Craft. A gold border marks the selection; the bottom strip shows current HP and weapon.
- Red vertical bars show HP; blue bars show total current/maximum realm mana. Portrait tooltips show exact HP, XP, individual mana pools and timed effects. No stamina value is fabricated.
- Party sizes 1–8 remain supported. Portraits split across the two sides, with up to four per side; unoccupied slots contain no fake heroes.
- The compass plots the actual test-room position and facing plus the training sentinel. It is not a monastery minimap or an imported world map.
- The realm dial opens Magic for the selected hero and preselects Fire or Divine. Other realms remain disabled until represented by learned spells; the dial does not implement additional spell systems.
- Attack, Magic, Items and Craft remain on the bottom command strip. Journal toggles the message log. Menu contains New party, Save, Load, keeper dialogue and the synthetic capture/escape controls.
- The current synthetic room, battle mechanics, recipes and spells remain unchanged.

Touch mode keeps larger action targets, a movement pad and Party/Log overlays. Desktop portrait rails, compass and dial are hidden in that mode. Dialogs use the same dark theme and suspend world look/movement. A graphical desktop preview with eight heroes is available through `--ui-preview`; this resets that preview party and is intended for layout inspection.
