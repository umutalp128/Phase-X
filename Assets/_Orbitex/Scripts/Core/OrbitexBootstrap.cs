using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Zero-click entry point. After any scene loads, spawns every manager, the camera/lighting,
/// the procedural 3D preview and the full Mission Control UI. Works in a completely empty scene.
/// </summary>
public class OrbitexBootstrap : MonoBehaviour
{
    public static OrbitexBootstrap Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBoot()
    {
        if (Instance != null) return;
        var go = new GameObject("[Orbitex] Bootstrap");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<OrbitexBootstrap>();
        Instance.Boot();
    }

    void Boot()
    {
        Application.targetFrameRate = 60;
        // Consistent number formatting ($12.7M, not $12,7M) on every teammate's OS locale.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var calc = MissionCalculator.EnsureExists();

        EnsureEventSystem();
        Camera cam = SetupCamera();
        SetupLighting();

        var previewGo = new GameObject("[Orbitex] SpacecraftPreview");
        previewGo.transform.SetParent(transform, false);
        previewGo.AddComponent<SpacecraftPreview>().Init(cam);

        var uiGo = new GameObject("[Orbitex] MissionControlUI");
        uiGo.transform.SetParent(transform, false);
        uiGo.AddComponent<MissionControlUI>().Init(calc);

        Debug.Log("[Orbitex] Mission Control online. Scenario: " + calc.CurrentScenario.Title);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("[Orbitex] EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
        DontDestroyOnLoad(es);
    }

    static Camera SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("[Orbitex] Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.008f, 0.012f, 0.03f, 1f);
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1500f;
        cam.transform.position = new Vector3(0, 1.2f, -8f);
        cam.transform.LookAt(Vector3.zero);
        return cam;
    }

    static void SetupLighting()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            var any = FindAnyObjectByType<Light>();
            if (any != null && any.type == LightType.Directional) sun = any;
        }
        if (sun == null)
        {
            var go = new GameObject("[Orbitex] Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(18f, -62f, 0f);
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.intensity = 1.8f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.11f, 0.16f);
        RenderSettings.fog = false;
    }
}
