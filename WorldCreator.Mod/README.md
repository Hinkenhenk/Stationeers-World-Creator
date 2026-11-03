# Build and install

This repository contains source only; it does not include a prebuilt mod package.

Build with the .NET SDK and your local Stationeers install path:

```powershell
dotnet build .\WorldCreator.Mod.csproj -c Release -p:GameDir='D:\Games\steamapps\common\Stationeers'
```

Replace the example `GameDir` with your Stationeers installation folder. Copy the generated `WorldCreator` mod folder into `Documents\My Games\Stationeers\mods`, then enable it in LaunchPad.

The source targets BepInEx 5 and StationeersLaunchPad. The mod version is defined in `BuildInfo.cs`.
