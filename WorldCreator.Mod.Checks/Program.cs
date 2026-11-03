using System.Xml.Linq;
using WorldCreator.Mod;

var assets = @"D:\Games\steamapps\common\Stationeers\rocketstation_Data\StreamingAssets";
var temp = Path.Combine(Directory.GetCurrentDirectory(), ".worldcreator-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var previousWeatherLibrary = Environment.GetEnvironmentVariable("WORLD_CREATOR_WEATHER_LIBRARY");
Environment.SetEnvironmentVariable("WORLD_CREATOR_WEATHER_LIBRARY", Path.Combine(temp, "WeatherLibrary"));
try
{
    var project = new WorldProject(assets, "Mars2/Mars2.xml");
    if (string.IsNullOrWhiteSpace(project.MiniMapAssetPath) || project.PointOfInterestCount == 0 || project.TerrainWorldSize == "(not set)") throw new Exception("World overview data missing");
    if (project.MapLayers.Count == 0) throw new Exception("Region map layers missing");
    var temperatureTime = project.AtmosphereFields.First(f => f.Label.Contains("/Temperature/Key") && f.Label.EndsWith(" / Time"));
    var temperatureValue = project.AtmosphereFields.First(f => f.Label.Contains("/Temperature/Key") && f.Label.EndsWith(" / Value"));
    var gasQuantity = project.AtmosphereFields.First(f => f.Label.Contains("[CarbonDioxide]") && f.Label.EndsWith(" / Quantity"));
    var cooldown = project.WeatherFields.First(f => f.Label.Contains("CoolDown") && f.Label.EndsWith(" / Min"));
    if (!temperatureTime.Tooltip.Contains("Solar angle") || !temperatureValue.Tooltip.Contains("kelvin") ||
        !gasQuantity.Tooltip.Contains("8,000-litre") || !cooldown.Tooltip.Contains("game days"))
        throw new Exception("Editable world settings are missing their unit and purpose tooltips: " + temperatureTime.Tooltip + " | " + temperatureValue.Tooltip + " | " + gasQuantity.Tooltip + " | " + cooldown.Tooltip);
    if (!project.AdvancedFields.Any(f => f.Label.Contains("/Temperature/Key") && f.Label.Contains("InTangent")))
        throw new Exception("Advanced XML no longer exposes the preserved curve interpolation data");
    var previewProject = new WorldProject(assets, "Mars2/Mars2.xml");
    var previewTemperature = previewProject.AtmosphereFields.First(f => f.Label.Contains("/Temperature/Key") && f.Label.EndsWith(" / Value"));
    previewTemperature.Value = "300";
    var editedPreview = previewProject.CreateAtmosphereSnapshot();
    if ((string?)editedPreview.Element("Temperature")?.Element("Key")?.Attribute("Value") != "300")
        throw new Exception("Climate preview ignored a temperature draft");
    previewTemperature.Value = previewTemperature.Original;
    var revertedPreview = previewProject.CreateAtmosphereSnapshot();
    if ((string?)revertedPreview.Element("Temperature")?.Element("Key")?.Attribute("Value") != previewTemperature.Original ||
        (string?)editedPreview.Element("Temperature")?.Element("Key")?.Attribute("Value") != "300")
        throw new Exception("Climate preview retained a reverted draft or changed a previous snapshot");
    var previewGas = previewProject.AtmosphereFields.First(f => f.Label.Contains("[CarbonDioxide]") && f.Label.EndsWith(" / Quantity"));
    previewGas.Value = "-1";
    var invalidPreviewRejected = false;
    try { previewProject.CreateAtmosphereSnapshot(); } catch (InvalidOperationException) { invalidPreviewRejected = true; }
    if (!invalidPreviewRejected) throw new Exception("Climate preview accepted a negative gas amount");
    if (args.Contains("--climate-snapshot"))
    {
        Console.WriteLine("Climate snapshot checks passed: edited values, reverted values, independent snapshots and invalid gas rejection.");
        return;
    }
    var blank = WorldProject.CreateBlank(assets);
    var blankReadiness = blank.ValidateForExport(temp, "WC_BlankCheck", "Blank Check");
    if (blankReadiness.Any(issue => issue.IsError) || !blankReadiness.Any(issue => issue.Text.Contains("no compiled terrain")))
        throw new Exception("The export readiness preview did not distinguish the blank-world terrain note from blocking errors");
    if (!ItemPrefabCatalog.Entries.Any(item => item.Id == "ItemIronSheets" && item.Name == "Iron Sheets"))
        throw new Exception("The embedded item-name catalog is missing its prefab ID mapping");
    if (blank.MapLayers.Count != 0 || blank.PointOfInterestCount != 0 || blank.MiniMapAssetPath != null || blank.TerrainWorldSize != "4096")
        throw new Exception("Blank world unexpectedly inherited a built-in world's map or terrain assets");
    if (!blank.AdvancedFields.Any(field => field.Label.EndsWith("/Gravity")) || !blank.AtmosphereFields.Any(field => field.Label.Contains("Temperature")) || !blank.OrbitFields.Any(field => field.Label.Contains("SemiMajorAxis")))
        throw new Exception("Blank world is missing its initial editable settings");
    var blankTarget = blank.Export(temp, "WC_BlankCheck", "Blank Check");
    var blankXml = XDocument.Load(Path.Combine(blankTarget, "GameData", "world.xml"));
    var blankWorld = blankXml.Root!.Element("WorldSettings")!.Element("World")!;
    if (blankWorld.Element("TerrainSettings")?.Attribute("Path")?.Value != "Worlds/WC_BlankCheck/Terrain" || !Directory.Exists(Path.Combine(blankTarget, "GameData", "Worlds", "WC_BlankCheck", "Terrain")))
        throw new Exception("Blank world did not export its own empty terrain folder path");
    if (!blank.ValidateForExport(temp, "WC_BlankCheck", "Blank Check").Any(issue => issue.IsError && issue.Text.Contains("already exists")))
        throw new Exception("The export readiness preview did not report an existing output folder");

    var kitProject = new WorldProject(assets, "Mars2/Mars2.xml");
    kitProject.AddStartConditionFrom("DefaultStart");
    var kitCondition = kitProject.StartConditions.Single(condition => condition.IsCustom);
    kitProject.SetStartConditionName(kitCondition.Id, "Field Engineer");
    var crateId = kitProject.AddStartConditionCrate(kitCondition.Id, "Field kit");
    kitProject.AddStartConditionCrateItem(crateId, "ItemIronSheets", 12);
    kitProject.AddStartConditionCrateItem(crateId, "ItemIronFrames", 6);
    var kitTarget = kitProject.Export(temp, "WC_KitCheck", "Kit Check");
    var kitStartData = XDocument.Load(Path.Combine(kitTarget, "GameData", "startconditions.xml"));
    var kitConditionXml = kitStartData.Root!.Elements("StartCondition").Single(condition => (string?)condition.Attribute("Id") == kitCondition.Id);
    var kitSpawnReference = kitConditionXml.Elements("Spawn").Single(reference => (string?)reference.Attribute("Id") == crateId);
    var crateDefinition = kitStartData.Root.Elements("Spawn").Single(spawn => (string?)spawn.Attribute("Id") == crateId);
    if ((string?)kitSpawnReference.Attribute("Event") != "NewWorld" || (string?)crateDefinition.Element("DynamicThing")?.Attribute("Id") != "DynamicCrate" ||
        crateDefinition.Descendants("Item").Count() != 2 || (string?)crateDefinition.Descendants("Item").First().Attribute("Id") != "ItemIronSheets" ||
        (string?)crateDefinition.Descendants("Item").First().Element("Quantity")?.Attribute("Value") != "12")
        throw new Exception("Custom start crate did not export as the installed DynamicCrate and nested Item/Quantity schema");
    var kitLanguage = XDocument.Load(Path.Combine(kitTarget, "GameData", "Language", "english.xml"));
    if (!kitLanguage.Descendants("Record").Any(record => (string?)record.Element("Key") == kitCondition.Id + "Name" && (string?)record.Element("Value") == "Field Engineer") ||
        !kitLanguage.Descendants("Record").Any(record => (string?)record.Element("Key") == crateId + "Name" && (string?)record.Element("Value") == "Field kit"))
        throw new Exception("Custom start condition or crate name localization was not exported");

    var weatherAuthoring = new WorldProject(assets, "Mars2/Mars2.xml");
    weatherAuthoring.CreateWeather("WCWeather_CheckEffect", "Check Gale");
    weatherAuthoring.AddWeatherBlock("WCWeather_CheckEffect", "Fog");
    weatherAuthoring.AddWeatherBlock("WCWeather_CheckEffect", "Fog");
    weatherAuthoring.AddWeatherBlock("WCWeather_CheckEffect", "StormEffect");
    weatherAuthoring.AddWeatherBlock("WCWeather_CheckEffect", "DirectionalLight");
    weatherAuthoring.AddWeatherSound("WCWeather_CheckEffect");
    weatherAuthoring.SaveWeatherToLibrary("WCWeather_CheckEffect");
    weatherAuthoring.SaveWeatherToLibrary("WCWeather_CheckEffect");
    var savedEffect = WeatherEffectLibrary.Load().Single(e => e.Id == "WCWeather_CheckEffect");
    if (savedEffect.Name != "Check Gale" || savedEffect.Definition.Elements("Fog").Count() != 3 || savedEffect.Definition.Element("StormEffect") == null || savedEffect.Definition.Element("DirectionalLight") == null || savedEffect.Definition.Element("Shell1Sound") == null)
        throw new Exception("Reusable weather library did not preserve the authored effect and its XML blocks");
    var customWeatherPath = weatherAuthoring.Export(temp, "WC_WeatherAuthor", "Weather Authoring");
    var customWeatherDoc = XDocument.Load(Path.Combine(customWeatherPath, "GameData", "world.xml"));
    var customWeatherId = (string?)customWeatherDoc.Root!.Elements("WeatherEvent").FirstOrDefault(e => (string?)e.Element("Name")?.Attribute("Key") == "WCWeather_CheckEffect")?.Attribute("Id");
    var customWeatherDef = customWeatherDoc.Root.Elements("WeatherEvent").SingleOrDefault(e => (string?)e.Attribute("Id") == customWeatherId);
    if (customWeatherId == null || customWeatherDef?.Elements("Fog").Count() != 3 || customWeatherDef.Element("StormEffect") == null || customWeatherDef.Element("DirectionalLight") == null || customWeatherDef.Element("Shell1Sound") == null)
        throw new Exception("Exported custom weather definition is missing its reference or gameplay/visual/audio blocks");
    var customLanguage = XDocument.Load(Path.Combine(customWeatherPath, "GameData", "Language", "english.xml"));
    var customNameKey = (string?)customWeatherDef.Element("Name")?.Attribute("Key");
    if (customLanguage.Root?.Name != "English" || !customLanguage.Root.Element("Interface")!.Elements("Record").Any(e => (string?)e.Element("Key") == customNameKey && (string?)e.Element("Value") == "Check Gale"))
        throw new Exception("Exported weather localization is missing or uses the wrong Stationeers language XML root");

    var reuseProject = new WorldProject(assets, "Mars2/Mars2.xml");
    reuseProject.AddWeather("WCWeather_CheckEffect");
    var reuseTarget = reuseProject.Export(temp, "WC_WeatherReuse", "Weather Reuse");
    var reuseDocument = XDocument.Load(Path.Combine(reuseTarget, "GameData", "world.xml"));
    var reusedRef = (string?)reuseDocument.Root!.Element("WorldSettings")!.Element("World")!.Elements("WeatherEvent").Last().Attribute("Id");
    if (reusedRef == null || reuseDocument.Root.Elements("WeatherEvent").Count(e => (string?)e.Attribute("Id") == reusedRef) != 1)
        throw new Exception("An unchanged saved weather effect was not included in the exported mod");
    if (XDocument.Load(Path.Combine(reuseTarget, "GameData", "Language", "english.xml")).Root?.Element("Interface")?.Elements("Record").Any(e => (string?)e.Element("Value") == "Check Gale") != true)
        throw new Exception("Reused weather effect lost its localized display name");

    var workshopInput = Path.Combine(temp, "WorkshopInput", "GameData");
    Directory.CreateDirectory(Path.Combine(workshopInput, "Language"));
    var workshopSource = XDocument.Load(Path.Combine(customWeatherPath, "GameData", "world.xml"));
    foreach (var definition in workshopSource.Root!.Elements("WeatherEvent").ToList()) definition.Remove();
    var workshopWorld = workshopSource.Root.Element("WorldSettings")!.Element("World")!;
    foreach (var reference in workshopWorld.Elements("WeatherEvent").ToList()) reference.Remove();
    workshopWorld.Add(new XElement("WeatherEvent", new XAttribute("Id", "WorkshopWeather")));
    workshopSource.Save(Path.Combine(workshopInput, "world.xml"));
    var workshopEvent = new XElement("WeatherEvent", new XAttribute("Id", "WorkshopWeather"), new XElement("Name", new XAttribute("Key", "WorkshopWeatherName")),
        new XElement("CoolDown", new XAttribute("Min", "1"), new XAttribute("Max", "2")), new XElement("StartDelay", new XAttribute("Min", "10"), new XAttribute("Max", "20")),
        new XElement("Duration", new XAttribute("Min", "30"), new XAttribute("Max", "60")), new XElement("Fog", new XElement("Color", new XAttribute("r", "1"), new XAttribute("g", "1"), new XAttribute("b", "1"), new XAttribute("a", "1"))));
    new XDocument(new XElement("GameData", workshopEvent)).Save(Path.Combine(workshopInput, "weather.xml"));
    new XDocument(new XElement("English", new XElement("Name", "English"), new XElement("Code", "EN"), new XElement("Font", "font_english"),
        new XElement("Interface", new XElement("Record", new XElement("Key", "WorkshopWeatherName"), new XElement("Value", "Workshop Breeze")))))
        .Save(Path.Combine(workshopInput, "Language", "english.xml"));
    var workshopProject = new WorldProject(assets, Path.Combine(workshopInput, "world.xml"), true);
    var workshopExport = workshopProject.Export(temp, "WC_WorkshopWeather", "Workshop Weather");
    var workshopOutput = XDocument.Load(Path.Combine(workshopExport, "GameData", "world.xml"));
    var workshopOutputId = (string?)workshopOutput.Root!.Element("WorldSettings")!.Element("World")!.Element("WeatherEvent")!.Attribute("Id");
    if (workshopOutputId == null || workshopOutput.Root.Elements("WeatherEvent").SingleOrDefault(e => (string?)e.Attribute("Id") == workshopOutputId)?.Element("Fog") == null)
        throw new Exception("Imported sibling weather.xml event was not packaged with its exported world");
    var workshopLanguage = XDocument.Load(Path.Combine(workshopExport, "GameData", "Language", "english.xml"));
    if (workshopLanguage.Root?.Element("Interface")?.Elements("Record").Any(e => (string?)e.Element("Value") == "Workshop Breeze") != true)
        throw new Exception("Imported Workshop weather localization was not retained");
    var deletedWeather = Path.Combine(WeatherEffectLibrary.DirectoryPath, "WCWeather_CheckEffect.xml");
    WeatherEffectLibrary.Delete("WCWeather_CheckEffect");
    if (WeatherEffectLibrary.Load().Any(e => e.Id == "WCWeather_CheckEffect") || File.Exists(deletedWeather))
        throw new Exception("Deleting a saved weather effect did not remove it from the reusable library");
    var cloneProject = new WorldProject(assets, "Mars2/Mars2.xml");
    cloneProject.CreateWeather("WCWeather_CloneCheck", "Cloned Dust Storm", "MarsDustStorm");
    cloneProject.SaveWeatherToLibrary("WCWeather_CloneCheck");
    var clonedEffect = WeatherEffectLibrary.Load().Single(e => e.Id == "WCWeather_CloneCheck");
    if (clonedEffect.Definition.Element("StormEffect") == null || clonedEffect.Name != "Cloned Dust Storm")
        throw new Exception("Creating a custom weather effect from an installed storm did not preserve its definition");
    if (project.PointsOfInterest.Count != project.PointOfInterestCount || project.PointsOfInterest.Any(poi => !project.MapLayers.Any(layer => layer.Regions.Any(region => region.Id == poi.RegionId)))) throw new Exception("A point of interest has no matching map region");
    var rawSource = Path.Combine(temp, "height.raw");
    using (var raw = File.Create(rawSource)) raw.SetLength(4096L * 4096 * 4);
    project.SetTerrainSource("Heightmap", rawSource);
    if (!project.TerrainDescription.Contains("Selected source maps")) throw new Exception("Terrain source selection missing");
    using (var raw = new FileStream(rawSource, FileMode.Open, FileAccess.Write))
    {
        raw.Seek(-4, SeekOrigin.End);
        raw.Write(BitConverter.GetBytes(float.NaN), 0, 4);
    }
    try { project.SetTerrainSource("Heightmap", rawSource); throw new Exception("Non-finite heightmap sample accepted"); } catch (InvalidOperationException) { }
    using (var raw = new FileStream(rawSource, FileMode.Open, FileAccess.Write))
    {
        raw.Seek(-4, SeekOrigin.End);
        raw.Write(new byte[4], 0, 4);
    }
    project.SetTerrainSource("Heightmap", rawSource);
    var compiled = Path.Combine(temp, "compiled");
    Directory.CreateDirectory(compiled);
    File.WriteAllBytes(Path.Combine(compiled, "Terrain0.dat"), new byte[] { 1, 2, 3 });
    File.WriteAllBytes(Path.Combine(compiled, "Terrain44.dat"), new byte[] { 4, 5, 6 });
    File.WriteAllBytes(Path.Combine(compiled, "notes.txt"), new byte[] { 7 });
    project.SetCompiledTerrainFolder(compiled);
    project.WorldFields.Single(f => f.Label.Contains("Gravity")).Value = "-4.2";
    project.AtmosphereFields.First(f => f.Label.Contains("CarbonDioxide") && f.Label.EndsWith("Quantity")).Value = "9";
    project.WeatherFields.First(f => f.Label.Contains("CoolDown") && f.Label.EndsWith("Min")).Value = "4";
    if (project.WeatherTimings.Single(e => e.Id == "MarsDustStorm").CooldownMin != 4) throw new Exception("Weather timing preview did not read edited cooldown");
    project.OrbitFields.First(f => f.Label.EndsWith("SemiMajorAxisAu")).Value = "1.8";
    project.ResourceFields.First(f => f.Label.StartsWith("Surface resources [DefaultMinablesSurface]") && f.Label.EndsWith("OreDensity")).Value = "0.75";
    var advancedSurface = project.AdvancedFields.First(f => f.Label.EndsWith("/Surface/@Value") && f.Original == "true");
    advancedSurface.Value = "false";
    var advancedText = project.AdvancedFields.First(f => f.Label.EndsWith("/Description/@Key"));
    advancedText.Value = "Advanced Name";
    var target = project.Export(temp, "WC_Check", "Check World");
    var result = XDocument.Load(Path.Combine(target, "GameData", "world.xml"));
    var world = result.Root!.Element("WorldSettings")!.Element("World")!;
    if ((string?)world.Attribute("Id") != "WC_Check" || world.Element("Gravity")!.Value != "-4.2") throw new Exception("World export failed");
    if ((string?)world.Element("GlobalAtmosphere")!.Element("Gas")!.Elements("Gas").First().Attribute("Quantity") != "9") throw new Exception("Gas export failed");
    if ((string?)world.Element("WeatherEvent")!.Attribute("Id") != "WC_Check_Weather0" || (string?)result.Root.Element("WeatherEvent")!.Attribute("Id") != "WC_Check_Weather0") throw new Exception("Weather references not isolated");
    if ((string?)world.Element("PlayableBody")!.Attribute("Id") != "WC_Check_Body" || (string?)result.Root.Element("CelestialBodies")!.Element("CelestialBody")!.Element("Orbit")!.Attribute("SemiMajorAxisAu") != "1.8") throw new Exception("Orbit not isolated");
    if (world.Element("TerrainSettings") == null || !world.DescendantNodes().OfType<XComment>().Any()) throw new Exception("World content not preserved");
    if ((string?)world.Elements("Minables").Single(e => (string?)e.Attribute("Id") == "DefaultMinablesSurface").Attribute("OreDensity") != "0.75") throw new Exception("Resource density export failed");
    if ((string?)world.Descendants("Surface").First(e => (string?)e.Attribute("Value") == "false").Attribute("Value") != "false") throw new Exception("Advanced boolean edit failed");
    if ((string?)world.Element("Description")?.Attribute("Key") != "Advanced Name") throw new Exception("Advanced string edit failed");
    if (new FileInfo(Path.Combine(target, "TerrainSource", "Heightmap.raw")).Length != 4096L * 4096 * 4) throw new Exception("Terrain source was not packaged");
    if (!File.Exists(Path.Combine(target, "TerrainSource", "README.txt"))) throw new Exception("Terrain source instructions missing");
    if ((string?)world.Element("TerrainSettings")!.Attribute("Path") != "Worlds/WC_Check/Terrain") throw new Exception("Custom terrain path not rewritten");
    if (!File.Exists(Path.Combine(target, "GameData", "Worlds", "WC_Check", "Terrain", "Terrain44.dat")) || File.Exists(Path.Combine(target, "GameData", "Worlds", "WC_Check", "Terrain", "notes.txt"))) throw new Exception("Indexed terrain package was not filtered/copied");
    var wrongSize = Path.Combine(temp, "wrong.raw");
    File.WriteAllBytes(wrongSize, new byte[16]);
    try { project.SetTerrainSource("Heightmap", wrongSize); throw new Exception("Wrong-sized heightmap accepted"); } catch (InvalidOperationException) { }
    var second = project.Export(temp, "WC_Second", "Second World");
    var secondDoc = XDocument.Load(Path.Combine(second, "GameData", "world.xml"));
    if ((string?)secondDoc.Root!.Element("WorldSettings")!.Element("World")!.Element("PlayableBody")!.Attribute("Id") != "WC_Second_Body") throw new Exception("Repeated export did not update body reference");
    try { project.Export(temp, "WC_Check", "Check World"); throw new Exception("Existing mod was overwritten"); } catch (IOException) { }
    var invalid = new WorldProject(assets, "Mars2/Mars2.xml");
    invalid.WorldFields.Single(f => f.Label.Contains("Gravity")).Value = "not a number";
    if (!invalid.ValidateForExport(temp, "WC_Invalid", "Invalid").Any(issue => issue.IsError && issue.Text.Contains("Invalid number")))
        throw new Exception("The export readiness preview did not report an invalid edited numeric value");
    try { invalid.Export(temp, "WC_Invalid", "Invalid"); throw new Exception("Invalid value accepted"); } catch (InvalidOperationException) { }
    if (Directory.Exists(Path.Combine(temp, "WorldCreator_WC_Invalid"))) throw new Exception("Invalid export left a mod folder");
    var moon = new WorldProject(assets, "Lunar/Lunar.xml");
    moon.AddGas("Oxygen", 1);
    moon.AddTemperatureCurve();
    moon.AddOptionalSetting("World/GlobalAtmosphere/Volume");
    moon.AddOptionalSetting("World/GlobalAtmosphere/SolarRadiationTemperature");
    moon.AddOptionalSetting("World/GlobalAtmosphere/GHGTemperatureOffset");
    moon.AddOptionalSetting("World/GlobalAtmosphere/DensityOffset");
    moon.AtmosphereFields.Single(f => f.Label.Contains("[Oxygen]") && f.Label.EndsWith("Quantity")).Value = "10";
    try { moon.AddGas("Oxygen", 1); throw new Exception("Duplicate gas accepted"); } catch (InvalidOperationException) { }
    moon.RemoveWeather("SolarStorm");
    moon.AddWeather("MarsDustStorm");
    var moonPath = moon.Export(temp, "WC_Moon", "Moon Test");
    var moonDoc = XDocument.Load(Path.Combine(moonPath, "GameData", "world.xml"));
    var moonWorld = moonDoc.Root!.Element("WorldSettings")!.Element("World")!;
    if ((string?)moonWorld.Element("GlobalAtmosphere")!.Element("Gas")!.Element("Gas")!.Attribute("Quantity") != "10") throw new Exception("Adding gas failed");
    if (moonWorld.Element("GlobalAtmosphere")!.Element("Temperature")!.Elements("Key").Count() != 2) throw new Exception("Temperature curve missing");
    var moonAtmosphere = moonWorld.Element("GlobalAtmosphere")!;
    if ((string?)moonAtmosphere.Element("Volume")?.Attribute("Value") != "40000000000" || moonAtmosphere.Element("SolarRadiationTemperature") == null || moonAtmosphere.Element("GHGTemperatureOffset") == null || moonAtmosphere.Element("DensityOffset") == null)
        throw new Exception("Adding optional atmosphere groups failed");
    if (moonWorld.Elements("WeatherEvent").Count() != 1 || (string?)moonWorld.Element("WeatherEvent")!.Attribute("Id") != "MarsDustStorm") throw new Exception("Weather selection failed");
    if (!project.HasWeather("MarsDustStorm")) throw new Exception("Export mutated editor weather state");
    var negative = new WorldProject(assets, "Mars2/Mars2.xml");
    negative.AtmosphereFields.First(f => f.Label.Contains("CarbonDioxide") && f.Label.EndsWith("Quantity")).Value = "-1";
    try { negative.Export(temp, "WC_Negative", "Negative"); throw new Exception("Negative gas accepted"); } catch (InvalidOperationException) { }
    if (Directory.Exists(Path.Combine(temp, "WorldCreator_WC_Negative"))) throw new Exception("Negative gas produced a mod folder");
    var templates = new[] { "Lunar/Lunar.xml", "Europa/Europa.xml", "Mimas/MimasHerschel.xml", "Venus/Venus.xml", "Vulcan/VulcanV2.xml" };
    for (var i = 0; i < templates.Length; i++)
    {
        var template = new WorldProject(assets, templates[i]);
        if (string.IsNullOrWhiteSpace(template.MiniMapAssetPath) || !File.Exists(Path.Combine(assets, template.MiniMapAssetPath.Replace('/', Path.DirectorySeparatorChar))))
            throw new Exception("Missing minimap preview asset for " + templates[i]);
        if (template.MapLayers.Any(layer => !File.Exists(Path.Combine(assets, layer.TexturePath.Replace('/', Path.DirectorySeparatorChar)))) )
            throw new Exception("Missing map layer asset for " + templates[i]);
        if (template.PointsOfInterest.Any(poi => !template.MapLayers.Any(layer => layer.Regions.Any(region => region.Id == poi.RegionId))))
            throw new Exception("Point of interest has no matching map region for " + templates[i]);
        var folder = template.Export(temp, "WC_Template" + i, "Template " + i);
        var document = XDocument.Load(Path.Combine(folder, "GameData", "world.xml"));
        if (document.Root!.Element("WorldSettings")!.Element("World")!.Element("TerrainSettings") == null) throw new Exception("Missing terrain for " + templates[i]);
        if (templates[i] == "Venus/Venus.xml" && document.Descendants().Attributes("Path").Any(a => a.Value == "Worlds/Mars/Textures/macro_normal.png"))
            throw new Exception("Venus export retained a missing Mars texture path instead of using its packaged template asset");
        foreach (var pathAttribute in document.Descendants().Attributes("Path"))
        {
            var path = pathAttribute.Value.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(assets, path)) && !Directory.Exists(Path.Combine(assets, path)) &&
                !File.Exists(Path.Combine(folder, "GameData", path)) && !Directory.Exists(Path.Combine(folder, "GameData", path)))
                throw new Exception("Exported asset reference is missing for " + templates[i] + ": " + pathAttribute.Value);
        }
    }
    var mapProject = new WorldProject(assets, "Mars2/Mars2.xml");
    var poiMask = File.ReadAllBytes(Path.Combine(assets, "Worlds", "Mars2", "Textures", "mars_poi_regions.png"));
    mapProject.SetMapLayerTexture("MarsPoiRegions", poiMask);
    var mapExport = mapProject.Export(temp, "WC_MapMask", "Map Mask Test");
    var mapDocument = XDocument.Load(Path.Combine(mapExport, "GameData", "world.xml"));
    var mapPath = (string?)mapDocument.Descendants("RegionSet").Single(e => (string?)e.Attribute("Id") == "MarsPoiRegions").Element("Texture")!.Attribute("Path");
    if (mapPath != "Worlds/WC_MapMask/Textures/MapLayer_2.png" || !File.ReadAllBytes(Path.Combine(mapExport, "GameData", mapPath!.Replace('/', Path.DirectorySeparatorChar))).SequenceEqual(poiMask))
        throw new Exception("Custom map mask was not rewritten and packaged with the world");
    var loadedMap = new WorldProject(assets, Path.Combine(mapExport, "GameData", "world.xml"), true);
    if (loadedMap.MapLayers.Single(layer => layer.Id == "MarsPoiRegions").TexturePath != Path.Combine(mapExport, "GameData", mapPath.Replace('/', Path.DirectorySeparatorChar)))
        throw new Exception("Loaded custom map texture was not resolved from the source mod");
    var reExport = loadedMap.Export(temp, "WC_MapReload", "Map Reload Test");
    var reExportDoc = XDocument.Load(Path.Combine(reExport, "GameData", "world.xml"));
    var reExportMapPath = (string?)reExportDoc.Descendants("RegionSet").Single(e => (string?)e.Attribute("Id") == "MarsPoiRegions").Element("Texture")!.Attribute("Path");
    if (reExportMapPath != mapPath || !File.ReadAllBytes(Path.Combine(reExport, "GameData", reExportMapPath!.Replace('/', Path.DirectorySeparatorChar))).SequenceEqual(poiMask))
        throw new Exception("Re-export of loaded custom map did not preserve and package its texture");

    var blankGeo = WorldProject.CreateBlank(assets);
    var geographicLayerId = blankGeo.AddGeographicLayer(poiMask);
    var newRegionId = blankGeo.AddGeographicRegion(geographicLayerId);
    var newRegion = blankGeo.MapLayers.Single(layer => layer.Id == geographicLayerId).Regions.Single(region => region.Id == newRegionId);
    blankGeo.SetMapRegionName(newRegion, "Northern Highlands");
    var geoExport = blankGeo.Export(temp, "WC_GeographicMap", "Geographic Map Test");
    var geoDocument = XDocument.Load(Path.Combine(geoExport, "GameData", "world.xml"));
    var geoWorld = geoDocument.Root!.Element("WorldSettings")!.Element("World")!;
    var geoSet = geoDocument.Descendants("RegionSet").Single(element => (string?)element.Attribute("Id") == geographicLayerId && element.Element("Texture") != null);
    var geoTexturePath = (string?)geoSet.Element("Texture")!.Attribute("Path");
    if ((string?)geoWorld.Element("GeographicRegionData")?.Element("RegionSet")?.Attribute("Id") != geographicLayerId ||
        geoSet.Elements("Region").Count() != 2 || geoSet.Elements("Region").Any(region => string.IsNullOrWhiteSpace((string?)region.Attribute("Id"))) ||
        geoSet.Elements("Region").Select(region => ((int?)region.Attribute("R") ?? -1, (int?)region.Attribute("G") ?? -1, (int?)region.Attribute("B") ?? -1)).Distinct().Count() != 2 ||
        string.IsNullOrWhiteSpace(geoTexturePath) || !File.Exists(Path.Combine(geoExport, "GameData", geoTexturePath.Replace('/', Path.DirectorySeparatorChar))))
        throw new Exception("A new blank geographic layer did not export with its RGB24 region mask and texture");
    var geoLanguage = XDocument.Load(Path.Combine(geoExport, "GameData", "Language", "english.xml"));
    if (geoLanguage.Descendants("Record").Count(record => (string?)record.Element("Key") == newRegion.NameKey && (string?)record.Element("Value") == "Northern Highlands") != 1)
        throw new Exception("A new geographic region's editable name was not exported to English localization");
    var loadedWeatherOrbit = new WorldProject(assets, Path.Combine(target, "GameData", "world.xml"), true);
    var loadedExport = loadedWeatherOrbit.Export(temp, "WC_Reload", "Reloaded Check World");
    var loadedDocument = XDocument.Load(Path.Combine(loadedExport, "GameData", "world.xml"));
    if (loadedDocument.Root!.Elements("WeatherEvent").Count() != 1 || loadedDocument.Root.Element("CelestialBodies")?.Element("CelestialBody") == null)
        throw new Exception("Loading and re-exporting a custom world dropped its local weather or orbit definitions");
    Console.WriteLine("World export, preservation, map masks, references, collision and invalid input checks passed.");
}
finally
{
    Environment.SetEnvironmentVariable("WORLD_CREATOR_WEATHER_LIBRARY", previousWeatherLibrary);
    Directory.Delete(temp, true);
}
