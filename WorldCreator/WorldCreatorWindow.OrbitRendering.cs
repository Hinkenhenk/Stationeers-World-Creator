using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private Vector3 _orbitLightDirectionView = Vector3.forward;
    private Vector3 _orbitRenderedLightDirectionView = Vector3.forward;
    private Texture2D? _orbitPlanetSurfaceTexture;
    private Texture2D? _orbitPrimarySurfaceTexture;
    private string? _orbitPrimaryTexturePath;
    private RenderTexture? _orbitSunRenderTexture;
    private RenderTexture? _orbitPlanetRenderTexture;
    private Camera? _orbitSphereCamera;
    private GameObject? _orbitSphereObject;
    private MeshRenderer? _orbitSphereRenderer;
    private Light? _orbitSphereLight;
    private Material? _orbitSphereMaterial;
    private Quaternion _orbitSphereRenderedRotation = Quaternion.identity;
    private bool _orbitSphereTexturesDirty = true;
    private byte[]? _orbitSourceSurfaceData;
    private string? _orbitPrimaryBodyIdRendered;
    private bool _orbitPrimaryIsBlackHole;
    private bool _orbitPrimaryIsStar;
    private string? _orbitGroundTexturePath;
    private byte[]? _orbitGroundTextureData;
    /// <summary>Refreshes sphere textures and render resources, then renders only when preview inputs changed.</summary>
    private void EnsureOrbitSpherePreviews()
    {
        RefreshPrimaryBodyTexture();
        RefreshPlayableBodyTexture();
        if (_orbitPrimarySurfaceTexture == null)
            _orbitPrimarySurfaceTexture = CreateSunTexture();
        if (_orbitSunRenderTexture == null)
            _orbitSunRenderTexture = CreateOrbitRenderTexture("World Creator sun sphere");
        if (_orbitPlanetRenderTexture == null)
            _orbitPlanetRenderTexture = CreateOrbitRenderTexture("World Creator planet sphere");
        if (_orbitSphereCamera == null)
        {
            CreateOrbitSphereScene();
        }

        if (_orbitSphereCamera == null || _orbitSphereObject == null || _orbitSphereRenderer == null || _orbitSphereMaterial == null)
            return;
        Quaternion bodyRotation;
        try { bodyRotation = BodyRotationAtPhase(_orbitPhase); }
        catch (InvalidOperationException) { bodyRotation = Quaternion.identity; }
        var viewRotation = _orbitGlobeRotation;
        var sphereRotation = viewRotation * (_orbitView == 3 ? Quaternion.identity : bodyRotation);
        var sourceDirection = CurrentPrimaryLightDirection();
        if (_orbitView == 3)
        {
            try
            {
                var lighting = WorldProject.CalculateRotationLighting(_orbitFrameRotation!, _orbitPhase, 0, 0, sourceDirection.x, sourceDirection.y, sourceDirection.z);
                sourceDirection = new Vector3((float)lighting.BodySunDirection.X, (float)lighting.BodySunDirection.Y, (float)lighting.BodySunDirection.Z);
            }
            catch (InvalidOperationException) { sourceDirection = Vector3.back; }
        }
        var lightDirectionView = (viewRotation * sourceDirection).normalized;
        if (!_orbitSphereTexturesDirty && Quaternion.Angle(_orbitSphereRenderedRotation, sphereRotation) < .01f && Vector3.Angle(_orbitRenderedLightDirectionView, lightDirectionView) < .01f)
            return;
        _orbitSphereObject.transform.rotation = sphereRotation;
        _orbitLightDirectionView = lightDirectionView;
        _orbitSphereLight!.transform.position = _orbitLightDirectionView * 3.5f;
        _orbitSphereLight.transform.rotation = Quaternion.LookRotation(-_orbitLightDirectionView);
        var rendererWasEnabled = _orbitSphereRenderer.enabled;
        try
        {
            _orbitSphereRenderer.enabled = true;
            RenderOrbitSphere(_orbitPrimarySurfaceTexture!, _orbitSunRenderTexture!, emissive: _orbitPrimaryIsBlackHole || _orbitPrimaryIsStar);
            RenderOrbitSphere(_orbitPlanetSurfaceTexture!, _orbitPlanetRenderTexture!, emissive: false);
        }
        finally
        {
            _orbitSphereRenderer.enabled = rendererWasEnabled;
        }

        _orbitSphereRenderedRotation = sphereRotation;
        _orbitRenderedLightDirectionView = _orbitLightDirectionView;
        _orbitSphereTexturesDirty = false;
    }

    /// <summary>Resolves and caches the loaded world surface image from local, workshop or game assets.</summary>
    private byte[]? LoadOrbitWorldGroundTexture()
    {
        var sourcePath = _project?.MiniMapAssetPath;
        if (string.IsNullOrWhiteSpace(sourcePath))
            return null;
        var relativePath = sourcePath.Replace('/', Path.DirectorySeparatorChar);
        var path = sourcePath;
        if (!Path.IsPathRooted(sourcePath))
        {
            var modPath = string.IsNullOrEmpty(_project?.SourceAssetRoot) ? string.Empty : Path.Combine(_project.SourceAssetRoot, relativePath);
            var gameDataPath = string.IsNullOrEmpty(_project?.SourceGameDataRoot) ? string.Empty : Path.Combine(_project.SourceGameDataRoot, relativePath);
            var gamePath = Path.Combine(Application.dataPath, "StreamingAssets", relativePath);
            path = File.Exists(modPath) ? modPath : File.Exists(gameDataPath) ? gameDataPath : gamePath;
        }

        if (string.Equals(path, _orbitGroundTexturePath, StringComparison.OrdinalIgnoreCase))
            return _orbitGroundTextureData;
        _orbitGroundTexturePath = path;
        try
        {
            _orbitGroundTextureData = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            _orbitGroundTextureData = null;
        }

        return _orbitGroundTextureData;
    }

    /// <summary>Renders a textured sphere into its target while restoring material, lighting and render state.</summary>
    private void RenderOrbitSphere(Texture2D surface, RenderTexture target, bool emissive)
    {
        var material = _orbitSphereMaterial!;
        var camera = _orbitSphereCamera!;
        var originalShader = material.shader;
        var originalTarget = camera.targetTexture;
        try
        {
            if (emissive)
            {
                var unlit = Shader.Find("Unlit/Texture");
                if (unlit != null)
                    material.shader = unlit;
            }

            material.mainTexture = surface;
            if (material.HasProperty("_Color"))
                material.color = Color.white;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emissive ? new Color(.75f, .31f, .04f) : Color.black);
            if (emissive)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
            camera.targetTexture = target;
            camera.Render();
        }
        finally
        {
            camera.targetTexture = originalTarget;
            material.shader = originalShader;
        }
    }

    /// <summary>Creates an isolated transparent render target for an orbital body preview.</summary>
    private static RenderTexture CreateOrbitRenderTexture(string name)
    {
        var texture = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 2
        };
        texture.Create();
        return texture;
    }

    /// <summary>Decodes image bytes into a preview texture and releases a failed decode.</summary>
    private static Texture2D? LoadOrbitTexture(byte[] data)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        if (texture.LoadImage(data))
            return texture;
        Destroy(texture);
        return null;
    }

    /// <summary>Reads an existing image file for the orbital preview, otherwise returns no texture.</summary>
    private static Texture2D? LoadOrbitTextureFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return File.Exists(path) ? LoadOrbitTexture(File.ReadAllBytes(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Creates a fallback surface using the configured primary-body colour.</summary>
    private static Texture2D CreatePrimaryBodyTexture((float R, float G, float B)? bodyColor)
    {
        const int width = 256;
        const int height = 128;
        var baseColor = bodyColor.HasValue ? new Color(bodyColor.Value.R, bodyColor.Value.G, bodyColor.Value.B) : new Color(.43f, .38f, .31f);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "World Creator primary-body surface",
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var noise = Mathf.PerlinNoise(x * .045f, y * .045f) * .28f + Mathf.PerlinNoise(x * .012f + 11f, y * .012f) * .32f;
                pixels[y * width + x] = Color.Lerp(baseColor * .65f, baseColor * 1.35f, noise);
            }

        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    /// <summary>Creates the fallback black-hole appearance used when no body texture is available.</summary>
    private static Texture2D CreateBlackHoleTexture()
    {
        const int width = 256;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "World Creator black-hole event horizon",
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var latitude = Math.Abs((y + .5f) / height - .5f) * 2f;
                var band = Mathf.Clamp01(1f - Mathf.Abs(latitude - .12f) / .11f);
                var turbulence = Mathf.PerlinNoise(x * .09f, y * .045f);
                pixels[y * width + x] = Color.Lerp(new Color(.008f, .006f, .015f), new Color(1f, .21f, .035f), band * (.55f + turbulence * .45f));
            }

        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    /// <summary>Creates the blue fallback planet surface used when no ground image can be loaded.</summary>
    private static Texture2D CreateBluePlanetTexture()
    {
        const int width = 256;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "World Creator blue planet surface",
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var noise = Mathf.PerlinNoise(x * .035f, y * .035f) * .12f + Mathf.PerlinNoise(x * .012f + 13.7f, y * .012f) * .1f;
                var land = Mathf.PerlinNoise(x * .022f + 21f, y * .022f + 5f) > .63f;
                var shade = .86f + noise;
                pixels[y * width + x] = land ? new Color(.20f * shade, .54f * shade, .67f * shade) : new Color(.12f * shade, .43f * shade, .62f * shade);
            }

        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    /// <summary>Creates the fallback glowing stellar surface for a primary star.</summary>
    private static Texture2D CreateSunTexture()
    {
        const int width = 256;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "World Creator solar photosphere",
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var grain = Mathf.PerlinNoise(x * .075f, y * .075f) * .5f + Mathf.PerlinNoise(x * .025f + 7f, y * .025f + 11f) * .5f;
                pixels[y * width + x] = Color.Lerp(new Color(1f, .24f, .025f), new Color(1f, .82f, .22f), .35f + grain * .65f);
            }

        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    /// <summary>Reloads the primary texture and appearance flags when its ID or source path changes.</summary>
    private void RefreshPrimaryBodyTexture()
    {
        var primaryId = _project?.PrimaryBodyId ?? "Sun";
        var primaryTexturePath = _project?.GetPrimaryBodyTexturePath(primaryId);
        var primaryIsBlackHole = _project?.IsBlackHolePrimary(primaryId) ?? false;
        var primaryIsStar = !primaryIsBlackHole && (_project?.IsStarPrimary(primaryId) ?? false);
        if (!string.Equals(primaryId, _orbitPrimaryBodyIdRendered, StringComparison.Ordinal) || !string.Equals(primaryTexturePath, _orbitPrimaryTexturePath, StringComparison.OrdinalIgnoreCase))
        {
            if (_orbitPrimarySurfaceTexture != null)
                Destroy(_orbitPrimarySurfaceTexture);
            var primaryData = LoadOrbitTextureFile(primaryTexturePath);
            _orbitPrimarySurfaceTexture = primaryData ?? (primaryIsBlackHole ? CreateBlackHoleTexture() : primaryIsStar ? CreateSunTexture() : CreatePrimaryBodyTexture(_project?.GetPrimaryBodyColor(primaryId)));
            _orbitPrimaryBodyIdRendered = primaryId;
            _orbitPrimaryTexturePath = primaryTexturePath;
            _orbitPrimaryIsBlackHole = primaryIsBlackHole;
            _orbitPrimaryIsStar = primaryIsStar;
            _orbitSphereTexturesDirty = true;
        }
    }

    /// <summary>Selects the loaded world ground image or a surface library asset for the playable body.</summary>
    private void RefreshPlayableBodyTexture()
    {
        var surfaceAsset = _project?.ProjectAssets.Where(asset => asset.Category.Equals("Surface", StringComparison.OrdinalIgnoreCase) && (asset.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || asset.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || asset.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(asset => asset.Name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0 || asset.Name.IndexOf("surface", StringComparison.OrdinalIgnoreCase) >= 0 || asset.Name.IndexOf("terrain", StringComparison.OrdinalIgnoreCase) >= 0).FirstOrDefault();
        var surfaceData = LoadOrbitWorldGroundTexture() ?? surfaceAsset?.Data;
        if (_orbitPlanetSurfaceTexture == null || !ReferenceEquals(surfaceData, _orbitSourceSurfaceData))
        {
            if (_orbitPlanetSurfaceTexture != null)
                Destroy(_orbitPlanetSurfaceTexture);
            _orbitPlanetSurfaceTexture = surfaceData == null ? CreateBluePlanetTexture() : LoadOrbitTexture(surfaceData) ?? CreateBluePlanetTexture();
            _orbitSourceSurfaceData = surfaceData;
            _orbitSphereTexturesDirty = true;
        }
    }

    /// <summary>Creates the isolated camera, sphere material and light used only for preview rendering.</summary>
    private void CreateOrbitSphereScene()
    {
        var cameraObject = new GameObject("World Creator orbit preview camera")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = 31
        };
        _orbitSphereCamera = cameraObject.AddComponent<Camera>();
        _orbitSphereCamera.enabled = false;
        _orbitSphereCamera.clearFlags = CameraClearFlags.SolidColor;
        _orbitSphereCamera.backgroundColor = new Color(0, 0, 0, 0);
        _orbitSphereCamera.orthographic = true;
        _orbitSphereCamera.orthographicSize = 1.08f;
        _orbitSphereCamera.nearClipPlane = .1f;
        _orbitSphereCamera.farClipPlane = 10f;
        _orbitSphereCamera.cullingMask = 1 << 31;
        cameraObject.transform.position = new Vector3(0, 0, -4);
        cameraObject.transform.rotation = Quaternion.identity;
        _orbitSphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _orbitSphereObject.name = "World Creator orbit preview sphere";
        _orbitSphereObject.hideFlags = HideFlags.HideAndDontSave;
        _orbitSphereObject.layer = 31;
        _orbitSphereObject.transform.position = Vector3.zero;
        _orbitSphereObject.transform.localScale = Vector3.one * 1.8f;
        var collider = _orbitSphereObject.GetComponent("SphereCollider");
        if (collider != null)
            Destroy(collider);
        _orbitSphereRenderer = _orbitSphereObject.GetComponent<MeshRenderer>();
        _orbitSphereRenderer.enabled = false;
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader != null)
            _orbitSphereMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        if (_orbitSphereMaterial != null)
            _orbitSphereRenderer.sharedMaterial = _orbitSphereMaterial;
        if (_orbitSphereMaterial != null)
        {
            if (_orbitSphereMaterial.HasProperty("_Metallic"))
                _orbitSphereMaterial.SetFloat("_Metallic", 0f);
            if (_orbitSphereMaterial.HasProperty("_Glossiness"))
                _orbitSphereMaterial.SetFloat("_Glossiness", 0f);
        }

        var lightObject = new GameObject("World Creator orbit preview light")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = 31
        };
        _orbitSphereLight = lightObject.AddComponent<Light>();
        _orbitSphereLight.type = LightType.Spot;
        _orbitSphereLight.renderMode = LightRenderMode.ForcePixel;
        _orbitSphereLight.intensity = 1.2f;
        _orbitSphereLight.range = 8f;
        _orbitSphereLight.spotAngle = 55f;
        _orbitSphereLight.cullingMask = 1 << 31;
    }
}
