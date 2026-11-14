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
            foreach (var tr in body.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
            body.gameObject.SetActive(false);
            player.body = body.gameObject;
            return player;
        }
    }
}
