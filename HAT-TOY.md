# The main-menu magic hat

Click the top hat on the home screen. A bobbing arrow and **CLICK THE HAT** identify the interaction. Every accepted click releases an animated animal with its own stylised call and rarity effects. There is a strict one-second cooldown measured in real time, so it works while the main menu pauses gameplay. Rejected clicks make no draw and play no sound. Repeated species are possible; the requested distribution is preserved.

| Species | Species chance | Aura colour | Golden chance per click |
| --- | ---: | --- | ---: |
| Mouse | 40% | Black | 0.4% |
| Rabbit | 20% | Grey | 0.2% |
| Dove | 15% | White | 0.15% |
| Snake | 10% | Green | 0.1% |
| Eagle | 5% | Cyan | 0.05% |
| Monkey | 3% | Blue | 0.03% |
| Bear | 2% | Purple | 0.02% |
| Lion | 1.5% | Pink | 0.015% |
| Tiger | 1.5% | Pink | 0.015% |
| Elephant | 0.9% | Orange | 0.009% |
| Orca | 0.5% | Red | 0.005% |
| Emperor penguin | 0.5% | Red | 0.005% |
| Divine dragon | 0.1% | Full-screen rainbow | 0.001% |

Species probabilities total exactly 100%. A separate **1% golden upgrade** is drawn after the species, preserving the species distribution. Golden chances in the last column include that upgrade: a golden dragon is one in 100,000 clicks. Normal and golden variants share the species probability; golden variants replace 1% of that species' appearances.

Higher rarities create larger rings and more sparks. A divine dragon sweeps into the centre of the screen with prismatic rays, colour washes, expanding rings, stars, a dragon call and a magical fanfare. Every golden animal has a gold palette, extra gold light and stars, plus a separate gold chime. A golden dragon combines both celebrations.

The home-menu art uses the same simple cut-paper shapes and velvet/gold palette as the stage. All 13 normal and 13 golden variants are drawn by native UI geometry. Small-animal calls, the divine dragon and gold cues are original sound designs. Eagle, elephant and bear now use licensed wildlife recordings; lion and tiger use licensed designed roars. See HAT-PERFORMANCES.md and AudioSources.json for sources. They are reproducible with `Tools/GenerateHatAudio.py`. Asset hashes and sources are recorded in `Assets/Resources/HatAudio/AudioSources.json`.

All animals and effects are non-interactive graphics. Start Game, View Levels, Settings and Quit remain clickable even during the dragon celebration. Leaving home immediately stops hat sounds and removes visitors and effects. Sound-effects and master-volume settings apply. Returning home rebuilds the hat's click listener but keeps the remaining cooldown. All player-facing text is English.

Editor checks: `Sleight.RabbitTrick.EditorTools.MenuHatValidation.Prepare` exhaustively validates one million species/gold ticket combinations and decodes the 15 audio clips. `MenuHatValidation.Run` exercises native mouse raycasts, the unscaled cooldown, all 26 visual variants, volume controls and menu cleanup, with screenshots in `Logs/Hat-*.png`.

## Queue the next animal in the editor

Open **Sleight > Debug > Magic Hat** (F7). In Play mode, choose Animal and Golden version, then select **QUEUE NEXT HAT CLICK**. The next successful home-menu hat click releases that exact variant once and automatically resumes random draws. Rejected clicks during cooldown or outside the home menu do not consume it. **CLEAR QUEUED ANIMAL** cancels it; **QUEUE GOLDEN DIVINE DRAGON** is a shortcut. **REVEAL QUEUED ANIMAL NOW** uses the normal reveal and cooldown path.

Editor-only API: MenuHatToy.QueueNextForEditor(HatSpecies.DivineDragon, true), ClearQueuedForEditor(), HasQueuedForEditor and QueuedForEditor. The queue and window are excluded from player builds.

## Long animal performances

Animals now perform long species-specific exits with wind, dust, ice, bubbles, clouds and a thrown banana. All environmental effects have golden variants. See [Long animal performances](HAT-PERFORMANCES.md) for timings, audio sources and the F7 animation-time slider.
