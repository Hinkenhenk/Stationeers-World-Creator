# World Creator

An in-game world editor for Stationeers. Create a world from scratch, start from a vanilla preset, or edit a world installed locally or through Steam Workshop.

## Install

Requires Stationeers, BepInEx 5 and [StationeersLaunchPad](https://stationeerslaunchpad.github.io/).

1. Download the **WorldCreator ZIP** from [Releases](https://github.com/Hinkenhenk/Stationeers-World-Creator/releases/latest).
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
- **Starting conditions:** starting profiles, landers, supply crates and individual equipment slots. Search for compatible items, adjust quantities and rename containers.
- **Map Studio and Terrain:** region masks, map painting and access to the game's terrain tools.
- **Resources, Asset libraries and Advanced:** ore settings, project files and the underlying XML fields.

Each page has a **Help** button. When finished, choose **Create world mod**. Restart Stationeers, enable the generated world mod in LaunchPad, then select it when starting a new world.

A blank project needs compiled terrain before it can be played. The climate preview shows the initial world conditions; active weather, phase changes and player heat can change the result in game.

Tested with Stationeers **0.2.6429.27802**. Game assemblies are read from your installation and are not included here.

Report problems in [Issues](https://github.com/Hinkenhenk/Stationeers-World-Creator/issues), with the mod version and the relevant log or screenshot.

## Map Studio

Paint region masks with a brush or one pixel at a time. Use the mouse wheel over the map to zoom, and hold the wheel button to pan. The pipette selects a region from its mask colour; Ctrl+Z undoes recent map changes.

Playable boundaries can be polygons, squares or circles. Move the area, resize it, or add polygon corners, then choose **Apply playable area**. Region names, descriptions and RGB colours are edited in the palette; the editor keeps the XML references connected for you.

## Code layout

The editor and world data are split by feature. To change a page, start with its `WorldCreatorWindow.<feature>.cs` file. The matching `WorldProject.<feature>.cs` file handles its XML and validation.

- `WorldCreatorWindow.Layout.cs`: page layout, navigation and the shared header/footer.
- `WorldCreatorWindow.Cards.cs`, `Controls.cs` and `Styles.cs`: card spacing, dropdowns, tooltips and appearance.
- `WorldCreatorWindow.TemperatureChart.cs`, `Climate.cs`, `Orbit.cs` and `MapStudio.cs`: previews and their controls.
- `WorldProject.cs`: project construction and shared state. `WorldProject.Types.cs` defines the data passed to the editor.
- `WorldProject.Export.cs` and `Validation.cs`: world-mod files, reference checks and safe export staging.

Each method has a summary describing its purpose. Keep control flow shallow: prefer three or four indentation levels, and review any case that needs more than five. Page drawing and model changes must preserve Unity's Layout/Repaint ordering.

## Release builds

Releases are built locally and uploaded as prebuilt packages. GitHub Actions is not used.

To build with your installed game and BepInEx:

```powershell
./scripts/Build-Package.ps1 -GameDirectory 'D:/Games/steamapps/common/Stationeers'
```

The script creates a ZIP and SHA256 checksum in `dist`. It packages our DLL, mod metadata and installation notes; game assemblies and textures are excluded. For a release, update the version in `WorldCreator/BuildInfo.cs` and `WorldCreator/About/About.xml`, build and test, then upload the ZIP and checksum to the matching GitHub release.
