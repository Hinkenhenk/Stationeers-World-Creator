# Build and install

A prebuilt package is available from [GitHub Releases](https://github.com/Hinkenhenk/Stationeers-World-Creator/releases/latest).

## Install the package

Close Stationeers. Extract `WorldCreator-0.2.146.zip` into `Documents\My Games\Stationeers\mods`, replacing the existing WorldCreator DLL and About metadata. Keep one installed copy; store old builds outside the game's mod folders. Enable StationeersWorldCreator in your LaunchPad profile and open World Creator from the main menu.

## Build from source

From this directory, run:

```powershell
dotnet build .\WorldCreator.Mod.csproj -c Release -p:GameDir='D:\Games\steamapps\common\Stationeers'
```

Replace GameDir with your local Stationeers path. The DLL is written to `bin\Release\netstandard2.1\WorldCreator.Mod.dll`. Create `Documents\My Games\Stationeers\mods\WorldCreator\About`, copy the DLL into WorldCreator, and copy `About\About.xml` into its About subfolder.

The source targets BepInEx 5 and StationeersLaunchPad. The version is defined in BuildInfo.cs and About/About.xml. Game assemblies are referenced from the local installation and are not redistributed.

## Climate preview

ClimatePreview.cs owns the isolated atmosphere and gas-response curves. WorldCreatorWindow.Climate.cs draws the Environment graphs and breakdown. It calls the game's curve evaluators and preserves Unity tangents and weights. Its initial-world calculation excludes live weather, latent heat, and external heat input.

The focused snapshot check can be run from the repository root:

```powershell
dotnet run --project WorldCreator.Mod.Checks -- --climate-snapshot
```

The release build and focused checks pass. The full existing export suite currently encounters a Windows directory-move access error on the development machine. The new Unity graphs have not yet been checked in-game.
