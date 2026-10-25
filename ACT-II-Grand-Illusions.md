# Act II: Grand Illusions

> **Play order (changed 2026-10-25):** tricks 2 and 4 and tricks 5 and 9 were swapped. The order is now 01 Rabbit · 02 Flowering Hat · 03 Floating Lady · 04 Substitution · 05 Still Rain · 06 Invisible Card · 07 Water Vault · 08 Mirror Split · 09 Golden Transfer · 10 Vanishing Vault (`RabbitTrickGame.PlayOrder`). Patience is 50 s for every act, and finishing act 10 plays a curtain call.

The campaign now has ten playable acts. In **View Levels**, use **ACT II: TRICKS 06–10** to turn the programme page. Every act has its own playable demonstration and a different stage set. The existing five acts remain Act I.

All player-facing text is English. A/D moves the magician, W/S moves smoothly in depth, arrows move the selected hand, F switches hands, and E grabs or releases the selected hand. Keep one hand gripping, switch with F, and use the other hand for a second control. P opens the pause menu, which includes restart, demonstration, settings, main menu, and quit. Each act starts with its looping how-to demonstration; after the first three seconds, Space or Enter starts normal play.

Objects and controls are physically available from the start. The three-step checklist records a convincing performance; it does not unlock interaction. Distance, reach, actual cover, exposure, timers, and mechanical failure still apply.

## 06 — The Invisible Card

**Set:** a midnight gallery with geometric paintings and a gold display frame.

**New mechanism:** real two-hand palming. A playing card is hidden only when the other palm is actually over its center. An uncovered secret card in the spotlight accumulates exposure and is caught after a short grace period.

1. Stand beside the display, pull its knob up, and show the empty frame in the light. Leave the frame open.
2. Collect the card from the right wing. Raise it, switch hands, and place the empty palm over it. Carry both palms together to the frame. Release the card into the opening while the other palm hides the load.
3. The frame's shutter closes over the loaded card. Step to its right and move the empty hand away, then open it in the spotlight. A body or palm blocking the card does not count as a reveal.

The spotlight examines the empty frame first, follows the magician during transport, and returns to the display for the reveal. The contact must remain covered during hand movement and walking; moving to the back alone does not make the card invisible.

## 07 — The Water Vault Escape

**Set:** a teal industrial theatre with plumbing, a sealed tank, a large clock, and a separate balcony.

**New mechanism:** simultaneous drain-and-release controls plus a real air deadline. The left valve fills when raised and drains when lowered; it returns to neutral when released. The safety latch requires the other hand. The water level and remaining air are visible.

1. Open the front curtain and hold the left valve up. Show the assistant in the flooded tank under the spotlight.
2. Lower the curtain. Hold the left valve down while the other hand raises the right safety latch. The escape occurs only with the tank drained and the latch released. Opening the visible hatch exposes the method; leaving the assistant submerged exhausts her air.
3. Release both mechanisms, let the latch close, and raise the curtain. The spotlight first verifies the empty tank, then moves to the assistant on the left balcony.

This is a game apparatus with a fictional hidden transfer. Its controls and timing are designed for the two-hand game, rather than real-world escape instruction.

## 08 — The Mirror Split

**Set:** a burgundy mirror salon with a display cabinet on the right and a separate control desk on the left, keeping the assistant visible.

**New mechanism:** two independent optical alignments and a reversible blade pass. Tune the left mirror to **32%** and the right to **72%**. Green lamps confirm the safe image arrangement.

1. Open the center curtain and show the whole assistant with both mirrors upright.
2. Tune both mirrors, then drag the blade grip right. The reflected image separates as the blade crosses it.
3. Withdraw the blade fully left before restoring both mirrors to **0%**. Show the assistant whole again in the light.

Moving a visible blade through an unaligned image reveals the illusion. Untuning a mirror while the blade remains inside also fails. Closing the veil and moving a control early is possible, but it does not replace the witnessed proofs.

## 05 — The Still Rain (moved to Act I)

**Set:** a rain-dark rooftop, skyline, projector, pump console, water nozzle, and catch basin.

**New mechanism:** a two-hand stroboscopic water illusion. The left hand supplies pump pressure; the right tunes the apparent motion. Droplets visibly fall, freeze, and rise using the actual frequency state.

1. Hold the left pump near **75%** and show falling water in the spotlight.
2. Keep that hand gripping. Use the other hand to tune the right strobe to **60%** and hold it steady until the audience sees the frozen rain.
3. Retune the strobe to **82%** while maintaining pressure. The apparent flow reverses and rises into the nozzle.

Releasing the pump drains pressure. Full pressure or an extreme strobe setting overheats the apparatus if held too long; the heat meter warns before failure. A correct effect performed outside the spotlight does not advance the show.

## 10 — The Vanishing Vault

**Set:** an Art Deco bank hall with a revolving two-chamber vault, balance tray, and banknote cannon.

**New mechanism:** counterweight balance plus an attention window and a physical rotating chamber. The brass weight balances the real gold case; without it, the vault lurches. The banknote cannon pulls the spotlight right for about seven seconds after winding.

1. Stand to the right of the vault, open it, and show the real gold in the light, then close the door.
2. Fetch the brass weight from the left floor and release it onto the balance tray. Wind the right banknote cannon. While the audience watches the flying notes, pull the vault wheel right to **100%**.
3. Open the vault's other chamber from the side. Wait for the spotlight to return and reveal the unobstructed empty interior; the finale spreads a rain of banknotes across the stage.

The gold physically belongs to the hidden rear chamber after the turn, so its depth and mask order follow that chamber. A rotation is exposed if the door is open or the vault is lit. Reaching the rear without actual vault cover does not hide a loose gold case.

## Integration and validation

The new acts share the established hands, reach constraints, depth sorting, spotlight renderer, pause menu, audience patience, curtain transition, and classical recordings. They use distinct scenery and mechanisms rather than new control keys. All ten demonstrations drive the same hand controls and gameplay rules as normal play.

Validation covers ten successful acts, preservation of the first five acts, the new failure cases, live play-mode performances, both programme pages, and the existing input/menu checks. Review captures are written under `Logs/Act06-*` through `Logs/Act10-*`; reports are `Logs/TenActValidation.txt` and `Logs/GrandIllusionsRuntime.txt`.

## Creative references

The gallery palming, water spectacle, and banknote finale use the broad cinematic language of an illusionist heist show, as described in the [official Now You See Me 2 synopsis](https://www.lionsgate.com/movies/now-you-see-me-2). The tank act also takes inspiration from the stage-escape tradition documented by [Guinness World Records](https://www.guinnessworldrecords.com/news/2024/03/escapologists-death-defying-escape-from-water-tank-leaves-audience-holding-their-768070). These are original interactive game routines and stage designs.
