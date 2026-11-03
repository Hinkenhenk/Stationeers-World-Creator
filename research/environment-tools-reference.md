# Built-in environment tools — saved response

Saved 3 November 2026 at the user's request. Evidence: the locally decompiled Stationeers build 0.2.6428.27798. These findings are from source inspection, not live command testing.

Yes. I checked the decompiled code for your installed build, and there are several useful tools beyond `terraineditor`.

| Tool / console command | What it provides | Use for World Creator |
|---|---|---|
| `worldsetting` | Temperature component graphs, combined temperature, global gas mixture, greenhouse curves, and planetary atmosphere information. | The strongest foundation for climate calculations. This overlaps with the Environment integration we have started. |
| `atmos global` | Live planetary atmosphere debug information. | Compare calculated conditions with the running world. |
| `atmos world` | Outdoor atmosphere cells, including pressure, temperature, solar energy, condensation and lighting state. | Investigate why a particular location heats, cools or condenses. |
| `atmos direction` | Lines showing atmospheric movement direction. | Inspect local gas movement. |
| `atmos room`, `atmos pipe`, `atmos thing` | Atmosphere diagnostics for rooms, pipe networks and objects. | Test buildings and equipment under the world's conditions. |
| `storm start <weatherId>`, `storm stop`, `storm debug` | Force a weather event, stop it, or display weather diagnostics. | Test a loaded custom storm without waiting for its natural schedule. |
| `orbit debug`, `orbit view` | Orbital diagnostics and an orbit display in the running world. | Check orbital configuration against the actual game simulation. |
| `orbit simulate 6 Hours`, `orbit timescale 10` | Advance celestial time or accelerate orbital progression. | Examine sunlight, day/night changes and seasonal positions. |
| `celestial <bodyId>` | Inspect a celestial body; additional arguments can edit orbital parameters in single player. | Compare the editor's orbit settings with the engine's interpretation. |
| `printgasinfo` | Gas phase-change reference information, including triple-point and critical-point temperature/pressure data. | Build our gas information library from the installed game's calculations. |
| `addgas <gasType> <targetId> <moles> [temperatureK]` | Add material to a supported atmosphere, object or pipe network. | Create controlled pressure, temperature and condensation experiments. |
| `liquid show` | Liquid atmosphere and solver diagnostics. | Inspect liquid formation and behaviour. |

The atmosphere overlays expose actual simulation values, including energy and condensation—not just visual effects. Sources: `Assets.Scripts.UI.ImGuiUi/ImGuiAtmosphericDebug.cs:33` and `Util.Commands/StormCommand.cs:10` under the local decompiled Assembly-CSharp directory.

There is also a **native orbital appearance tuning window**, opened with:

```text
loworbitstation tune
```

It controls atmosphere colour, depth and falloff, surface height, planetary curvature and apparent planet position. This could help us preview how a custom atmosphere looks from orbit. These are appearance controls; they do not replace the physical atmosphere calculations. Its native Save button can write directly into the game's world XML, so integrating it would require directing saves into our generated mod instead. Source: `UI.ImGuiUi/ImGuiOrbitalViewWindow.cs:75`.

For our mod, I would prioritise:

1. **Live environment diagnostics:** planetary temperature, gas composition, pressure, greenhouse contribution and cloud/ice reservoirs.
2. **Weather test controls:** select a registered weather event, start it, stop it and inspect its state.
3. **Sunlight testing:** advance orbital time and compare the resulting solar input and temperature.
4. **Gas laboratory:** gas properties and controlled experiments using the engine's own calculations.
5. **Orbital appearance preview:** atmosphere colour and shell settings.

One important limitation: **advancing orbital time does not itself simulate all atmospheric processes for that elapsed duration**. The orbital method advances celestial bodies; planetary atmosphere processing has its own ticks. A reliable “simulate this world for a year” feature would need additional work. Source: `OrbitalSimulation.cs:272`.

I inspected the code only; I have not changed the mod or exercised these commands live. Most require a loaded world, and commands that change conditions should be tested in a disposable world.
