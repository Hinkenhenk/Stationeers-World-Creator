using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Renders with only the marker's light and a dim neutral ambient floor, restoring the game scene even if rendering fails.</summary>
    private void RenderIsolatedOrbitSpheres()
    {
        WithIsolatedOrbitLighting(() =>
        {
            RenderOrbitSphere(_orbitPrimarySurfaceTexture!, _orbitSunRenderTexture!, emissive: _orbitPrimaryIsBlackHole || _orbitPrimaryIsStar);
            RenderOrbitSphere(_orbitPlanetSurfaceTexture!, _orbitPlanetRenderTexture!, emissive: false);
        });
    }

    /// <summary>Limits a synchronous preview render to its own light and restores the main-menu lighting on every exit.</summary>
    private void WithIsolatedOrbitLighting(Action render)
    {
        const int previewMask = 1 << 31;
        var lights = FindObjectsOfType<Light>()
            .Where(light => light != _orbitSphereLight && (light.cullingMask & previewMask) != 0)
            .Select(light => (Light: light, Mask: light.cullingMask)).ToArray();
        var ambientMode = RenderSettings.ambientMode;
        var ambientLight = RenderSettings.ambientLight;
        try
        {
            foreach (var entry in lights) entry.Light.cullingMask = entry.Mask & ~previewMask;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.035f, .035f, .035f);
            render();
        }
        finally
        {
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            foreach (var entry in lights)
            {
                if (entry.Light == null) continue;
                entry.Light.cullingMask = entry.Mask;
                Debug.Assert(entry.Light.cullingMask == entry.Mask, "Orbit preview did not restore a scene light mask.");
            }
            Debug.Assert(RenderSettings.ambientMode == ambientMode && RenderSettings.ambientLight == ambientLight,
                "Orbit preview did not restore scene ambient lighting.");
        }
    }
}
