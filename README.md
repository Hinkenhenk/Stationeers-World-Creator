# Stationeers World Creator

An in-game BepInEx mod for creating and editing custom Stationeers worlds.

Current version: **0.2.150**.

Download the ready-to-install **WorldCreator-0.2.150.zip** from [Releases](https://github.com/Hinkenhenk/Stationeers-World-Creator/releases/latest). BepInEx 5 and StationeersLaunchPad must already be installed.

Extract the ZIP into `Documents\My Games\Stationeers\mods`. The result should be `mods\WorldCreator\About\About.xml` and `mods\WorldCreator\WorldCreator.Mod.dll`. Enable StationeersWorldCreator in LaunchPad, then open World Creator from the main menu.

## In this update

- Help on every editor page opens page-specific explanations and an eight-step guide to creating a world from scratch.
- Hover help now appears in front of the editor. Climate plots stay inside their scroll viewport.
- Orbit simulation advances on the editor page; sphere lighting avoids the previous white overexposure.

- Environment includes a combined temperature preview and baseline, sunlight, greenhouse, and density contributions, using the installed game's curve evaluators.
- Inspect the temperature breakdown at a selected sun angle and sunlight input. Per-gas greenhouse graphs and a concentration/contribution table explain how the gas mixture affects the result.
- Installed weather and personal saved effects have separate dropdowns. Custom effects persist across game restarts and can be deleted with confirmation. Installed presets cannot be deleted from the personal library.
- Empty pressure-estimate inputs retain `- kPa`; temperature-curve labels and descriptions explain their actual inputs.

The climate preview works on an isolated copy of the project atmosphere. It excludes transient weather, phase-change energy, and player heat input. Gas greenhouse definitions come from the loaded game, or the installed `terraforming.xml` when no definitions are loaded. It does not author shared gas-response definitions.

The source and build/install notes are in [WorldCreator.Mod](WorldCreator.Mod/README.md). Verification checks are in [WorldCreator.Mod.Checks](WorldCreator.Mod.Checks/Program.cs).

Game DLLs, textures, decompiled sources, research inventories, and old archives are not included. The release ZIP contains only the mod DLL, metadata, and installation notes.


Build 0.2.150 adds Remove gas controls, a square Map Studio preview, and separate cards for region sets and their regions. World IDs no longer require WC_; help, tooltips and validation agree. Gas removal and World ID XML/export checks passed. The UI changes still need an in-game review. The broader suite reports a missing texture when re-exporting a loaded custom map.

