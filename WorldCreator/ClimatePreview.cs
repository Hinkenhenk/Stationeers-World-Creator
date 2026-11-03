using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.UI.ImGuiUi;
using UnityEngine;

namespace WorldCreator
{
    // A creation preview owns its data and curves; it never registers or edits live game settings.
    internal sealed class ClimatePreview
    {
        internal sealed class GasResponse
        {
            internal readonly Chemistry.GasType Type;
            internal readonly string Id;
            internal readonly AnimationCurve Curve;
            internal GasResponse(Chemistry.GasType type, AnimationCurve curve)
            {
                Type = type;
                var name = Enum.GetName(typeof(Chemistry.GasType), type) ?? type.ToString();
                Id = typeof(Chemistry.GasType).GetField(name)?.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? name;
                Curve = curve;
            }
        }

        private static readonly XmlSerializer AtmosphereSerializer = new XmlSerializer(typeof(GlobalAtmosphereData), new XmlRootAttribute("GlobalAtmosphere"));
        private static readonly XmlSerializer ResponseSerializer = new XmlSerializer(typeof(TerraformingGasCurveData), new XmlRootAttribute("GHGIndex"));
        private GlobalAtmosphereData _data = null!;
        internal GasResponse[] Responses { get; }
        internal string ResponseSource { get; }
        internal GlobalGasMix Mixture { get; private set; } = null!;
        internal float GreenhouseIndex { get; private set; }
        internal float Density { get; private set; }
        internal IReadOnlyList<GlobalMoleData> Gases => _data.GlobalGasMixData.GlobalMoleDatas;

        internal ClimatePreview(string assets)
        {
            if (TerraForming.TerraformingGasCurves.Count > 0)
            {
                Responses = TerraForming.TerraformingGasCurves.Select(pair => new GasResponse(pair.Key, pair.Value.ToAnimationCurve())).ToArray();
                ResponseSource = "Loaded game greenhouse definitions";
            }
            else
            {
                var path = Path.Combine(assets, "Data", "terraforming.xml");
                Responses = XDocument.Load(path).Root!.Elements("GHGIndex").Select(element =>
                {
                    using (var reader = element.CreateReader())
                    {
                        var definition = (TerraformingGasCurveData)ResponseSerializer.Deserialize(reader)!;
                        // Init() on this derived type would register it in the game's shared dictionary.
                        return new GasResponse(definition.GasType, definition.ToAnimationCurve());
                    }
                }).ToArray();
                ResponseSource = "Installed terraforming.xml (no loaded greenhouse definitions)";
            }
            if (Responses.Length == 0) throw new InvalidDataException("No greenhouse response curves are available for this preview.");
            Responses = Responses.OrderBy(response => response.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal void UpdateAtmosphere(XElement snapshot)
        {
            using (var reader = snapshot.CreateReader()) _data = (GlobalAtmosphereData)AtmosphereSerializer.Deserialize(reader)!;
            if (_data.Volume.Value <= 0 || double.IsNaN(_data.Volume.Value) || double.IsInfinity(_data.Volume.Value))
                throw new InvalidDataException("Atmosphere volume must be a finite number above zero.");
            if (Gases.Any(gas => gas.Type == Chemistry.GasType.Undefined || gas.Quantity < 0 || !IsFinite(gas.Quantity)) || Gases.GroupBy(gas => gas.Type).Any(group => group.Count() > 1))
                throw new InvalidDataException("Gas amounts must be finite and non-negative, with one entry per gas.");
            _data.Init();
            Mixture = GlobalGasMix.Create(_data);
            Density = (float)IdealGas.GetMilliMolesPerLitre(Mixture.Volume, Mixture.TotalQuantityGas());
            GreenhouseIndex = Responses.Sum(response => Contribution(response));
            if (!IsFinite(Density) || !IsFinite(GreenhouseIndex)) throw new InvalidDataException("The atmosphere produces an invalid density or greenhouse index.");
        }

        internal float Concentration(Chemistry.GasType gas) => (float)IdealGas.GetMilliMolesPerLitre(Mixture.Volume, Mixture.Get(gas));
        internal float Contribution(GasResponse response) => response.Curve.Evaluate(Concentration(response.Type));

        // Use the game's component evaluators. Its aggregate helper also reads weather and player heat from the live world.
        internal float[] Evaluate(float angle, float sunlight)
        {
            var baseline = _data.GetSolarAngleTemperature(angle).ToFloat();
            var solar = _data.GetSolarDistanceTemperatureOffset(angle, sunlight).ToFloat();
            var greenhouse = _data.GetGHGTemperatureOffset(angle, GreenhouseIndex).ToFloat();
            var density = _data.GetDensityOffset(angle, Density).ToFloat();
            var result = new[] { baseline + solar + greenhouse + density, baseline, solar, greenhouse, density };
            if (result.Any(value => !IsFinite(value))) throw new InvalidDataException("A temperature curve produced a non-finite value. Check its points and shape settings.");
            return result;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
