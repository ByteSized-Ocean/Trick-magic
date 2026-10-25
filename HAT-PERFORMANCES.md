# Long animal entrances and exits

Main-menu animals now perform a complete entrance, short presentation, and species-specific exit. Mouse, rabbit and dove exits take 4.5 seconds, about 60% faster than their original 7.2-second exits; their 2.4-second entrance and presentation stay the same. Other normal visits last about 9.6–10.9 seconds and the dragon lasts about 13.7 seconds, plus a short environmental tail. Both normal and golden variants use these timings. Rapid clicks preserve the one-second cooldown and let the previous animal begin its full exit; they no longer destroy or instantly fade it. All animations run on unscaled time while the menu pauses the game.

| Animal | Exit performance | Environmental effects and sounds |
| --- | --- | --- |
| Mouse | Scurries toward alternating screen edges with moving paws | Small tracks and its squeak |
| Rabbit | Scampers and hops toward the screen edges, ears bouncing | Footprints and its call |
| Snake | Uncoils, then slithers in a moving S shape toward the edges | Faint winding trail and hiss |
| Dove | Flaps faster and climbs out of the screen | Drifting feathers and coo |
| Eagle | Sweeps upward while beating its wings | Eagle call, a short swirling gust and wind ribbons |
| Monkey | Bounds away through four jumps | Throws one spinning banana toward the camera; it grows in perspective, with throw and impact cues |
| Bear, lion, tiger | Change to a side-on running pose, moving all four legs | Growls/roars, paw sounds and rolling sand clouds |
| Elephant | Raises its trunk and runs with heavy steps | Elephant trumpet, heavier dust, flying stones and stampede sounds |
| Emperor penguin | Appears among glacier shards, tips smoothly onto its belly, then slides away | A continuous icy trail, frost crystals and ice-sliding sounds |
| Orca | Appears over an ocean swell and swims away with its tail moving | Waves, a splash, water bubbles and swimming sounds |
| Divine dragon | Arrives among seven-colour auspicious clouds, spirals twice, then flies off | Following cloud banks, a cloud trail, dragon call, magical fanfare and airy flight sound |

Golden variants keep their gold bodies and rarity burst. Wind, dust, stones, banana, ice, ocean, bubbles, clouds and the whole travelling trail also switch to gold tones, with a gold chime at departure. Normal dragon clouds use seven rainbow colours; golden dragon clouds use a range of gold tones while retaining the original prismatic celebration.

All graphics are non-interactive, including the approaching banana, so the menu buttons remain clickable. Leaving home clears every visitor, aura, trail and sound immediately. Completed sequences reclaim their UI objects. Existing animal probabilities, 1% golden upgrade and F7 next-animal override are unchanged.

## Preview

Run Game, press **F7**, choose the animal and golden flag, and queue or reveal it. The new **Animation time** slider scrubs the most recent visitor's timeline, including its departure and environmental trail. It does not skip the one-second click cooldown or spend another animal draw.

## Audio sources

Eagle: [National Park Service recording](https://commons.wikimedia.org/wiki/File:Bald_Eagle_Yellowstone_National_Park.ogg), public domain. Elephant: [Info-farmer's trumpeting recording](https://commons.wikimedia.org/wiki/File:Elephant_voice_-_trumpeting.ogg), CC0. Bear: [U.S. Fish & Wildlife Service growls edited by AntumDeluge](https://opengameart.org/content/bear-growls), CC0. Lion and tiger: [SFXMint lion roar](https://sfxmint.com/sounds/lion-roar-02) and [tiger roar](https://sfxmint.com/sounds/short-tiger-roar-01), designed animal roars released under CC0.

The nine environmental Foley cues are original sound designs. `Tools/PrepareHatJourneyAudio.py` downloads the licensed calls, matches their peak levels, prepares the Foley, and updates `Assets/Resources/HatAudio/AudioSources.json` with sources, licences, processing and hashes. Run it after `Tools/GenerateHatAudio.py` if regenerating the entire hat sound set.

Verification: `HatJourneyValidation.Prepare` checks all 52 motion routes, long durations, finite positions, offscreen exits, belly pose, jumps, probabilities and audio import. `HatJourneyValidation.Run` captures the entrances, exits and trails of all 26 variants through the native Unity renderer and verifies the one-shot debug override, cooldown, sounds, rapid clicks, menu raycasts and cleanup. Runtime screenshots are saved as `Logs/HatJourney-*.png`.
