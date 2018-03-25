## Dark Reign mod readme

### Outstanding issues

There's currently a lot of problems as this mod is almost brand new. It's pretty much RA with a DR skin.

There's no shadows, no building underlay sprites, unit hitboxes are off, tile transition graphics are not available, resources don't work like the original, etc.

We also don't know all the DR file formats such as the tilesets (.TIL) and palettes.

* Construction rigs can't build buildings like the original

Major. Building in the original had you pick a building from the build palette, place it, and having the rig move to the site and begin building. Much like Starcraft or Warcraft.

OpenRA doesn't really support this yet. Instead we have a single, tough Construction Rig that can build and acts as a mobile base provider. Much like a commander in TA or Supcom.

People have worked on parallel build queues: https://github.com/OpenRA/OpenRA/pull/14201

But the code has moved on in the meantime. I had a look and it's a bit of effort to adapt this to the new API.

Apparently the mod Medieval Warfare has this implemented: https://github.com/CombinE88/Medieval-Warfare/tree/Sultan_Addon

But I haven't had much of a look yet.

* Resources don't work like the original

Major. Resources in the original had credits, water, and taelon. Water and Taelon have their own bars, and when the player fills up either bar, they get ~3000 credits.

In OpenRA, we just deposit these resources directly as credits.

* Freighters can return resources to the wrong refinery type

In DR, freighters can harvest taelon and return it to a power generator, or harvest water and return it to a water launch pad. But in OpenRA they can return the resources to either building.

Currently, there's no way to tell freighters to return taelon to power for example.

* No tile elevation

A unique feature of DR, the map also had an elevation layer. Firing from an elevated position had stat benefits. It was also used for line of sight calculations.

* No line of sight fog reveal

Feature. Very unlikely to ever be implemented. DR has a unique way of calculating view through the fog. It considers trees, rocks, etc. and also elevation.

* No map conversion

Feature. The DR map format is unknown and can't be read. Somebody could try and decode it one day to convert all the old DR maps.
Even just the tiles would be great.

* No sprites from tilesets

Feature. A big one. We can't read the .TIL format. We need this to get the tile masks, animations, and building underlays (mud & construction site sprites).

Currently the tiles used are screenshotted from the DR editor and assembled manually.

* No shadows

Major. DR shadows are defined in a seperate sprite file. I've tried combining the two files in the sequence with Combine:, but this only lets me animate by switching between the sprite and its shadow.

I can accomplish this with an overlay in the rules, but I haven't figured out how to render it as a shadow instead of a black blob.

* NNE facing projectiles crash

Major. When shooting north north east, it usually crashes the game due to a large facing value. Currently we're addressing this with a copy of DefaultSpriteSequence: HackySpriteSequence. Line 245 contains a hack to always choose a valid facing.

Only happens if the Length of the projectile sequence is > 1. Otherwise, it doesn't do the facing of the bolt correctly, but it doesn't crash the game.

* WavReader problems

The current WavReader doesn't handle the DR wav files. The DR wavs have junk at the end and it isn't being dealt with properly. Easily solved. I have a PR for this: https://github.com/OpenRA/OpenRA/pull/15007

* Palettes from JASC files

We don't use a cnc format for the terrain palettes, instead we use a JASC palette. There's a way to load these palettes in Palettes.yaml using PaletteFromGimpOrJascFile, but the tileset palettes are loaded differently. The tileset palette is defined in the tileset yaml as Palette, accepting a filename of a cnc palette.

We need the tileset palette to instead get the palette by name from Palettes.yaml so we can load it as a JASC file. I have a PR out for this, but it's going to need some changes: https://github.com/OpenRA/OpenRA/pull/14985

* Palette remapping

Minor. Palette remapping indices for player colors is 16 in cnc. But we only have 8 indices to work with.

Currently this is OK because we're just using 8 instead. The colors look OK. But it's just the first 8 instead of selecting maybe every 2nd color.

Located in palettes.yaml:PlayerColorPalette

* No unique art for the in-game interface

We're just using the RA in-game interface.

* No fire and smoke effects on damaged units/structures

Would be nice to have.

* Build sounds are annoying

OpenRA could use a throttle on building and construction complete sounds. Apparently this is technically difficult due to the nature of the multi-platform code.

* No Imperium-specific voices for generic unit types

Generic unit types like the construction rig or the freighter use the freedom guard voice by default. Not sure how to have a faction-specific voice.

* No building upgrades

Buildings that can be upgraded do not have their sprites changed to the upgraded one.

* Selecting barracks or assembly plant doesn't select the right build palette

Like it says on the tin. I'm not sure how to wire this up.

* Build clock animation and fade-out is all black

The default build-time animation included with the example mod shows in all black. This shows as ugly black boxes in the build palette.

Need to check which palette it is using. Maybe locate DR build clock animations instead?

* Bounding boxes

No real effort has been put into setting the hit shape and bounding boxes for units and buildings.

* Animation speeds

Minor. Currently almost everything animates at 100 ticks. This should be examined and set to a more appropriate value for various common animations.

* Conversion of unit speed to OpenRA speed

I'm not quite sure how to convert speed values from DR unit definitions to the OpenRA speed. Currently we're just multiplying by 10 and that seems adequate, but it's not the exact value.

* Reload speeds

Similar to the previous two: I'm not sure how to convert weapon reload values from Weapon.txt (DR definition file). Currently we're just multiplying by 2.

* Proliferation of ".spr" in sequences

For some reason I can't work out how to define a sprite sequence just using the filename, e.g. "ufsabmn0", without ".spr".

* Bink video decoder

Minor. DR uses Bink video. The format was reverse-engineered by the FFmpeg project and Bink decoding is supported by the open-source libavcodec library.

This could be wrapped, if the license permits.

