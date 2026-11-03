# Building from source

Install the .NET SDK and set up Stationeers with BepInEx 5 and StationeersLaunchPad. From the repository root, run:

```powershell
dotnet build .\WorldCreator.Mod\WorldCreator.Mod.csproj -c Release -p:GameDir='D:\Games\steamapps\common\Stationeers'
```

Change `GameDir` to your Stationeers folder. The project reads the game and Unity assemblies from that installation.

The output is `WorldCreator.Mod\bin\Release\netstandard2.1\WorldCreator.dll`. Copy it into `Documents\My Games\Stationeers\mods\WorldCreator`, with `WorldCreator.Mod\About\About.xml` in the `About` subfolder. Close the game before replacing files. Remove an older `WorldCreator.Mod.dll` so LaunchPad does not find two copies.

Version numbers are set in `WorldCreator.Mod\BuildInfo.cs` and `WorldCreator.Mod\About\About.xml`. Keep them in sync when preparing a release. A release ZIP contains the `WorldCreator` folder with `WorldCreator.dll`, `About\About.xml` and `README.txt`.

## Source layout

- `WorldCreatorWindow.cs` handles the editor, with separate files for climate, help and map controls.
- `WorldProject.cs` reads and writes world definitions and validates generated mods.
- `ClimatePreview.cs` calls the game's climate evaluators on a project-owned atmosphere.
- `MainMenuPatch.cs` adds the main-menu entry; `EditorWindow.cs` handles the menu integration.
- Weather and asset libraries are stored in the player's Stationeers Documents folder.

The build currently reports a `System.Net.Http` reference conflict through the game's assembly dependencies. It does not prevent compilation.
