namespace WorldCreator
{
    internal static class EditorHelp
    {
        internal static readonly string[] Titles =
        {
            "Overview", "Environment", "Weather", "Orbit & seasons", "Resources",
            "Terrain", "Advanced", "Map studio", "Starting conditions", "Asset libraries"
        };

        internal static readonly (string Title, string Body)[] GettingStarted =
        {
            ("1. Begin with a blank world",
                "Choose Create a New World on the World Creator start page. A blank world has basic editable settings, a temperature curve and a default starting-profile reference. It does not generate a complete custom terrain. You can build it without applying an original-world preset."),
            ("2. Give the world an identity",
                "On Overview, enter a display name such as First World and a unique ID such as FirstWorld. The editor accepts 1–47 letters, numbers or underscores. No prefix is required; WC_ is only an optional naming convention. Add a description and choose gravity. For example, -3.7 means a weaker downward pull than -9.81. Keep the same World ID while finishing this project."),
            ("3. Supply the terrain",
                "Open Terrain, then Open Terrain Tools. World Creator hides so you can use the game's terrain utility. Import and compile terrain with the size expected by this world, saving its indexed Terrain<number>.dat files to a folder. Reopen World Creator from the main menu, enter that folder on Terrain and choose Use compiled terrain. A painted region map alone does not create terrain geometry."),
            ("4. Set the orbit and landing reference",
                "On Orbit & seasons, choose the available orbital distance, period, shape and orientation values. Keep eccentricity below 1 for an elliptical orbit. Set latitude between -90 and 90 degrees and longitude between -180 and 180 degrees. Use the orbit and Landing site views to inspect the result. Camera rotation, zoom, phase and Simulate are preview controls; they do not save a new starting time into the world."),
            ("5. Build the atmosphere",
                "On Environment, choose a gas, enter its amount in moles and press Add gas. Quantities describe each 8,000-litre atmosphere cell. Set the base-temperature points between 0 and 180 degrees of sun angle. Temperature units are display choices; the world stores kelvin. Inspect the pressure estimate and combined-temperature preview. Add optional solar, greenhouse or density offset groups only when you want those extra effects."),
            ("6. Choose weather",
                "On Weather, add an installed event or create your own. Set cooldown in game days, and start delay and duration in seconds. Remove an event from the world to exclude it. To reuse a custom event in other projects, choose Save to My Weather on its settings card. A blank world can also remain without weather events."),
            ("7. Prepare gear, maps and assets",
                "On Starting conditions, keep the default profile or add a copy of an installed profile and customize it. Add supply crates and item quantities where needed. On Map studio, create or edit region masks only if your design needs them. Import reusable files through Asset libraries, add project copies, and assign their displayed paths to the settings that use them. If you need a particular ore layout, check the matching resource definitions and region links as well."),
            ("8. Create and try the world mod",
                "Press Create world mod in the footer. Correct any reported input or file-reference problems. The output is a world-definition mod in your local Stationeers mods folder; it is not a saved game. Restart Stationeers, enable that generated world mod in LaunchPad, then choose it when starting a New World. Keep the original source files and a backup of the generated mod. Use Load a World to continue from an installed world definition later.")
        };

        internal static readonly (string Title, string Body)[][] Pages =
        {
            new (string Title, string Body)[]
            {
                ("What Overview is for",
                    "Overview identifies the world and sets its main presentation options. The World Project header refers to the same project on every page. The summary tiles count or display existing settings; they are not separate inputs. Continue building buttons take you to the page that edits each part."),
                ("Display name, World ID and description",
                    "Display name is the name players see in the world menu. World ID is the identifier used by XML references and the generated mod. Choose a unique ID with 1–47 letters, numbers or underscores; no prefix is required. WC_ is optional. Avoid reusing an ID from another installed world. Description is the longer text shown for the world. The multiline box accepts several paragraphs. Names are readable text; IDs must remain exact and unique."),
                ("Gravity",
                    "Gravity uses metres per second squared. Stationeers world definitions use a negative value for downward gravity: -9.81 has a stronger pull than -3.7. Change the value itself to alter the world. Orbiting the preview camera or changing the planet's displayed size does not change gravity."),
                ("Starting from an original world",
                    "Select an installed world in Start from an original world and press Apply preset. Confirming replaces the current project with that selected template. Apply it before making custom changes you want to keep. To work entirely from scratch, leave this card unused. Load a World on the start page instead reads a world definition from a local or Workshop mod; it does not load a saved play session."),
                ("World list and visibility",
                    "World list priority influences where the definition appears in the game's list. The visibility checkbox is backed by the XML Hidden flag: enabling that flag hides the world from normal selection. Keep a world visible while checking that a newly created mod appears in New World. Priority and visibility do not change terrain, difficulty or atmospheric conditions."),
                ("Primary body, lighting and appearance",
                    "Primary body ID identifies the celestial reference used by the world; it may be a star, planet or another defined body. Primary star irradiance is the reference solar input in W/m² used by the orbital calculation. Ambience sound ID, skybox material ID and Sun prefab ID select existing game or mod resources. They are resource identifiers, so an invented name will not create a new sound, material or light."),
                ("Visual scale and celestial clock",
                    "Celestial body scale and skybox scale change the appearance of the sky. They do not change physical orbital distance. Clock offset days, hours, minutes and seconds shift the initial celestial clock. They do not set the duration of a game day. Some source worlds leave components unspecified; keep source values unless you intend to supply a new offset.")
            },
            new (string Title, string Body)[]
            {
                ("Gas amounts and composition",
                    "Choose a gas from the dropdown, enter Amount (mol), then press Add gas. The amount is moles per 8,000-litre atmosphere cell, not a percentage. Edit the listed quantities to change the mixture. Remove gas beside a gas entry deletes it from this world's atmosphere; it can be added again from the dropdown. Removing every gas creates a vacuum mixture. The colored composition bar always represents the whole mixture; the legend reports each gas's share and amount. Gas temperature is controlled by the world temperature settings rather than a separate temperature for each added gas."),
                ("Pressure estimate",
                    "The reference temperature beside Pressure estimate is only a calculator input. It does not edit the world temperature. The result is a gas-only ideal-gas estimate rather than a complete simulation of liquid content or phase changes. Empty or invalid inputs show - kPa. Actual in-game pressure also depends on atmospheric conditions and changes between gas and liquid."),
                ("Base temperature and sun angle",
                    "The base curve gives an absolute temperature for each sun angle. Zero degrees means overhead sun, 90 degrees is the horizon and 180 degrees is night on the opposite side. This is a 0-180 degree angle, not a 24-hour clock or a full orbital revolution. Click empty graph space to add a point, or drag an existing point. The point list below lets you enter exact angle and temperature values or remove points."),
                ("Display units and graph scale",
                    "Kelvin, Celsius and Fahrenheit change how temperatures are displayed and entered; the XML stores the base temperature in kelvin. The graph bottom corresponds to 0 K. Scale top is always entered in kelvin. Leave it empty for automatic scaling. A requested top below the highest existing temperature cannot hide that point; the display raises the top to include it. Scaling the graph does not change a point's stored temperature."),
                ("Solar, greenhouse and density offsets",
                    "Offsets add to the base temperature. Their values are temperature changes, so a 10 K offset also means a 10°C change. Solar-radiation curves use a relative sunlight input from 0 to 100 across the calculated orbital irradiance range. Greenhouse curves use the combined greenhouse index. Density curves use gas concentration in mmol/L. Day and Night curves define different adjustments; the game blends them around the day/night transition. The XML name Time means curve input here, not clock time."),
                ("Combined-temperature preview",
                    "The preview combines the base, sunlight, greenhouse and density contributions using the game's curve evaluators. The colored buttons show or hide each line. Inspect sun angle selects the position used by the breakdown card. Relative sunlight level selects the input to the solar-radiation curves. These sliders are preview inputs. They do not rewrite world settings. This initial-world calculation excludes active weather, phase-change energy and player heat input."),
                ("Gas greenhouse response",
                    "The gas dropdown selects a response curve to inspect. Its horizontal axis is concentration in mmol/L; its vertical axis is a contribution to the greenhouse index. Contributions from the mixture are added together, then the world's greenhouse temperature curves turn the combined index into a temperature change. The table lists the current concentrations and contributions. Response definitions come from loaded game data, or installed terraforming.xml when no definitions are loaded; this view does not edit those shared definitions."),
                ("World conditions and optional groups",
                    "Maximum sun intensity limits apparent sunlight and lighting; it is separate from orbital distance and the atmosphere's base temperature. Global atmosphere Volume, when present, is the effective simulation volume in litres, separate from one 8,000-litre cell. Optional setting groups at the bottom copy a missing group from a listed installed world. Check the group's source and input units before adding it. Available groups depend on the world's existing XML and the installed game data.")
            },
            new (string Title, string Body)[]
            {
                ("Installed weather versus My Saved Weather",
                    "Installed Weather lists available game or source-world events. Add to world assigns the selected event; Remove from world excludes it from this project. This does not delete an installed preset. My Saved Weather lists your reusable custom effects. It has its own dropdown so personal saves stay separate from installed examples."),
                ("Create a custom effect",
                    "Enter a Weather ID beginning with WCWeather_ and a readable display name. Create blank effect supplies initial timing and effect values. Copy selected event uses the selected installed event as a starting point for an independent custom effect. Edit the resulting event card below. The game name key is a localization identifier, not a duration or weather type; the new effect's generated key is a suitable starting value."),
                ("Cooldown, start delay and duration",
                    "Cooldown minimum and maximum are measured in game days. Start delay minimum and maximum are seconds before the event becomes active. Duration minimum and maximum are active seconds. Keep each minimum less than or equal to its maximum. Cooldown 0 does not disable an event. Natural weather also has the game's initial scheduling delay, so shorter values do not guarantee a storm immediately on day zero."),
                ("Temperature, sunlight and gameplay",
                    "Day and night temperature offsets are changes in kelvin added while the event runs. Movement speed uses a multiplier: 1 keeps the baseline speed, and values below 1 reduce it. Damage multiplier scales the event's weather-damage contribution; 0 removes that contribution. Solar ratio changes the event's sunlight contribution. Wind strength and direction configure the weather's wind parameters. Wind strength has no confirmed physical unit in this editor; compare it with an installed event rather than treating it as metres per second."),
                ("Visual effect cards",
                    "Custom effects can add Fog, Storm effect and Directional light cards. Fog controls color and any available distance settings. Storm-effect settings describe the rendered cloud or storm layers, including scale, density, textures and colors. Directional-light settings describe its intensity, frequency and color. Removing a card removes that XML block. Repeated cards remain separate in the editor, but the game's schema may consume only one block of a given type; repeated cards do not guarantee several simultaneous rendered effects."),
                ("Audio and availability",
                    "Wind sound enabled controls the event's wind-audio flag. Shell sound slots refer to existing game sound resources; the editor supports up to four slots where this event format permits them. Can occur in orbit marks the event's orbital availability. These flags change the event definition. They do not start a weather event by themselves or create a new audio file."),
                ("Save, delete and reuse",
                    "Choose Save to My Weather on a custom event's card to keep its current definition in your personal on-disk library. Those saves remain available after restarting the game and in future projects. Delete saved effect removes the personal library copy after confirmation. A copy already added to the open project stays there. Remove from world instead changes only this world's event list. Create world mod includes the events assigned to the world.")
            },
            new (string Title, string Body)[]
            {
                ("Parent body and playable body",
                    "An orbit belongs to a body and has a parent-body reference. The parent need not be the Sun; it can be another defined celestial body. The playable body is the world whose location and landing reference are being edited. Preserve valid body IDs and parent links. Changing a readable world name does not rename these underlying celestial references."),
                ("Distance, period and orbit shape",
                    "Average orbital distance is the semi-major axis. Use the displayed unit, AU or kilometres; these are alternate ways to describe orbit size. Orbital period is the time for a full orbit in simulated game days. Eccentricity controls shape: 0 is circular, larger values stretch the ellipse, and an elliptical orbit must stay below 1. Periapsis is the closest approach and apoapsis the farthest point. The editor's readouts help compare them."),
                ("Orbit orientation",
                    "Inclination tilts the orbital plane relative to its reference plane. Orbit plane angle, or longitude of ascending node, turns that plane around the reference axis. Closest approach angle, or argument of periapsis, turns the ellipse within its own plane. Longitude at epoch sets a reference position at the orbital epoch. These are different controls; changing one is not equivalent to rotating the planet's spin axis."),
                ("Celestial appearance and rotation fields",
                    "Display-color red, green and blue channels use 0-1 values and affect the celestial body's color. They do not paint the terrain. Sunrise offset is an angular adjustment to sunrise/sunset alignment. When a source supplies additional rotation fields, follow their displayed model and units; a rotation period in days is different from an orbit period, and orbit inclination is different from axial tilt. Source-specific fields may be available through Advanced."),
                ("Landing reference coordinates",
                    "Latitude runs from -90 degrees at the south pole to +90 at the north pole; 0 is the equator. Longitude runs from -180 to +180 degrees around the body. These coordinates locate the playable reference on the celestial surface. They are separate from any terrain-map start-location coordinates or lander spawn definitions. The red marker in Landing site is an illustrative coordinate reference, not a terrain-height or safe-landing guarantee."),
                ("Camera views, spheres and lighting",
                    "Above (XZ), Side (XY), 3D and Landing site show different projections of the same preview. Drag in the interactive views to rotate it. Zoom changes the display size; Reset view restores the camera. The centered XYZ lines are orientation references. Available body textures are used where supported, with a fallback color otherwise. Preview sphere size and lighting are visual aids rather than an accurate physical scale or a full simulation of eclipses."),
                ("Phase and Simulate",
                    "Position along orbit changes the preview's current phase. Simulate animates it; 1x means one preview revolution per minute and 10x is ten times that preview rate. This is a viewing speed, not the saved game orbital period. Camera drag, zoom, phase and simulation controls do not change the world XML. In Landing site, simulation moves the lighting reference around the displayed globe."),
                ("What the created mod changes",
                    "Edited orbital data is packaged using a world-specific celestial-body definition and references where the editor supports that source. Installed vanilla definitions stay untouched. The preview helps inspect shape and coordinates, but unusual parent-body arrangements, rotation rules and extreme orbital values still need a check in a newly started game world.")
            },
            new (string Title, string Body)[]
            {
                ("Regional resource entries",
                    "This page lists resource-related attributes already present in the world's surface and deep-resource entries. Each entry belongs to the region identified in its label. The controls you see depend on the loaded world definition; a blank project may have no regional resource entries to edit."),
                ("Ore density",
                    "OreDensity is a relative multiplier on that entry's baseline ore density. A value of 1 keeps the authored baseline; larger values increase its density multiplier. It is not a percentage, an item-stack count or a command to place one specific vein. Preserve the source meaning when other regional attributes use different units."),
                ("Surface and deep resources",
                    "Surface-resource entries and deep-resource entries are separate definitions. Editing a surface-region density does not automatically change a deep-mining profile. Keep the region ID and the resource profile it references consistent. Detailed ore mixes, vein shapes and deep-mining quantities may be contained in other existing XML records, rather than in a simple density field on this page."),
                ("How map regions affect resources",
                    "A region mask can determine where a linked resource definition applies. Map studio edits those mask pixels; it does not automatically invent a new ore profile for every painted color. A complete custom layout needs both the resource definitions and the region references they use. Changing an ID without updating its links can leave a region without the intended resources."),
                ("What is preserved",
                    "Existing shared resource definitions are retained by their references. This page does not provide a full recipe or ore-vein authoring system. For a small change, adjust the exposed regional values. For source-specific settings, inspect their existing records in Advanced and retain unknown fields unless their behavior is understood."),
                ("Check the resulting world",
                    "Create world mod saves the edited resource values, but the preview does not show generated underground veins. Check the resulting terrain and mining behavior in a newly started world. If a blank definition lacks the resource records needed for your design, begin from a suitable existing definition or supply correctly linked XML records; the density controls alone cannot create a complete resource setup.")
            },
            new (string Title, string Body)[]
            {
                ("Terrain geometry and map images",
                    "Terrain is the physical ground used by the game. Region masks, minimaps and ground textures are images associated with that world; they are not a replacement for compiled terrain geometry. A blank project's basic TerrainSettings entry does not generate a playable landscape by itself."),
                ("Read the terrain summary",
                    "The summary shows the source path, world-size setting, curvature and any available minimap, cracks, lava or other map references. World size is the game's terrain-size value, not a directly documented kilometre measurement. Curvature is a game terrain setting rather than an ordinary physical radius. Use values compatible with the terrain files you will compile or reuse."),
                ("Add a source map",
                    "Set Source type to Heightmap, Crust, Cracks or Lava and enter a full local file path. Heightmaps use the supported raw/data format and must match the world's expected terrain dimensions and float-sample layout. Image sources use PNG. Validate and add source checks the file and records it for packaging. It does not turn the source image into compiled terrain automatically."),
                ("Use the game's Terrain Tools",
                    "Open Terrain Tools opens the game's own terrain utility without a console command. World Creator hides while that utility is open. Use its import and compilation controls, then reopen World Creator from the main menu to continue the same project. The utility is a separate game tool; its controls are not embedded in the Terrain page."),
                ("Select compiled terrain",
                    "Enter the folder containing the terrain utility's indexed Terrain<number>.dat output and choose Use compiled terrain. The editor validates the supported indexed files and packages selected chunks under this world's GameData/Worlds folder. Keep source maps and compiled output distinct: adding a source heightmap alone does not select compiled chunks."),
                ("Existing worlds and blank worlds",
                    "An existing source world can retain references to its original compiled terrain when no replacement is selected. A blank world has no complete original landscape to reuse, so supply appropriate compiled terrain before expecting it to load as a playable custom landscape. Applying a world preset replaces the current project after confirmation; do that before custom work you need to keep."),
                ("File paths and previews",
                    "Input fields accept local paths while you work. The created mod uses its packaged relative paths and required game resources. A PNG preview confirms that an image can be decoded; it does not verify the final ground shape or safe spawn locations. Keep the original authoring files outside the game mod folder so you can compile them again later.")
            },
            new (string Title, string Body)[]
            {
                ("What Advanced contains",
                    "Advanced exposes the existing world's XML attributes and leaf values, including numeric, text and boolean fields. Paths show the record each value belongs to. It is intended for settings that are source-specific or not covered by a guided card. Unrecognized existing nodes are retained when the world mod is created."),
                ("Find a setting",
                    "Use Filter path or value to narrow the visible fields. Search for a record name such as GlobalAtmosphere, TerrainSettings or PlayableBody, or for an existing value. A filter changes the view only. Hiding a row with the filter does not disable or delete that setting."),
                ("Numbers, text and checkboxes",
                    "Use a decimal point for numbers. Keep minimum and maximum values in the correct order. Text fields often contain exact resource IDs or relative file paths. Checkboxes represent true/false settings. A familiar XML name does not guarantee a familiar unit: Time on an atmosphere curve can mean sun angle, concentration or another input instead of seconds."),
                ("IDs and file references",
                    "An ID connects one record to another; a file path points to an asset expected by the game loader. Changing only one side of a reference can disconnect it. A referenced file must be packaged in the world mod or supplied by the installed game or another required mod. Entering a new ID or path does not create the referenced object."),
                ("Curve shape controls",
                    "InTangent and OutTangent are slopes on either side of a curve point. InWeight and OutWeight influence how strongly the curve follows those directions. WeightedMode selects which weights are active. Guided curve editing preserves these shape settings. Change them here only when you want to adjust interpolation rather than just move a point. The calculated climate preview uses the game's weighted curve evaluation."),
                ("Optional setting groups",
                    "The browser at the bottom offers supported groups found in installed world templates but missing from this project. Check the source-world name before adding a group, then review its values. This copies a setting group; it is not a universal switch for every game feature. Available groups depend on this project's XML and the installed game version."),
                ("Scope and checking changes",
                    "Advanced edits the world records loaded into this project. It is not a general editor for every shared game-data file, recipe or per-gas greenhouse response definition. Avoid giving the same setting conflicting values in guided and Advanced fields. Create world mod checks supported inputs and references, but an unfamiliar setting's full behavior may still require an in-game check.")
            },
            new (string Title, string Body)[]
            {
                ("Layers and region colors",
                    "A region mask is an image whose exact RGB colors identify regions used by world data. Layer checkboxes control which overlays are visible. A selected paint layer and region determine which color a brush writes. Viewing a layer alone does not change it. Painting a new color does not automatically create a matching ore, spawn or environment profile."),
                ("Create a geographic layer",
                    "On a world without one, Create geographic layer adds a new geographic mask and its required region records. Add named region creates another region with its own identifier and color. Enter a readable region name where the name field is available, select that region, and paint its area. Region names and their localization records are included when the world mod is created."),
                ("Paint, erase and undo",
                    "Select a visible layer and region, enable Paint selected region on map, then left-drag on the preview. Brush radius is measured in preview pixels. For a newly created geographic layer, Erase to base region paints the layer's defined base color back onto the map. Undo last map change restores the most recent stored map change; it is not an unlimited history."),
                ("Use an existing PNG mask",
                    "Import the PNG through Asset libraries first, select a layer and region, then choose Use PNG as selected layer mask. Visible pixels must use that layer's defined colors exactly; transparent pixels are allowed. The editor validates the mask before using it. Similar-looking colors with different RGB values are different region identifiers."),
                ("Use a visual guide",
                    "A library PNG, JPG or JPEG can be shown as an editor-only reference image. Use as visual guide changes the background you paint against. That guide is not exported as a game region mask and does not assign terrain settings. Clear visual guide removes the reference from the editor view without deleting the library file."),
                ("Restore or save a mask",
                    "Restore source mask returns an existing layer to its source image, or a newly created geographic layer to its blank base. Save selected mask as PNG writes a standalone copy under the WorldCreator/MapExports folder in your Stationeers Documents data. Create world mod separately packages edited masks with the world-specific references it generates."),
                ("Points of interest and linked data",
                    "Point-of-interest markers are preview positions derived from their referenced region masks. They help inspect links and are not exact safe-spawn or height measurements. Existing POI, resource and playable-area records still need valid region references. Editing fields on this page affects the project; keep mask colors, region IDs and the records that reference them consistent.")
            },
            new (string Title, string Body)[]
            {
                ("Choose an installed profile",
                    "The dropdown lists installed starting profiles with readable names and IDs. Add copy to this world creates a world-specific copy of the selected profile's spawn references. For a profile already referenced by the world, choose Customize before editing its custom data. Existing game definitions are preserved."),
                ("Default, name and removal",
                    "Display name labels the custom starting option. Make default selects the option initially used for this world. Remove removes the option from this world's list. Keep a usable starting option while building a normal playable world. New blank condition starts from the installed default profile's references; it does not mean every lander and item slot starts empty."),
                ("Understand spawn roles",
                    "Spawn ID is an exact reference to an existing spawn definition. World applies when the world is created. Player applies to a joining player, and Respawn to a respawning player. New-player kit and Respawn kit refer to their corresponding equipment roles. Keep the intended event and definition paired; moving a lander reference into a kit role does not convert it into equipment."),
                ("Profile flags",
                    "Brutal mode enables the game's special brutal-start rules for this profile. Terrain edit marks a terrain-editing start. These are special game flags, separate from the world's gravity, gases or ore density. Leave them off for a normal start unless your design deliberately requires the corresponding behavior."),
                ("Build supply crates",
                    "For a custom condition, enter a crate name and choose Add supply crate. Search for an item by name or prefab ID, select the match, set a whole-number quantity of at least 1, then use Add selected item on the desired crate. You can rename crates, edit their item IDs and quantities, or remove individual items and crates. The editor writes the supported nested crate/item records for you."),
                ("Game and modded item IDs",
                    "The search catalog covers known game items. A modded prefab ID can be entered directly, but the supplying mod must be available when the world runs. A valid-looking identifier is not proof that an item can be spawned, and the editor does not simulate every container's capacity or equipment compatibility."),
                ("Landers and additional spawns",
                    "Use Add spawn to reference a game or mod spawn definition for one of the available roles. Existing landers, capsules and nested kits remain linked definitions rather than being flattened into the supply-crate list. The crate builder is not a complete lander construction tool. Preserve valid original references unless you have replacement definitions ready."),
                ("What is saved",
                    "Create world mod packages custom starting-condition and supported crate/spawn definitions, plus their generated names. These custom conditions belong to this world project; they do not use the reusable My Saved Weather library. Test both a fresh player's start and respawn behavior if you changed those roles, because they can use different definitions.")
            },
            new (string Title, string Body)[]
            {
                ("Import a reusable file",
                    "Enter the full local path to a supported PNG, JPG, JPEG, DDS, TIF, TIFF, RAW, DATA or DAT file, choose a category, then Import file to library. The library stores a copy and leaves the original file in place. The displayed library folder is the persistent location used by this installation."),
                ("Categories, filters and search",
                    "Surface, Underground, Atmosphere / Sky, Map / UI and Other are organizational categories. Use the type and category filters plus Search to find a file. Changing a category or filter does not apply that file to a world setting. A file named ground.png is not automatically selected as the planet's surface texture."),
                ("Library copy versus project copy",
                    "The library is shared by projects on this computer and remains after the game restarts. Add to project makes a separate copy for the current world. That project copy is included when Create world mod runs. Another computer does not automatically receive your personal library; keep a backup of the library and original files if you need them for later editing."),
                ("Assign the displayed asset path",
                    "Assets in this world shows the relative path that the created mod will use under GameData/Worlds/<world ID>/Assets. Copy that path into the world XML setting that needs the file. Adding a file to the project makes it available; it does not decide whether the file is a ground texture, sky image, map mask or another resource."),
                ("Remove copies independently",
                    "Remove from library asks you to confirm removal of the reusable library copy. The original file and any existing project copy remain. Remove from project excludes that project copy from this world's packaged assets while leaving the reusable library copy available. Check references before removing a file that a world setting still needs."),
                ("Map guides and masks",
                    "Map studio can use suitable library images as visual guides, and supported PNGs as starting region masks. A guide is editor-only. A region mask must match the selected layer's exact region colors and is used by exported world data. Choose the appropriate action in Map studio; importing the image into the library alone does neither."),
                ("Terrain files need terrain setup",
                    "A library can store terrain-related source files, but adding an individual RAW or DAT asset to a project does not compile a landscape or select a complete compiled terrain set. Use Terrain to validate source maps and choose the indexed compiled-terrain folder. Use the library for reusable file copies, and each world page for the actual setting that uses them.")
            }
        };
    }
}
