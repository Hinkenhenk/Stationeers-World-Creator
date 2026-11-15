using System;
using System.Globalization;
using System.Linq;
using Assets.Scripts.Atmospherics;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _phaseGasSelection;
    /// <summary>Shows gas-property reference values independently of the world's gas mixture.</summary>
    private void DrawGasPhaseReference()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("GAS PHASE REFERENCE", _sectionStyle!);
        GUILayout.Label("Properties calculated by the installed game. No loaded world is required.", _mutedStyle!);
        var gases = Chemistry.Gases.Where(mole => Mole.MatterState(mole.Type) == AtmosphereHelper.MatterState.Gas && MoleHelper.CanCondense(mole.Type)).ToArray();
        if (gases.Length > 0)
        {
            _phaseGasSelection = Mathf.Clamp(_phaseGasSelection, 0, gases.Length - 1);
            _phaseGasSelection = NativeDropdown(
                "gas-phase-reference",
                _phaseGasSelection,
                gases.Select(mole => HumanizeXmlName(mole.Type.ToString())).ToArray(),
                _buttonStyle!,
                GUILayout.MaxWidth(300)
            );
            var gas = gases[_phaseGasSelection];
            try
            {
                var triplePressure = gas.MinLiquidPressure();
                var criticalPressure = gas.MinimumLiquidPressureAtMaxTemperature();
                HelpLabel(
                    new GUIContent(
                    "Triple point  ⓘ",
                    "Game-derived temperature and pressure at the lower boundary of liquid stability. The mixture's actual partial pressures and temperature determine phase behaviour."
                ),
                    _mutedStyle!
                );
                GUILayout.Label(MoleHelper.EvaporationTemperature(gas.Type, triplePressure).ToDouble().ToString("0.##", CultureInfo.InvariantCulture) + " K · " + triplePressure.ToDouble().ToString("0.###", CultureInfo.InvariantCulture) + " kPa");
                HelpLabel(
                    new GUIContent(
                    "Critical point  ⓘ",
                    "Game-derived upper end of the liquid-vapour boundary. These are reference properties, not a condensation forecast for the project's mixture."
                ),
                    _mutedStyle!
                );
                GUILayout.Label(MoleHelper.EvaporationTemperature(gas.Type, criticalPressure).ToDouble().ToString("0.##", CultureInfo.InvariantCulture) + " K · " + criticalPressure.ToDouble().ToString("0.###", CultureInfo.InvariantCulture) + " kPa");
            }
            catch (Exception error)
            {
                GUILayout.Label("Reference unavailable: " + error.GetBaseException().Message, _mutedStyle!);
            }
        }
        else
            GUILayout.Label("No condensable gas definitions are available.", _mutedStyle!);
        GUILayout.Label(
            "This reads the same gas-property calculations as the game's gas information tool. It does not change the project or simulate phase changes over time.",
            _mutedStyle!
        );
        GUILayout.EndVertical();
    }
}
