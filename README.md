# World Creator

An in-game world editor for Stationeers. Create a world from scratch, start from a vanilla preset, or edit a world installed locally or through Steam Workshop.

## Install

Requires Stationeers, BepInEx 5 and [StationeersLaunchPad](https://stationeerslaunchpad.github.io/).

1. Download **WorldCreator-0.2.154.zip** from [Releases](https://github.com/Hinkenhenk/Stationeers-World-Creator/releases/latest).
2. Close Stationeers and extract the `WorldCreator` folder into `Documents\My Games\Stationeers\mods`.
3. Enable **StationeersWorldCreator** in your LaunchPad profile, then open **Create World** from the game's main menu.

The installed folder should contain:

```text
WorldCreator/
  About/
    About.xml
  WorldCreator.dll
  README.txt
```

## Using the editor

Choose **Create a New World** for a blank project, or **Load a World** to edit an installed world definition. Vanilla presets are available on Overview.

- **Environment:** gas composition, temperature curves and climate calculations.
- **Weather:** installed events, custom effects and a reusable weather library.
- **Orbit & seasons:** orbital settings, landing coordinates and a rotating preview.
- **Starting conditions:** spawn profiles, supply crates and equipment.
- **Map Studio and Terrain:** region masks, map painting and access to the game's terrain tools.
- **Resources, Asset libraries and Advanced:** ore settings, project files and the underlying XML fields.

Each page has a **Help** button. When finished, choose **Create world mod**. Restart Stationeers, enable the generated world mod in LaunchPad, then select it when starting a new world.

A blank project needs compiled terrain before it can be played. The climate preview shows the initial world conditions; active weather, phase changes and player heat can change the result in game.

The mod targets Stationeers **0.2.6428.27798**. Game assemblies are read from your installation and are not included here.

## Known issues
- Re-exporting a loaded custom map can fail because a referenced texture is missing from the output.

Report problems in [Issues](https://github.com/Hinkenhenk/Stationeers-World-Creator/issues), with the mod version and the relevant log or screenshot.
