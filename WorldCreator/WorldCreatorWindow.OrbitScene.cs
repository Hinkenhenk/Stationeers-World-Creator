using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private string? _orbitSystemError;
    private GameObject? _orbitSystemRoot;
    private MeshRenderer? _orbitSystemPrimary;
    private Material? _orbitSystemPrimaryMaterial;
    private readonly List<LineRenderer> _orbitSystemLines = new List<LineRenderer>();
    private readonly List<Material> _orbitSystemLineMaterials = new List<Material>();
    private RenderTexture? _orbitSystemTexture;
    private readonly Vector3[] _orbitSystemAxisLabels = new Vector3[3];
    private static readonly string[] OrbitAxisNames = { "X", "Y", "Z" };
    private static readonly Vector3[] OrbitAxes = { Vector3.right, Vector3.up, Vector3.forward };

    /// <summary>Renders the orbit, parent and playable body together; the camera alone selects Above, Side or 3D.</summary>
    private double DrawOrbitalSystemChart(Rect chart, double a, double e)
    {
        var anomaly = SolveEccentricAnomaly(_orbitPhase * Math.PI / 180, e);
        if (Event.current.type == EventType.Repaint && _orbitSystemError == null)
        {
            try
            {
                PrepareOrbitSphereResources();
                PrepareOrbitSystemScene(chart);
                RenderOrbitSystemScene(chart, e, anomaly);
            }
            catch (Exception exception) when (!(exception is ExitGUIException))
            {
                DestroyOrbitSystemScene();
                _orbitSystemError = "Preview unavailable: " + exception.Message + " Use Reset view to retry.";
                Debug.LogException(exception);
            }
        }
        if (_orbitSystemError != null)
        {
            GUI.Label(new Rect(12, 12, Math.Max(1, chart.width - 24), 80), _orbitSystemError, _mutedStyle!);
            return a * (1 - e * Math.Cos(anomaly));
        }
        if (_orbitSystemTexture != null) GUI.DrawTexture(new Rect(0, 0, chart.width, chart.height), _orbitSystemTexture, ScaleMode.StretchToFill, true);
        for (var i = 0; i < _orbitSystemAxisLabels.Length; i++)
        {
            var point = _orbitSystemAxisLabels[i];
            if (point.z > 0) GUI.Label(new Rect(point.x * chart.width + 3, (1 - point.y) * chart.height - 9, 20, 18), OrbitAxisNames[i], _mutedStyle!);
        }
        return a * (1 - e * Math.Cos(anomaly));
    }

    /// <summary>Creates real 3D parent and line renderers and a depth-buffered target matching the chart's aspect.</summary>
    private void PrepareOrbitSystemScene(Rect chart)
    {
        try
        {
            if (_orbitSphereMaterial == null) throw new InvalidOperationException("The orbital preview material is unavailable.");
            if (_orbitSystemRoot == null)
            {
                _orbitSystemRoot = new GameObject("World Creator orbital scene") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
                _orbitSystemRoot.SetActive(false);
                var primary = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                primary.name = "Orbit parent";
                primary.hideFlags = HideFlags.HideAndDontSave;
                primary.layer = 31;
                primary.transform.SetParent(_orbitSystemRoot.transform, false);
                Destroy(primary.GetComponent("SphereCollider"));
                _orbitSystemPrimary = primary.GetComponent<MeshRenderer>();
                _orbitSystemPrimary.lightProbeUsage = LightProbeUsage.Off;
                _orbitSystemPrimary.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _orbitSystemPrimary.shadowCastingMode = ShadowCastingMode.Off;
                _orbitSystemPrimary.receiveShadows = false;
                _orbitSystemPrimaryMaterial = new Material(_orbitSphereMaterial!) { hideFlags = HideFlags.HideAndDontSave };
                _orbitSystemPrimary.sharedMaterial = _orbitSystemPrimaryMaterial;
                CreateOrbitSystemLine("Orbit path", MenuBlue, true);
                CreateOrbitSystemLine("X axis", new Color(.95f, .38f, .38f), false);
                CreateOrbitSystemLine("Y axis", new Color(.35f, .58f, 1f), false);
                CreateOrbitSystemLine("Z axis", new Color(.38f, .83f, .51f), false);
            }
            var width = Mathf.Clamp(Mathf.CeilToInt(chart.width), 32, 4096);
            var height = Mathf.Clamp(Mathf.CeilToInt(chart.height), 32, 4096);
            if (_orbitSystemTexture != null && _orbitSystemTexture.width == width && _orbitSystemTexture.height == height) return;
            if (_orbitSystemTexture != null) { _orbitSystemTexture.Release(); Destroy(_orbitSystemTexture); }
            _orbitSystemTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { name = "World Creator orbital scene", hideFlags = HideFlags.HideAndDontSave, antiAliasing = 2 };
            if (!_orbitSystemTexture.Create()) throw new InvalidOperationException("The orbital preview render target could not be allocated.");
        }
        catch
        {
            DestroyOrbitSystemScene();
            throw;
        }
    }

    /// <summary>Adds a depth-tested world-space line so paths and axes are occluded by the same spheres as each other.</summary>
    private void CreateOrbitSystemLine(string name, Color colour, bool loop)
    {
        var item = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
        item.transform.SetParent(_orbitSystemRoot!.transform, false);
        var line = item.AddComponent<LineRenderer>();
        var material = new Material(_orbitSphereMaterial!) { hideFlags = HideFlags.HideAndDontSave };
        _orbitSystemLineMaterials.Add(material);
        material.mainTexture = Texture2D.whiteTexture;
        material.color = colour;
        // Reuse the working sphere shader; stripped game builds may not contain Unlit/Color.
        if (material.HasProperty("_EmissionColor"))
        {
            material.color = Color.black;
            material.SetColor("_EmissionColor", colour);
            material.EnableKeyword("_EMISSION");
        }
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = loop;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = loop ? 240 : 2;
        _orbitSystemLines.Add(line);
    }

    /// <summary>Uses identical world coordinates for path and planet, real camera presets and a parent-positioned light.</summary>
    private void RenderOrbitSystemScene(Rect chart, double e, double anomaly)
    {
        var camera = _orbitSphereCamera!;
        var planet = _orbitSphereObject!.transform;
        var light = _orbitSphereLight!.transform;
        var cameraPosition = camera.transform.position;
        var cameraRotation = camera.transform.rotation;
        var cameraSize = camera.orthographicSize;
        var cameraAspect = camera.aspect;
        var target = camera.targetTexture;
        var activeTarget = RenderTexture.active;
        var planetPosition = planet.position;
        var planetRotation = planet.rotation;
        var planetScale = planet.localScale;
        var lightPosition = light.position;
        var lightRotation = light.rotation;
        var rendererEnabled = _orbitSphereRenderer!.enabled;
        try
        {
            ConfigureOrbitSystemScene(chart, e, anomaly);
            _orbitSystemRoot!.SetActive(true);
            _orbitSphereRenderer.enabled = true;
            camera.targetTexture = _orbitSystemTexture;
            WithIsolatedOrbitLighting(camera.Render);
        }
        finally
        {
            _orbitSystemRoot!.SetActive(false);
            _orbitSphereRenderer.enabled = rendererEnabled;
            planet.SetPositionAndRotation(planetPosition, planetRotation);
            planet.localScale = planetScale;
            light.SetPositionAndRotation(lightPosition, lightRotation);
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            camera.orthographicSize = cameraSize;
            camera.aspect = cameraAspect;
            camera.targetTexture = target;
            RenderTexture.active = activeTarget;
            _orbitSphereTexturesDirty = true;
        }
    }

    /// <summary>Updates scene geometry and materials without moving world objects when only the view preset changes.</summary>
    private void ConfigureOrbitSystemScene(Rect chart, double e, double anomaly)
    {
        var inclination = OrbitAngle("Inclination");
        var node = OrbitAngle("LongitudeOfAscendingNode");
        var periapsis = OrbitAngle("ArgumentOfPeriapsis");
        var path = _orbitSystemLines[0];
        for (var i = 0; i < path.positionCount; i++)
            path.SetPosition(i, OrbitScenePosition(i * Math.PI * 2 / path.positionCount, e, inclination, node, periapsis));
        _orbitSphereObject!.transform.position = OrbitScenePosition(anomaly, e, inclination, node, periapsis);
        try { _orbitSphereObject.transform.rotation = BodyRotationAtPhase(_orbitPhase); }
        catch (InvalidOperationException) { _orbitSphereObject.transform.rotation = Quaternion.identity; }
        _orbitSphereMaterial!.mainTexture = _orbitPlanetSurfaceTexture;
        _orbitSystemPrimaryMaterial!.shader = (_orbitPrimaryIsStar || _orbitPrimaryIsBlackHole ? Shader.Find("Unlit/Texture") : _orbitSphereMaterial.shader) ?? _orbitSphereMaterial.shader;
        _orbitSystemPrimaryMaterial.mainTexture = _orbitPrimarySurfaceTexture;
        var camera = _orbitSphereCamera!;
        camera.aspect = chart.width / chart.height;
        camera.orthographicSize = (float)OrbitPreviewFraming.HalfHeight(e, camera.aspect, _orbitZoom);
        var orientation = Quaternion.Inverse(OrbitCameraRotation(_orbitView, _orbitGlobeRotation));
        camera.transform.SetPositionAndRotation(orientation * new Vector3(0, 0, -6), orientation);
        var pixelSize = camera.orthographicSize * 2 / chart.height;
        _orbitSphereObject.transform.localScale = Vector3.one * (19 * _orbitZoom * pixelSize);
        _orbitSystemPrimary!.transform.localScale = Vector3.one * (30 * _orbitZoom * pixelSize);
        for (var i = 0; i < OrbitAxes.Length; i++)
        {
            _orbitSystemLines[i + 1].SetPosition(0, Vector3.zero);
            _orbitSystemLines[i + 1].SetPosition(1, OrbitAxes[i] * .35f);
            _orbitSystemAxisLabels[i] = camera.WorldToViewportPoint(OrbitAxes[i] * .35f);
        }
        foreach (var line in _orbitSystemLines) line.startWidth = line.endWidth = pixelSize * 1.5f;
        _orbitSphereLight!.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(_orbitSphereObject.transform.position));
    }

    /// <summary>Converts the native orbit plane to Unity XYZ once for both the path and the moving sphere.</summary>
    private static Vector3 OrbitScenePosition(double anomaly, double e, double inclination, double node, double periapsis)
    {
        var point = CalculateOrbitPosition(anomaly, e, inclination, node, periapsis);
        return new Vector3(point.x, point.z, point.y);
    }

    /// <summary>Releases the orbital target, parent object and line materials when the editor closes.</summary>
    private void DestroyOrbitSystemScene()
    {
        if (_orbitSystemRoot != null)
        {
            _orbitSystemRoot.SetActive(false);
            Destroy(_orbitSystemRoot);
        }
        _orbitSystemRoot = null;
        _orbitSystemPrimary = null;
        if (_orbitSystemPrimaryMaterial != null) Destroy(_orbitSystemPrimaryMaterial);
        _orbitSystemPrimaryMaterial = null;
        foreach (var material in _orbitSystemLineMaterials) Destroy(material);
        _orbitSystemLineMaterials.Clear();
        _orbitSystemLines.Clear();
        if (_orbitSystemTexture != null)
        {
            if (_orbitSphereCamera != null && _orbitSphereCamera.targetTexture == _orbitSystemTexture) _orbitSphereCamera.targetTexture = null;
            if (RenderTexture.active == _orbitSystemTexture) RenderTexture.active = null;
            _orbitSystemTexture.Release();
            Destroy(_orbitSystemTexture);
        }
        _orbitSystemTexture = null;
        Array.Clear(_orbitSystemAxisLabels, 0, _orbitSystemAxisLabels.Length);
    }
}
