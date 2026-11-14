using PhaseX.Player;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Builds the whole 3D NASA Mission Control room, the launch pad outside its panoramic window and the playable
    /// character, entirely from primitives and runtime materials (no models, prefabs or textures on disk).
    /// Used two ways: PhaseXBootstrap calls it at Play time when the scene has no station (zero-click boot), and the
    /// editor menu "PhaseX/Build 3D Mission Control Station &amp; Player" calls it to build the same hierarchy into the scene.
    /// </summary>
    public static class StationBuilder
    {
        public const string StationName = "[PHASEX_STATION]";
        public const string PlayerName = "[PHASEX_PLAYER]";

        /// <summary>The room sits far below the origin so it never shares the frame with the design view or flight sim.</summary>
        public static readonly Vector3 StationOrigin = new Vector3(0f, -300f, 0f);

        // room interior: x -10..10, z -8..8, height 5
        const float HalfW = 10f, HalfD = 8f, Height = 5f;
        const float WinHalf = 7.5f, WinBottom = 0.9f, WinTop = 3.8f;

        public static MissionControlStation EnsureStation()
        {
            var existing = Object.FindAnyObjectByType<MissionControlStation>();
            return existing != null ? existing : Build();
        }

        public static PhaseXPlayerController EnsurePlayer(MissionControlStation station)
        {
            var existing = Object.FindAnyObjectByType<PhaseXPlayerController>();
            return existing != null ? existing : BuildPlayer(station);
        }

        public static MissionControlStation Build()
        {
            var root = new GameObject(StationName);
            root.transform.position = StationOrigin;
            var station = root.AddComponent<MissionControlStation>();
            var t = root.transform;

            BuildShell(t, station);
            BuildExterior(t, station, out Transform rocket, out GameObject boosters, out Transform flame, out Light flameLight);
            EnsureExteriorDetail(t);

            // ---- Desk 1: main CAD monitor desk, against the west wall, facing into the room
            var desk = StationKit.Node("MonitorTerminalStation", t, new Vector3(-HalfW + 0.9f, 0, -1.5f), new Vector3(0, 90f, 0));
            station.monitor = desk.gameObject.AddComponent<MonitorTerminalStation>();
            station.monitor.BuildVisuals();

            // ---- centrepiece: hologram table
            var table = StationKit.Node("HologramAssemblyTable", t, new Vector3(0, 0, 0.3f));
            station.table = table.gameObject.AddComponent<HologramAssemblyTable>();
            station.table.BuildVisuals();

            // ---- east wall: mission briefing board
            var board = StationKit.Node("MissionBriefingBoard", t, new Vector3(HalfW - 0.12f, 2.25f, -1.5f), new Vector3(0, -90f, 0));
            station.board = board.gameObject.AddComponent<MissionBriefingBoard>();
            station.board.BuildVisuals();

            // ---- window console with the launch lever
            var console = StationKit.Node("LaunchControlConsole", t, new Vector3(0, 0, HalfD - 1.6f));
            station.console = console.gameObject.AddComponent<LaunchControlConsole>();
            station.console.BuildVisuals();
            station.console.rocket = rocket;
            station.console.boosters = boosters;
            station.console.flame = flame;
            station.console.flameLight = flameLight;

            // ---- three NASA knowledge pods
            var podSpots = new[] { new Vector3(-7.6f, 0, -6.3f), new Vector3(7.6f, 0, -6.3f), new Vector3(7.6f, 0, 5.4f) };
            var podFacing = new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(-1, 0, -1) };
            station.pods = new NasaKnowledgePod[StationKnowledge.Pods.Length];
            for (int i = 0; i < StationKnowledge.Pods.Length; i++)
            {
                var art = StationKnowledge.Pods[i];
                var pod = StationKit.Node("NasaKnowledgePod_" + art.Id, t, podSpots[i % podSpots.Length]);
                var comp = pod.gameObject.AddComponent<NasaKnowledgePod>();
                comp.podId = art.Id; comp.title = art.Title; comp.badge = art.Badge; comp.body = art.Body;
                comp.BuildVisuals(podFacing[i % podFacing.Length]);
                station.pods[i] = comp;
            }

            station.spawnPoint = StationKit.Node("PlayerSpawn", t, new Vector3(0, 0.05f, -5f));
            station.cinematicCam = StationKit.Node("LaunchCinematicCamera", t, new Vector3(3.4f, 2.0f, 1.2f));
            station.cinematicCam.rotation = Quaternion.LookRotation(new Vector3(-3.4f, 4.5f, 55f - 1.2f).normalized, Vector3.up);

            BuildPlayer(station);
            return station;
        }

        // ================================================================== room shell

        static void BuildShell(Transform t, MissionControlStation station)
        {
            var shell = StationKit.Node("Room", t, Vector3.zero);
            var floorA = PhaseXMaterials.Lit(new Color(0.105f, 0.12f, 0.15f), 0.72f, 0.25f);
            var floorB = PhaseXMaterials.Lit(new Color(0.085f, 0.095f, 0.12f), 0.72f, 0.25f);
            var wall = PhaseXMaterials.Lit(new Color(0.17f, 0.2f, 0.25f), 0.35f, 0.1f);
            var wallDark = PhaseXMaterials.Lit(new Color(0.1f, 0.115f, 0.145f), 0.4f, 0.2f);
            var ceil = PhaseXMaterials.Lit(new Color(0.07f, 0.08f, 0.1f), 0.3f, 0.1f);
            var trim = PhaseXMaterials.Lit(new Color(0.05f, 0.055f, 0.07f), 0.6f, 0.6f);

            // floor + ceiling as tiles (each tile then gets its own nearest lights), one collider for the floor
            var floor = StationKit.Node("Floor", shell, Vector3.zero);
            var ceiling = StationKit.Node("Ceiling", shell, Vector3.zero);
            for (int ix = 0; ix < 5; ix++)
            for (int iz = 0; iz < 4; iz++)
            {
                var c = new Vector3(-HalfW + 2f + ix * 4f, 0, -HalfD + 2f + iz * 4f);
                StationKit.Box(floor, "Tile", c + Vector3.down * 0.1f, new Vector3(3.96f, 0.2f, 3.96f), (ix + iz) % 2 == 0 ? floorA : floorB);
                StationKit.Box(ceiling, "Panel", c + Vector3.up * (Height + 0.1f), new Vector3(3.96f, 0.2f, 3.96f), ceil);
            }
            StationKit.Box(floor, "Grout", new Vector3(0, -0.12f, 0), new Vector3(HalfW * 2f, 0.2f, HalfD * 2f), trim);
            StationKit.Blocker(floor, "FloorCollider", new Vector3(0, -0.5f, 0), new Vector3(HalfW * 2f + 2f, 1f, HalfD * 2f + 2f));
            StationKit.Blocker(ceiling, "CeilingCollider", new Vector3(0, Height + 0.5f, 0), new Vector3(HalfW * 2f + 2f, 1f, HalfD * 2f + 2f));
            // glowing guide circle around the hologram table
            StationKit.Prim(PrimitiveType.Cylinder, floor, "FloorRing", new Vector3(0, 0.004f, 0.3f), new Vector3(4.6f, 0.004f, 4.6f),
                PhaseXMaterials.Lit(new Color(0.02f, 0.06f, 0.09f), 0.8f, 0f, new Color(0.03f, 0.22f, 0.34f)));
            StationKit.Prim(PrimitiveType.Cylinder, floor, "FloorRingInner", new Vector3(0, 0.006f, 0.3f), new Vector3(4.3f, 0.004f, 4.3f), floorB);

            // walls: south, west, east are solid panels; north holds the panoramic window
            var walls = StationKit.Node("Walls", shell, Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                float x = -HalfW + 2f + i * 4f;
                StationKit.Box(walls, "SouthPanel", new Vector3(x, Height / 2f, -HalfD - 0.1f), new Vector3(3.97f, Height, 0.2f), i % 2 == 0 ? wall : wallDark);
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -HalfD + 2f + i * 4f;
                StationKit.Box(walls, "WestPanel", new Vector3(-HalfW - 0.1f, Height / 2f, z), new Vector3(0.2f, Height, 3.97f), i % 2 == 0 ? wall : wallDark);
                StationKit.Box(walls, "EastPanel", new Vector3(HalfW + 0.1f, Height / 2f, z), new Vector3(0.2f, Height, 3.97f), i % 2 == 0 ? wall : wallDark);
            }
            StationKit.Blocker(walls, "SouthCollider", new Vector3(0, Height / 2f, -HalfD - 0.5f), new Vector3(HalfW * 2f + 2f, Height, 1f));
            StationKit.Blocker(walls, "WestCollider", new Vector3(-HalfW - 0.5f, Height / 2f, 0), new Vector3(1f, Height, HalfD * 2f + 2f));
            StationKit.Blocker(walls, "EastCollider", new Vector3(HalfW + 0.5f, Height / 2f, 0), new Vector3(1f, Height, HalfD * 2f + 2f));
            StationKit.Blocker(walls, "NorthCollider", new Vector3(0, Height / 2f, HalfD + 0.5f), new Vector3(HalfW * 2f + 2f, Height, 1f));

            float nz = HalfD + 0.1f;
            float side = HalfW - WinHalf;
            StationKit.Box(walls, "NorthSill", new Vector3(0, WinBottom / 2f, nz), new Vector3(HalfW * 2f, WinBottom, 0.2f), wallDark);
            StationKit.Box(walls, "NorthHeader", new Vector3(0, (WinTop + Height) / 2f, nz), new Vector3(HalfW * 2f, Height - WinTop, 0.2f), wallDark);
            StationKit.Box(walls, "NorthLeft", new Vector3(-HalfW + side / 2f, (WinBottom + WinTop) / 2f, nz), new Vector3(side, WinTop - WinBottom, 0.2f), wall);
            StationKit.Box(walls, "NorthRight", new Vector3(HalfW - side / 2f, (WinBottom + WinTop) / 2f, nz), new Vector3(side, WinTop - WinBottom, 0.2f), wall);

            // panoramic observation window: tinted glass and mullions
            var window = StationKit.Node("PanoramicWindow", shell, Vector3.zero);
            StationKit.Box(window, "Glass", new Vector3(0, (WinBottom + WinTop) / 2f, nz), new Vector3(WinHalf * 2f, WinTop - WinBottom, 0.03f),
                StationKit.Ghost(new Color(0.35f, 0.65f, 0.9f, 0.07f)));
            for (int i = -2; i <= 2; i++)
                StationKit.Box(window, "Mullion", new Vector3(i * WinHalf / 2.5f * 1.25f, (WinBottom + WinTop) / 2f, nz - 0.02f), new Vector3(0.07f, WinTop - WinBottom, 0.1f), trim);
            StationKit.Box(window, "FrameTop", new Vector3(0, WinTop, nz - 0.02f), new Vector3(WinHalf * 2f + 0.2f, 0.1f, 0.14f), trim);
            StationKit.Box(window, "FrameBottom", new Vector3(0, WinBottom, nz - 0.06f), new Vector3(WinHalf * 2f + 0.2f, 0.1f, 0.3f), trim);

            // big wall telemetry screen above the window
            var screen = StationKit.Node("WallTelemetryScreen", shell, new Vector3(0, 4.4f, HalfD - 0.03f));
            StationKit.Box(screen, "Bezel", Vector3.zero, new Vector3(15.2f, 0.98f, 0.06f), trim);
            StationKit.Box(screen, "Panel", new Vector3(0, 0, -0.035f), new Vector3(15f, 0.86f, 0.01f),
                PhaseXMaterials.Lit(new Color(0.012f, 0.03f, 0.06f), 0.85f, 0f, new Color(0.015f, 0.07f, 0.14f)));
            station.wallText = StationKit.Text(screen, "WallText", "MISSION CONTROL ONLINE", new Vector3(0, 0, -0.06f), Vector3.back, 0.235f, StationKit.TextC);

            // status light strips: ceiling perimeter + two overhead bars. They change colour with the mission state.
            var strips = StationKit.Node("StatusLightStrips", shell, Vector3.zero);
            var glow = StationKit.Glow(StationKit.Cyan);
            var list = new System.Collections.Generic.List<Renderer>();
            void Strip(string name, Vector3 pos, Vector3 size) => list.Add(StationKit.Box(strips, name, pos, size, glow).GetComponent<Renderer>());
            Strip("StripSouth", new Vector3(0, Height - 0.12f, -HalfD + 0.06f), new Vector3(HalfW * 2f - 0.6f, 0.07f, 0.07f));
            Strip("StripWest", new Vector3(-HalfW + 0.06f, Height - 0.12f, 0), new Vector3(0.07f, 0.07f, HalfD * 2f - 0.6f));
            Strip("StripEast", new Vector3(HalfW - 0.06f, Height - 0.12f, 0), new Vector3(0.07f, 0.07f, HalfD * 2f - 0.6f));
            Strip("StripFloorWest", new Vector3(-HalfW + 0.05f, 0.08f, 0), new Vector3(0.05f, 0.05f, HalfD * 2f - 0.6f));
            Strip("StripFloorEast", new Vector3(HalfW - 0.05f, 0.08f, 0), new Vector3(0.05f, 0.05f, HalfD * 2f - 0.6f));
            Strip("StripFloorSouth", new Vector3(0, 0.08f, -HalfD + 0.05f), new Vector3(HalfW * 2f - 0.6f, 0.05f, 0.05f));
            Strip("OverheadA", new Vector3(-4f, Height - 0.03f, 0), new Vector3(0.14f, 0.05f, 9f));
            Strip("OverheadB", new Vector3(4f, Height - 0.03f, 0), new Vector3(0.14f, 0.05f, 9f));
            station.statusStrips = list.ToArray();

            // room lighting: four soft ceiling lights
            var lights = StationKit.Node("RoomLights", shell, Vector3.zero);
            var ll = new System.Collections.Generic.List<Light>();
            foreach (var p in new[] { new Vector3(-5f, 4.3f, -4f), new Vector3(5f, 4.3f, -4f), new Vector3(-5f, 4.3f, 3.5f), new Vector3(5f, 4.3f, 3.5f) })
            {
                ll.Add(StationKit.PointLight(lights, "CeilingLight", p, new Color(0.86f, 0.92f, 1f), 30f, 13f));
                StationKit.Box(lights, "Fixture", p + Vector3.up * 0.62f, new Vector3(1.4f, 0.05f, 1.4f),
                    PhaseXMaterials.Lit(new Color(0.5f, 0.55f, 0.6f), 0.5f, 0f, new Color(1.6f, 1.8f, 2.1f)));
            }
            station.roomLights = ll.ToArray();

            // warning beacon hanging from the ceiling near the window
            var beaconRoot = StationKit.Node("WarningBeacon", shell, new Vector3(-6f, Height - 0.35f, HalfD - 1.2f));
            StationKit.Prim(PrimitiveType.Cylinder, beaconRoot, "Mount", new Vector3(0, 0.25f, 0), new Vector3(0.3f, 0.1f, 0.3f), trim);
            station.beaconLens = StationKit.Prim(PrimitiveType.Sphere, beaconRoot, "Lens", Vector3.zero, Vector3.one * 0.42f, StationKit.Glow(StationKit.Amber, 0.2f)).GetComponent<Renderer>();
            station.beacon = StationKit.Node("Rotor", beaconRoot, Vector3.zero);
            var bl = StationKit.Node("BeaconLight", station.beacon, new Vector3(0, 0, 0.15f)).gameObject.AddComponent<Light>();
            bl.type = LightType.Spot; bl.spotAngle = 70f; bl.range = 16f; bl.intensity = 60f; bl.color = StationKit.Amber; bl.shadows = LightShadows.None;
            bl.enabled = false;
            station.beaconLight = bl;

            // wall dressing
            StationKit.Text(shell, "SouthSign", "PHASE X  //  NASA MISSION CONTROL & SPACECRAFT ASSEMBLY STATION", new Vector3(0, 3.9f, -HalfD + 0.02f), Vector3.forward, 0.2f, StationKit.Cyan);
            StationKit.Text(shell, "SouthSub", "DESIGN  -  VALIDATE  -  LAUNCH", new Vector3(0, 3.55f, -HalfD + 0.02f), Vector3.forward, 0.13f, StationKit.Amber);
            StationKit.Text(shell, "WindowSign", "LAUNCH COMPLEX 1  //  OBSERVATION WINDOW", new Vector3(0, 0.55f, HalfD - 0.02f), Vector3.back, 0.11f, new Color(0.55f, 0.62f, 0.72f));
        }

        // ================================================================== launch pad outside the window

        static void BuildExterior(Transform t, MissionControlStation station, out Transform rocket, out GameObject boosters, out Transform flame, out Light flameLight)
        {
            var ext = StationKit.Node("Exterior", t, Vector3.zero);
            var ground = PhaseXMaterials.Lit(new Color(0.045f, 0.05f, 0.055f), 0.15f, 0f);
            var concrete = PhaseXMaterials.Lit(new Color(0.3f, 0.3f, 0.31f), 0.2f, 0f);
            var steel = PhaseXMaterials.Lit(new Color(0.35f, 0.12f, 0.08f), 0.4f, 0.5f);
            var white = PhaseXMaterials.Lit(new Color(0.92f, 0.93f, 0.95f), 0.6f, 0.2f);
            var dark = PhaseXMaterials.Lit(new Color(0.08f, 0.08f, 0.1f), 0.5f, 0.5f);
            var lamp = PhaseXMaterials.Lit(new Color(0.6f, 0.6f, 0.55f), 0.5f, 0f, new Color(3f, 2.8f, 2.2f));

            const float padZ = 55f;
            StationKit.Box(ext, "Ground", new Vector3(0, -0.6f, 150f), new Vector3(700f, 1f, 420f), ground);
            StationKit.Box(ext, "Road", new Vector3(0, -0.08f, 30f), new Vector3(7f, 0.04f, 40f), concrete);
            StationKit.Box(ext, "PadApron", new Vector3(0, 0.1f, padZ), new Vector3(30f, 0.4f, 30f), concrete);
            StationKit.Box(ext, "PadMount", new Vector3(0, 0.9f, padZ), new Vector3(8f, 1.4f, 8f), dark);

            // service tower with a lightning mast and floodlights
            var tower = StationKit.Node("ServiceTower", ext, new Vector3(-7.5f, 0, padZ + 1f));
            for (int i = 0; i < 4; i++)
                StationKit.Box(tower, "Leg", new Vector3(i % 2 == 0 ? -1.3f : 1.3f, 16f, i < 2 ? -1.3f : 1.3f), new Vector3(0.35f, 32f, 0.35f), steel);
            for (int k = 0; k < 8; k++)
            {
                StationKit.Box(tower, "Deck", new Vector3(0, 2f + k * 4f, 0), new Vector3(3f, 0.2f, 3f), steel);
                if (k % 2 == 1) StationKit.Box(tower, "Arm", new Vector3(3.4f, 2f + k * 4f, 0), new Vector3(4.2f, 0.3f, 0.6f), steel);
            }
            StationKit.Box(tower, "Mast", new Vector3(0, 36f, 0), new Vector3(0.15f, 8f, 0.15f), dark);
            foreach (var p in new[] { new Vector3(-13f, 0, padZ - 12f), new Vector3(13f, 0, padZ - 12f) })
            {
                StationKit.Box(ext, "FloodPole", p + Vector3.up * 6f, new Vector3(0.3f, 12f, 0.3f), dark);
                StationKit.Box(ext, "FloodLamp", p + Vector3.up * 12.2f, new Vector3(2.2f, 0.8f, 0.4f), lamp);
                var fl = StationKit.Node("FloodLight", ext, p + Vector3.up * 12f).gameObject.AddComponent<Light>();
                fl.type = LightType.Spot; fl.spotAngle = 75f; fl.range = 70f; fl.intensity = 900f; fl.color = new Color(1f, 0.95f, 0.85f); fl.shadows = LightShadows.None;
                fl.transform.rotation = Quaternion.LookRotation((new Vector3(0, 14f, padZ) + StationOrigin - fl.transform.position).normalized);
            }

            // multi-stage rocket standing on the pad (origin at its base)
            rocket = StationKit.Node("Rocket", ext, new Vector3(0, 1.6f, padZ));
            StationKit.Prim(PrimitiveType.Cylinder, rocket, "Stage1", new Vector3(0, 9f, 0), new Vector3(3.2f, 9f, 3.2f), white);
            StationKit.Prim(PrimitiveType.Cylinder, rocket, "Interstage", new Vector3(0, 18.4f, 0), new Vector3(3.25f, 0.5f, 3.25f), dark);
            StationKit.Prim(PrimitiveType.Cylinder, rocket, "Stage2", new Vector3(0, 22f, 0), new Vector3(3.2f, 3.2f, 3.2f), white);
            StationKit.Prim(PrimitiveType.Cylinder, rocket, "Fairing", new Vector3(0, 26.4f, 0), new Vector3(3.7f, 1.3f, 3.7f), white);
            StationKit.Prim(PrimitiveType.Sphere, rocket, "Nose", new Vector3(0, 27.7f, 0), new Vector3(3.7f, 5f, 3.7f), white);
            StationKit.Box(rocket, "Stripe", new Vector3(0, 12f, -1.58f), new Vector3(0.9f, 4.5f, 0.06f), PhaseXMaterials.Lit(new Color(0.75f, 0.1f, 0.08f), 0.5f, 0f));
            for (int i = 0; i < 4; i++)
                StationKit.Box(rocket, "Fin", Quaternion.Euler(0, i * 90f + 45f, 0) * new Vector3(1.9f, 1.4f, 0), new Vector3(1.4f, 2.8f, 0.12f), dark, false, new Vector3(0, i * 90f + 45f, 0));
            StationKit.Prim(PrimitiveType.Cylinder, rocket, "Engine", new Vector3(0, -0.3f, 0), new Vector3(2.2f, 0.4f, 2.2f), dark);
            var b = StationKit.Node("Boosters", rocket, Vector3.zero);
            for (int s = -1; s <= 1; s += 2)
            {
                StationKit.Prim(PrimitiveType.Cylinder, b, "Booster", new Vector3(s * 3.25f, 8f, 0), new Vector3(3.1f, 8f, 3.1f), white);
                StationKit.Prim(PrimitiveType.Sphere, b, "BoosterNose", new Vector3(s * 3.25f, 16f, 0), new Vector3(3.1f, 4.6f, 3.1f), white);
                StationKit.Prim(PrimitiveType.Cylinder, b, "BoosterEngine", new Vector3(s * 3.25f, -0.3f, 0), new Vector3(2.1f, 0.4f, 2.1f), dark);
            }
            b.gameObject.SetActive(false);
            boosters = b.gameObject;

            flame = StationKit.Prim(PrimitiveType.Sphere, rocket, "Flame", new Vector3(0, -4.5f, 0), new Vector3(2.6f, 9f, 2.6f),
                PhaseXMaterials.Lit(new Color(1f, 0.6f, 0.2f), 0.5f, 0f, new Color(6f, 2.6f, 0.6f)));
            flameLight = StationKit.PointLight(rocket, "FlameLight", new Vector3(0, -3f, 0), new Color(1f, 0.6f, 0.25f), 80f, 120f);
            flame.gameObject.SetActive(false);
            flameLight.enabled = false;

            // the Moon in the night sky (its texture is generated at Play time)
            station.moonRenderer = StationKit.Prim(PrimitiveType.Sphere, ext, "Moon", new Vector3(-70f, 62f, 240f), Vector3.one * 34f,
                PhaseXMaterials.Lit(new Color(0.78f, 0.78f, 0.8f), 0.05f, 0f, new Color(0.25f, 0.25f, 0.27f))).GetComponent<Renderer>();
        }

        // ================================================================== Phase 5 polish (idempotent)

        /// <summary>Name of the child that holds the Phase 5 detail; its presence means "already built".</summary>
        public const string DetailName = "Phase5Detail";

        /// <summary>
        /// Launch-pad dressing outside the window: visible floodlight beams on the rocket, a rim-light tower behind the
        /// pad, umbilical arms, hoses and cross-bracing on the service tower, pad edge lights and a layered horizon glow.
        /// Safe to call any number of times: <see cref="Build"/> calls it, and <see cref="MissionControlStation"/> calls
        /// it again at Play time so a station saved in a scene before Phase 5 looks the same as a freshly built one.
        /// </summary>
        public static void EnsureExteriorDetail(Transform stationRoot)
        {
            if (stationRoot == null) return;
            var ext = stationRoot.Find("Exterior");
            if (ext == null || ext.Find(DetailName) != null) return;
            var d = StationKit.Node(DetailName, ext, Vector3.zero);

            const float padZ = 55f;
            var dark = PhaseXMaterials.Lit(new Color(0.08f, 0.08f, 0.1f), 0.5f, 0.5f);
            var steel = PhaseXMaterials.Lit(new Color(0.35f, 0.12f, 0.08f), 0.4f, 0.5f);
            var hose = PhaseXMaterials.Lit(new Color(0.05f, 0.05f, 0.06f), 0.35f, 0.1f);
            var hazard = PhaseXMaterials.Lit(new Color(0.95f, 0.75f, 0.1f), 0.45f, 0.1f);
            var lamp = PhaseXMaterials.Lit(new Color(0.6f, 0.6f, 0.55f), 0.5f, 0f, new Color(3f, 2.8f, 2.2f));
            var amberLamp = PhaseXMaterials.Lit(new Color(0.5f, 0.3f, 0.05f), 0.5f, 0f, new Color(2.6f, 1.5f, 0.3f));
            var redLamp = PhaseXMaterials.Lit(new Color(0.5f, 0.05f, 0.04f), 0.5f, 0f, new Color(3f, 0.25f, 0.2f));
            var beam = StationKit.Ghost(new Color(1f, 0.95f, 0.82f, 0.016f));
            var beamCore = StationKit.Ghost(new Color(1f, 0.97f, 0.88f, 0.028f));
            var coolBeam = StationKit.Ghost(new Color(0.7f, 0.85f, 1f, 0.014f));

            // ---- floodlight beams from the two existing lamps onto the rocket
            var aim = new Vector3(0f, 14f, padZ);
            foreach (var p in new[] { new Vector3(-13f, 12.2f, padZ - 12f), new Vector3(13f, 12.2f, padZ - 12f) })
            {
                StationKit.Rod(d, "FloodBeam", p, aim, 1.8f, beam);
                StationKit.Rod(d, "FloodBeamCore", p, aim, 0.6f, beamCore);
            }

            // ---- two more light towers behind the pad. Only one carries a real light (a cool rim light), so the
            // rocket stays within URP's per-object light limit together with the engine flame.
            for (int s = -1; s <= 1; s += 2)
            {
                var foot = new Vector3(s * 17f, 0f, padZ + 13f);
                StationKit.Box(d, "RimLightPole", foot + Vector3.up * 7.5f, new Vector3(0.35f, 15f, 0.35f), dark);
                StationKit.Box(d, "RimLightLamp", foot + Vector3.up * 15.2f, new Vector3(2.4f, 0.9f, 0.5f), lamp, false, new Vector3(0f, s * 50f, 0f));
                StationKit.Rod(d, "RimLightBeam", foot + Vector3.up * 15f, aim + Vector3.up * 4f, 1.6f, coolBeam);
                if (s > 0)
                {
                    var l = StationKit.Node("RimLight", d, foot + Vector3.up * 15f).gameObject.AddComponent<Light>();
                    l.type = LightType.Spot; l.spotAngle = 70f; l.range = 80f; l.intensity = 650f;
                    l.color = new Color(0.72f, 0.86f, 1f); l.shadows = LightShadows.None;
                    l.transform.rotation = Quaternion.LookRotation((d.TransformPoint(aim + Vector3.up * 4f) - l.transform.position).normalized);
                }
            }

            // ---- service tower: cross-bracing on all four faces, umbilical arms with hoses, aviation beacon
            var tower = StationKit.Node("ServiceTowerDetail", d, new Vector3(-7.5f, 0f, padZ + 1f));
            for (int k = 0; k < 7; k++)
            {
                float y0 = 2f + k * 4f, y1 = y0 + 4f;
                float a = k % 2 == 0 ? -1.3f : 1.3f, b = -a;
                StationKit.Rod(tower, "Brace", new Vector3(a, y0, -1.3f), new Vector3(b, y1, -1.3f), 0.11f, steel);
                StationKit.Rod(tower, "Brace", new Vector3(a, y0, 1.3f), new Vector3(b, y1, 1.3f), 0.11f, steel);
                StationKit.Rod(tower, "Brace", new Vector3(-1.3f, y0, a), new Vector3(-1.3f, y1, b), 0.11f, steel);
                StationKit.Rod(tower, "Brace", new Vector3(1.3f, y0, a), new Vector3(1.3f, y1, b), 0.11f, steel);
            }
            foreach (float y in new[] { 6f, 14f, 22f })
            {
                // the existing arm ends at tower-local x = 5.5; the rocket hull is about 0.75 m further
                StationKit.Box(tower, "UmbilicalPlate", new Vector3(5.55f, y, 0f), new Vector3(0.25f, 0.9f, 0.9f), hazard);
                StationKit.Rod(tower, "UmbilicalHose", new Vector3(5.5f, y - 0.25f, -0.1f), new Vector3(6.05f, y - 0.95f, -0.3f), 0.16f, hose);
                StationKit.Rod(tower, "UmbilicalHose", new Vector3(5.5f, y + 0.1f, 0.2f), new Vector3(6.2f, y - 0.2f, 0.05f), 0.1f, hose);
                StationKit.Rod(tower, "ArmStrut", new Vector3(1.3f, y + 2.4f, 0f), new Vector3(5.2f, y + 0.15f, 0f), 0.09f, steel);
                StationKit.Prim(PrimitiveType.Sphere, tower, "ArmWarningLight", new Vector3(5.5f, y + 0.55f, 0f), Vector3.one * 0.28f, amberLamp);
            }
            StationKit.Prim(PrimitiveType.Sphere, tower, "AviationBeacon", new Vector3(0f, 40.2f, 0f), Vector3.one * 0.6f, redLamp);
            StationKit.Prim(PrimitiveType.Sphere, tower, "AviationBeacon", new Vector3(1.3f, 32.3f, 1.3f), Vector3.one * 0.35f, redLamp);

            // ---- pad: amber edge lights round the apron and a hazard stripe at the pad mount
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Clamp(Mathf.Cos(a) * 21f, -14.6f, 14.6f), 0.36f, padZ + Mathf.Clamp(Mathf.Sin(a) * 21f, -14.6f, 14.6f));
                StationKit.Box(d, "PadEdgeLight", p, new Vector3(0.3f, 0.14f, 0.3f), amberLamp);
            }
            StationKit.Box(d, "PadHazardStripe", new Vector3(0f, 1.62f, padZ - 4.02f), new Vector3(8f, 0.12f, 0.06f), hazard);

            // ---- horizon: a warm line at the ground's edge under layered blue airglow
            const float hz = 357f;
            // many faint layers of growing height add up to a smooth gradient that fades into the night sky
            StationKit.Box(d, "HorizonDawn", new Vector3(0f, 0.7f, hz), new Vector3(2400f, 1.6f, 0.5f), StationKit.Ghost(new Color(1f, 0.72f, 0.42f, 0.16f)));
            var airglow = StationKit.Ghost(new Color(0.25f, 0.5f, 1f, 0.012f));
            float layer = 2f;
            for (int i = 0; i < 16; i++)
            {
                StationKit.Box(d, "HorizonGlow", new Vector3(0f, layer * 0.5f, hz + 1f + i * 0.4f), new Vector3(2400f, layer, 0.5f), airglow);
                layer *= 1.25f;
            }
        }

        /// <summary>
        /// Extra stars for the night sky over the pad: a dome of bright stars and a dense, dim Milky Way band.
        /// Particles are not saved with a scene, so this is always built at Play time (by MissionControlStation).
        /// </summary>
        public static void EnsureSkyStars(Transform stationRoot)
        {
            if (stationRoot == null || !Application.isPlaying) return;
            var ext = stationRoot.Find("Exterior");
            if (ext == null || ext.Find("SkyStars") != null) return;
            var go = new GameObject("SkyStars");
            go.transform.SetParent(ext, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = 1e6f;
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = PhaseXMaterials.Sprite(Color.white, PhaseXMaterials.SoftDot());
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.maxParticleSize = 0.05f;

            var rng = new System.Random(1972);
            float Rnd() => (float)rng.NextDouble();
            void Emit(Vector3 dir, float size, Color c) => ps.Emit(new ParticleSystem.EmitParams
            {
                position = dir.normalized * 470f, velocity = Vector3.zero, startLifetime = 1e6f, startSize = size, startColor = c
            }, 1);

            // bright stars over the upper hemisphere
            for (int i = 0; i < 520; i++)
            {
                var dir = new Vector3(Rnd() * 2f - 1f, Rnd() * 0.98f + 0.02f, Rnd() * 2f - 1f);
                if (dir.sqrMagnitude < 0.02f) continue;
                float tint = Rnd();
                Color c = tint < 0.14f ? new Color(1f, 0.82f, 0.62f) : tint > 0.84f ? new Color(0.68f, 0.8f, 1f) : Color.white;
                Emit(dir, 0.9f + Rnd() * (Rnd() > 0.94f ? 3.4f : 1.5f), c * (0.5f + Rnd() * 0.5f));
            }
            // Milky Way: a dense band of faint stars on a tilted great circle, rising behind the pad
            var band = Quaternion.Euler(0f, 25f, 58f);
            for (int i = 0; i < 800; i++)
            {
                float a = Rnd() * Mathf.PI * 2f;
                float spread = (Rnd() + Rnd() + Rnd() - 1.5f) * 0.24f;      // roughly Gaussian across the band
                var dir = band * new Vector3(Mathf.Cos(a), spread, Mathf.Sin(a));
                if (dir.y < 0.03f) continue;
                Emit(dir, 0.6f + Rnd() * 0.9f, new Color(0.78f, 0.84f, 1f) * (0.22f + Rnd() * 0.3f));
            }
            ps.Play();
        }

        /// <summary>
        /// Astronaut suit detail: reflective gold visor in a dark frame, neck ring, helmet lamps, a chest telemetry
        /// patch with status LEDs, backpack hoses, antenna and a status light (coloured by the mission state at
        /// Play time, see PhaseXPlayerController). Safe to call any number of times.
        /// </summary>
        public static void EnsureAstronautDetail(GameObject bodyGo)
        {
            if (bodyGo == null) return;
            var body = bodyGo.transform;
            if (body.Find("SuitDetail") != null) return;
            var d = StationKit.Node("SuitDetail", body, Vector3.zero);

            var gold = PhaseXMaterials.Lit(new Color(0.92f, 0.67f, 0.2f), 0.97f, 1f, new Color(0.17f, 0.1f, 0.015f));
            var dark = PhaseXMaterials.Lit(new Color(0.05f, 0.055f, 0.07f), 0.6f, 0.5f);
            var metal = PhaseXMaterials.Lit(new Color(0.7f, 0.72f, 0.76f), 0.8f, 0.9f);
            var grey = PhaseXMaterials.Lit(new Color(0.35f, 0.37f, 0.4f), 0.5f, 0.3f);
            var screen = PhaseXMaterials.Lit(new Color(0.02f, 0.08f, 0.12f), 0.85f, 0f, new Color(0.1f, 0.75f, 1.2f));
            var ledGreen = PhaseXMaterials.Lit(new Color(0.05f, 0.3f, 0.1f), 0.5f, 0f, new Color(0.3f, 2.4f, 0.8f));
            var ledAmber = PhaseXMaterials.Lit(new Color(0.3f, 0.2f, 0.03f), 0.5f, 0f, new Color(2.4f, 1.5f, 0.3f));
            var lampGlow = PhaseXMaterials.Lit(new Color(0.6f, 0.6f, 0.55f), 0.5f, 0f, new Color(2.2f, 2.1f, 1.8f));
            var flag = PhaseXMaterials.Lit(new Color(0.1f, 0.2f, 0.6f), 0.4f, 0f);

            // helmet: gold visor, dark frame just behind it, neck ring, two lamps
            var visor = body.Find("Visor");
            var visorRenderer = visor != null ? visor.GetComponent<Renderer>() : null;
            if (visorRenderer != null) visorRenderer.sharedMaterial = gold;
            StationKit.Prim(PrimitiveType.Sphere, d, "VisorFrame", new Vector3(0f, 1.63f, 0.07f), new Vector3(0.335f, 0.275f, 0.235f), dark);
            StationKit.Prim(PrimitiveType.Cylinder, d, "NeckRing", new Vector3(0f, 1.45f, 0f), new Vector3(0.34f, 0.025f, 0.34f), metal);
            for (int s = -1; s <= 1; s += 2)
            {
                StationKit.Box(d, "HelmetLamp", new Vector3(s * 0.19f, 1.7f, 0.05f), new Vector3(0.04f, 0.05f, 0.08f), grey);
                StationKit.Box(d, "HelmetLampLens", new Vector3(s * 0.19f, 1.7f, 0.093f), new Vector3(0.03f, 0.035f, 0.01f), lampGlow);
                // life-support hoses from the backpack to the helmet
                StationKit.Rod(d, "Hose", new Vector3(s * 0.16f, 1.34f, -0.2f), new Vector3(s * 0.13f, 1.5f, -0.1f), 0.03f, grey);
            }

            // chest telemetry patch: a lit readout with three bars and a column of status LEDs
            StationKit.Box(d, "ChestTelemetry", new Vector3(-0.035f, 1.2f, 0.243f), new Vector3(0.14f, 0.09f, 0.012f), screen);
            for (int i = 0; i < 3; i++)
                StationKit.Box(d, "TelemetryBar", new Vector3(-0.08f + i * 0.045f, 1.185f + i * 0.008f, 0.251f), new Vector3(0.028f, 0.03f + i * 0.016f, 0.006f), i == 2 ? ledAmber : ledGreen);
            StationKit.Prim(PrimitiveType.Sphere, d, "ChestLed", new Vector3(0.085f, 1.235f, 0.243f), Vector3.one * 0.024f, ledGreen);
            StationKit.Prim(PrimitiveType.Sphere, d, "ChestLed", new Vector3(0.085f, 1.2f, 0.243f), Vector3.one * 0.024f, ledGreen);
            StationKit.Prim(PrimitiveType.Sphere, d, "ChestLed", new Vector3(0.085f, 1.165f, 0.243f), Vector3.one * 0.024f, ledAmber);
            StationKit.Box(d, "FlagPatch", new Vector3(-0.408f, 1.22f, 0f), new Vector3(0.012f, 0.07f, 0.1f), flag);

            // backpack: status light in a housing, and a whip antenna
            StationKit.Prim(PrimitiveType.Cylinder, d, "StatusLightHousing", new Vector3(0.12f, 1.33f, -0.332f), new Vector3(0.09f, 0.008f, 0.09f), dark, false, new Vector3(90f, 0f, 0f));
            StationKit.Prim(PrimitiveType.Sphere, d, "BackpackStatusLight", new Vector3(0.12f, 1.33f, -0.34f), Vector3.one * 0.06f, ledGreen);
            StationKit.Rod(d, "Antenna", new Vector3(-0.15f, 1.38f, -0.29f), new Vector3(-0.15f, 1.74f, -0.31f), 0.012f, metal);
            StationKit.Prim(PrimitiveType.Sphere, d, "AntennaTip", new Vector3(-0.15f, 1.75f, -0.31f), Vector3.one * 0.03f, ledAmber);

            foreach (var tr in d.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
        }

        // ================================================================== player

        public static PhaseXPlayerController BuildPlayer(MissionControlStation station)
        {
            var go = new GameObject(PlayerName) { layer = 2 }; // Ignore Raycast: the interaction ray never hits the player
            Vector3 spawn = station != null && station.spawnPoint != null ? station.spawnPoint.position : StationOrigin + new Vector3(0, 0.05f, -5f);
            go.transform.position = spawn;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.34f; cc.center = new Vector3(0, 0.92f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 45f;
            var player = go.AddComponent<PhaseXPlayerController>();
            go.AddComponent<PlayerInteractionSystem>();

            player.head = StationKit.Node("Head", go.transform, new Vector3(0, player.eyeHeight, 0));

            // visible astronaut/engineer body (shown in third person)
            var body = StationKit.Node("AstronautBody", go.transform, Vector3.zero);
            var suit = PhaseXMaterials.Lit(new Color(0.9f, 0.9f, 0.92f), 0.45f, 0.05f);
            var grey = PhaseXMaterials.Lit(new Color(0.35f, 0.37f, 0.4f), 0.5f, 0.3f);
            var visor = PhaseXMaterials.Lit(new Color(0.05f, 0.08f, 0.14f), 0.95f, 0.6f, new Color(0.05f, 0.2f, 0.35f));
            var orange = PhaseXMaterials.Lit(new Color(0.95f, 0.45f, 0.1f), 0.5f, 0f);
            StationKit.Prim(PrimitiveType.Capsule, body, "Torso", new Vector3(0, 1.08f, 0), new Vector3(0.52f, 0.36f, 0.4f), suit);
            StationKit.Prim(PrimitiveType.Sphere, body, "Helmet", new Vector3(0, 1.62f, 0), Vector3.one * 0.38f, suit);
            StationKit.Prim(PrimitiveType.Sphere, body, "Visor", new Vector3(0, 1.63f, 0.09f), new Vector3(0.3f, 0.24f, 0.26f), visor);
            StationKit.Box(body, "LifeSupport", new Vector3(0, 1.15f, -0.24f), new Vector3(0.4f, 0.5f, 0.18f), grey);
            StationKit.Box(body, "ChestPack", new Vector3(0, 1.2f, 0.2f), new Vector3(0.24f, 0.16f, 0.08f), grey);
            StationKit.Box(body, "MissionPatch", new Vector3(0.19f, 1.3f, 0.19f), new Vector3(0.08f, 0.08f, 0.02f), orange);
            for (int s = -1; s <= 1; s += 2)
            {
                StationKit.Prim(PrimitiveType.Capsule, body, "Arm", new Vector3(s * 0.33f, 1.08f, 0), new Vector3(0.15f, 0.3f, 0.15f), suit, false, new Vector3(0, 0, s * 8f));
                StationKit.Prim(PrimitiveType.Sphere, body, "Glove", new Vector3(s * 0.37f, 0.76f, 0), Vector3.one * 0.13f, grey);
                StationKit.Prim(PrimitiveType.Capsule, body, "Leg", new Vector3(s * 0.13f, 0.42f, 0), new Vector3(0.19f, 0.42f, 0.19f), suit);
                StationKit.Box(body, "Boot", new Vector3(s * 0.13f, 0.05f, 0.04f), new Vector3(0.18f, 0.1f, 0.3f), grey);
            }
            EnsureAstronautDetail(body.gameObject);
            foreach (var tr in body.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
            body.gameObject.SetActive(false);
            player.body = body.gameObject;
            return player;
        }
    }
}
