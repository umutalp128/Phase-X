# Flight-simulation models and the Mission Control station furniture. Unity coordinates (+Y up).
# Uses lunar_module() and rover() from spacecraft.py, so load that file first.
I4 = Matrix.Identity(4)
METAL, SCREEN, STEEL = "Mat_Metal", "Mat_Screen", "Mat_SteelRed"


# ------------------------------------------------------------------------------------------------ simulation

@model("Simulation", "sim_lunar_module")
def sim_lm(m):
    m.M = S(0.064)                       # about 0.16 units across the footpads, standing on y = 0
    lunar_module(m, beacon=True, antennas=True)
    m.M = I4


@model("Simulation", "sim_lunar_rover")
def sim_rover(m):
    m.M = S(0.15) @ T((0, 0.325, 0))     # about 0.18 units long, wheels on y = 0
    rover(m, dish=True, beacon=True)
    m.M = I4


@model("Simulation", "planet_sphere_highres")
def planet(m):
    """64 x 32 UV sphere, radius 0.5 (same size as Unity's sphere), equirectangular UVs with a clean seam."""
    seg, rings, r = 64, 32, 0.5
    bm = m._bm("Mat_White", None, "PlanetSphere")
    uv = bm.loops.layers.uv.new("UVMap")
    def P(i, j):
        th = math.pi * j / rings; a = 2 * math.pi * i / seg
        return U((r * math.sin(th) * math.cos(a), r * math.cos(th), r * math.sin(th) * math.sin(a)))
    top = bm.verts.new(P(0, 0)); bot = bm.verts.new(P(0, rings))
    grid = [[bm.verts.new(P(i, j)) for i in range(seg)] for j in range(1, rings)]
    def face(vs, uvs):
        f = bm.faces.new(vs)
        for l, t in zip(f.loops, uvs): l[uv].uv = t
    for i in range(seg):
        i2 = (i + 1) % seg
        u0, u1 = 1 - i / seg, 1 - (i + 1) / seg
        face([top, grid[0][i], grid[0][i2]], [((u0 + u1) / 2, 1.0), (u0, 1 - 1 / rings), (u1, 1 - 1 / rings)])
        face([bot, grid[-1][i2], grid[-1][i]], [((u0 + u1) / 2, 0.0), (u1, 1 / rings), (u0, 1 / rings)])
        for j in range(rings - 2):
            v0, v1 = 1 - (j + 1) / rings, 1 - (j + 2) / rings
            face([grid[j][i], grid[j + 1][i], grid[j + 1][i2], grid[j][i2]], [(u0, v0), (u0, v1), (u1, v1), (u1, v0)])


# ------------------------------------------------------------------------------------------------ station

@model("Station", "station_cad_desk")
def cad_desk(m):
    D, C = "Mat_Dark", "Mat_GlowCyan"
    # desk: top, side panels, back panel, cable tray, glowing front edge
    m.box(D, (0, 0.82, 0), (2.6, 0.08, 1.1), bevel=0.02)
    m.box(C, (0, 0.80, 0.545), (2.4, 0.012, 0.012))
    for s in (-1, 1):
        m.box(METAL, (s * 1.2, 0.4, 0), (0.1, 0.8, 1.0), bevel=0.02)
        m.box(D, (s * 1.2, 0.03, 0), (0.16, 0.06, 1.06), bevel=0.012)
        m.box(METAL, (s * 0.75, 0.6, -0.1), (0.5, 0.36, 0.7), bevel=0.02)        # drawer pedestal
        for y in (0.5, 0.7):
            m.box("Mat_Silver", (s * 0.75, y, 0.255), (0.2, 0.014, 0.014), bevel=0.004)
    m.box(METAL, (0, 0.45, -0.5), (2.4, 0.7, 0.06), bevel=0.012)
    m.cyl("Mat_Silver", (-1.15, 0.14, 0.3), (1.15, 0.14, 0.3), 0.022, seg=10)    # footrest
    # curved ultrawide monitor: the screen is its own mesh so the station can tint it
    rc = 5.0
    o = (0, 0, -0.21 + rc)
    half = math.degrees(math.asin(0.8 / rc))
    m.named("Screen", (0, 1.55, -0.21))
    m.arc(SCREEN, 1.11, 1.99, rc, rc + 0.012, 180 - half, 180 + half, 10, o=o, n="Screen")
    hb = half + 0.7
    m.arc(D, 1.05, 2.05, rc + 0.012, rc + 0.07, 180 - hb, 180 + hb, 10, o=o)     # shell
    m.arc(D, 1.05, 1.105, rc - 0.012, rc + 0.02, 180 - hb, 180 + hb, 10, o=o)    # bezel lip, bottom / top
    m.arc(D, 1.995, 2.05, rc - 0.012, rc + 0.02, 180 - hb, 180 + hb, 10, o=o)
    for s in (-1, 1):
        m.arc(D, 1.05, 2.05, rc - 0.012, rc + 0.02, 180 + s * half, 180 + s * hb, 1, o=o)
    m.box(C, (0, 1.075, -0.196), (0.12, 0.008, 0.006))                           # power light
    m.box(D, (0, 0.98, -0.3), (0.12, 0.34, 0.08), bevel=0.02)                    # stand
    m.box(D, (0, 0.875, -0.26), (0.5, 0.03, 0.3), bevel=0.012)
    for s in (-1, 1):                                                             # angled side telemetry monitors
        m.M = T((s * 1.05, 1.35, -0.12)) @ euler(0, -s * 28, 0)
        m.box(D, (0, 0, 0), (0.62, 0.5, 0.05), bevel=0.014)
        m.box(SCREEN, (0, 0, 0.022), (0.56, 0.44, 0.012), bevel=0.003)
        for k in range(4):
            m.box(C if k != 2 else "Mat_GlowAmber", (-0.2 + k * 0.13, -0.1 + 0.03 * k, 0.03), (0.07, 0.06 + 0.05 * k, 0.003))
        m.cyl(D, (0, -0.25, -0.03), (0, -0.47, -0.03), 0.025, seg=10)
        m.cyl(D, (0, -0.47, -0.03), (0, -0.49, -0.03), 0.12, seg=18, bev=0.006)
        m.M = I4
    # mechanical keyboard with key relief, and a mouse
    m.box(D, (0, 0.872, 0.2), (0.72, 0.024, 0.24), bevel=0.008)
    m.tiles(METAL, (0, 0.89, 0.2), (1, 0, 0), (0, 0, 1), 0.68, 0.2, 15, 5, thick=0.014, gap=0.008, bevel=0.003)
    m.box(C, (0, 0.886, 0.325), (0.4, 0.004, 0.004))
    m.sph(D, (0.55, 0.878, 0.22), 0.06, 14, 7, scale=(0.6, 0.35, 1))
    m.box(METAL, (0.55, 0.862, 0.22), (0.26, 0.004, 0.22), bevel=0.001)
    # ergonomic control-room chair, pushed to the side
    m.M = T((1.75, 0, 0.85)) @ euler(0, -35, 0)
    for k in range(5):
        a = math.radians(k * 72)
        d = V(math.sin(a), 0, math.cos(a))
        m.cyl(METAL, V(0, 0.09, 0), d * 0.3 + V(0, 0.06, 0), 0.022, seg=8)
        m.sph(D, d * 0.3 + V(0, 0.03, 0), 0.03, 10, 5)
    m.cyl("Mat_Silver", (0, 0.08, 0), (0, 0.44, 0), 0.03, seg=12)
    m.cyl(METAL, (0, 0.42, 0), (0, 0.47, 0), 0.12, seg=16, bev=0.01)
    m.box(D, (0, 0.52, 0), (0.5, 0.09, 0.5), bevel=0.04)                          # seat cushion
    m.box(D, (0, 0.92, -0.25), (0.46, 0.62, 0.08), rot=(8, 0, 0), bevel=0.035)    # backrest
    m.box(D, (0, 1.3, -0.3), (0.26, 0.14, 0.07), rot=(8, 0, 0), bevel=0.03)       # headrest
    m.box(METAL, (0, 0.68, -0.26), (0.08, 0.34, 0.03), rot=(8, 0, 0), bevel=0.01)
    m.box("Mat_GlowCyan", (0, 0.92, -0.205), (0.3, 0.008, 0.006), rot=(8, 0, 0))
    for s in (-1, 1):
        m.cyl(METAL, (s * 0.27, 0.5, -0.05), (s * 0.27, 0.7, -0.05), 0.016, seg=8)
        m.box(D, (s * 0.27, 0.71, 0.0), (0.06, 0.03, 0.26), bevel=0.012)
    m.M = I4


@model("Station", "station_hologram_table")
def holo_table(m):
    D, C = "Mat_Dark", "Mat_GlowCyan"
    m.lathe(D, [(1.3, 0.0), (1.3, 0.1), (1.24, 0.16), (0.0, 0.16)], seg=48)                       # bevelled plinth
    m.lathe(C, [(1.245, 0.158), (1.2, 0.165), (1.2, 0.158)], seg=48, closed=True)
    m.lathe(METAL, [(1.0, 0.16), (0.9, 0.22), (0.9, 0.82), (1.02, 0.9), (0.0, 0.9)], seg=48)      # pedestal
    for k in range(24):                                                                            # ribs
        a = k * 15
        m.arc(METAL, 0.24, 0.8, 0.9, 0.94, a - 2.2, a + 2.2, 1)
    for y in (0.3, 0.74):
        m.torus("Mat_Silver", (0, y, 0), 0.945, 0.012, (0, 1, 0), 48, 6)
    m.lathe(D, [(1.02, 0.9), (1.1, 0.9), (1.1, 0.94), (1.06, 0.96), (0.0, 0.96)], seg=48)          # table top
    m.named("EmitterRing", (0, 0.965, 0))
    m.lathe(C, [(0.75, 0.958), (0.85, 0.958), (0.85, 0.972), (0.75, 0.972)], seg=48, closed=True, n="EmitterRing")
    m.lathe(D, [(0.75, 0.96), (0.75, 0.978), (0.7, 0.985), (0.0, 0.985)], seg=48)                  # emitter core
    for r_ in (0.25, 0.45, 0.62):
        m.torus("Mat_Silver", (0, 0.985, 0), r_, 0.006, (0, 1, 0), 40, 5)
    for k in range(6):                                                                             # projector emitters
        a = math.radians(k * 60 + 30)
        d = V(math.sin(a), 0, math.cos(a))
        m.box(METAL, d * 0.97 + V(0, 0.99, 0), (0.12, 0.07, 0.12), rot=(0, k * 60 + 30, 0), bevel=0.015)
        m.sph(C, d * 0.95 + V(0, 1.025, 0), 0.03, 10, 5)
    m.named("Beam", (0, 2.0, 0))
    m.cyl("Mat_Beam", (0, 1.0, 0), (0, 3.0, 0), 0.85, seg=40, n="Beam")
    m.box(D, (1.5, 0.03, 0.14), (1.2, 0.06, 0.22), bevel=0.012)                                    # cable conduit to the stand
    g = m.group("LaunchVehicleStand", (2.15, 0, 0.2))
    m.M = T((2.15, 0, 0.2))
    m.lathe(D, [(0.42, 0.0), (0.42, 0.05), (0.38, 0.08), (0.0, 0.08)], seg=32, g=g)
    m.lathe(METAL, [(0.33, 0.08), (0.3, 0.14), (0.3, 0.82), (0.35, 0.88), (0.35, 0.9), (0.0, 0.9)], seg=32, g=g)
    for k in range(12):
        m.arc(METAL, 0.16, 0.8, 0.3, 0.325, k * 30 - 4, k * 30 + 4, 1, g=g)
    m.lathe("Mat_GlowAmber", [(0.24, 0.9), (0.31, 0.9), (0.31, 0.912), (0.24, 0.912)], seg=32, closed=True, g=g)
    m.lathe(D, [(0.24, 0.9), (0.24, 0.918), (0.0, 0.918)], seg=32, g=g)
    m.M = I4


@model("Station", "station_launch_console")
def launch_console(m):
    D, S_ = "Mat_Dark", "Mat_Silver"
    m.box(METAL, (0, 0.45, 0), (3.2, 0.86, 0.86), bevel=0.04)
    m.box(D, (0, 0.04, 0), (3.24, 0.08, 0.9), bevel=0.012)
    for x in (-0.8, 0.0, 0.8):                                                    # recessed front panels with vents
        m.box(D, (x, 0.45, -0.432), (0.7, 0.6, 0.012), bevel=0.004)
        for k in range(5):
            m.box(METAL, (x, 0.3 + k * 0.05, -0.44), (0.5, 0.018, 0.006))
    m.box("Mat_GlowAmber", (0, 0.86, -0.436), (3.0, 0.012, 0.006))
    for s in (-1, 1):
        m.box(METAL, (s * 1.62, 0.56, -0.05), (0.06, 1.1, 1.0), bevel=0.02)       # side cheeks
    # slanted deck
    m.M = T((0, 0.98, -0.08)) @ euler(-14, 0, 0)
    m.box(D, (0, 0, 0), (3.2, 0.06, 1.05), bevel=0.015)
    for s in (-1, 1):                                                             # illuminated button banks
        c = V(s * 1.0, 0.036, -0.1)
        m.box(METAL, c - V(0, 0.004, 0), (0.86, 0.008, 0.5), bevel=0.002)
        m.tiles(SCREEN, c + V(0, 0.008, -0.08), (1, 0, 0), (0, 0, 1), 0.78, 0.26, 6, 2, thick=0.02, gap=0.03, bevel=0.005)
        cols = ["Mat_GlowCyan", "Mat_GlowCyan", "Mat_GlowAmber", "Mat_GlowGreen", "Mat_GlowCyan", "Mat_GlowRed"]
        for k in range(6):
            m.box(cols[k], c + V(-0.325 + k * 0.13, 0.012, 0.16), (0.09, 0.022, 0.09), bevel=0.006)
        m.box(METAL, V(s * 1.0, 0.1, 0.36), (0.8, 0.2, 0.05), rot=(-50, 0, 0), bevel=0.01)       # small readout hood
        m.box(SCREEN, V(s * 1.0, 0.105, 0.342), (0.72, 0.15, 0.012), rot=(-50, 0, 0))
    m.M = I4
    # guard rail along the engineer's edge
    m.cyl(S_, (-1.5, 1.2, -0.62), (1.5, 1.2, -0.62), 0.018, seg=10)
    for x in (-1.5, -0.75, 0.75, 1.5):
        m.cyl(S_, (x, 1.1, -0.6), (x, 1.2, -0.62), 0.014, seg=8)
        m.sph(S_, (x, 1.2, -0.62), 0.022, 8, 4)
    # launch lever: fixed housing with a guard, and the pivoting T-handle
    m.box(D, (0, 1.0, -0.05), (0.36, 0.1, 0.5), bevel=0.02)
    m.box("Mat_GlowRed", (0, 1.052, -0.05), (0.05, 0.004, 0.4))
    for s in (-1, 1):
        m.tube(S_, [(s * 0.2, 1.02, -0.27), (s * 0.2, 1.22, -0.27), (s * 0.2, 1.22, 0.17), (s * 0.2, 1.02, 0.17)], 0.012, seg=8)
    g = m.group("LeverPivot", (0, 1.02, -0.05))
    m.cyl(S_, (-0.06, 1.02, -0.05), (0.06, 1.02, -0.05), 0.04, seg=14, g=g)
    m.cyl(S_, (0, 1.02, -0.05), (0, 1.54, -0.05), 0.022, seg=12, g=g)
    m.cyl("Mat_FlagRed", (-0.15, 1.56, -0.05), (0.15, 1.56, -0.05), 0.034, seg=14, bev=0.01, g=g)
    for s in (-1, 1):
        m.sph(D, (s * 0.155, 1.56, -0.05), 0.036, 10, 5, g=g)
    # status light
    m.cyl(D, (0.45, 1.01, 0.1), (0.45, 1.06, 0.1), 0.12, seg=18, bev=0.01)
    m.torus(S_, (0.45, 1.065, 0.1), 0.1, 0.012, (0, 1, 0), 18, 6)
    m.named("StatusLight", (0.45, 1.14, 0.1))
    m.sph("Mat_GlowRed", (0.45, 1.14, 0.1), 0.1, 20, 10, n="StatusLight")
    # "LeverPull": the T-handle swings 70 degrees towards the window. The game scrubs this clip by the lever angle.
    for f in range(0, 36, 5):
        m.key("LeverPivot", f, rot=(70.0 * f / 35, 0, 0))
    m.clip("LeverPull", 0, 35)


@model("Station", "station_briefing_board")
def briefing_board(m):
    D = "Mat_Dark"
    m.box(D, (0, 0, -0.04), (4.6, 2.7, 0.1), bevel=0.03)
    m.named("Screen", (0, 0, 0.02))
    m.box(SCREEN, (0, 0, 0.02), (4.4, 2.5, 0.02), n="Screen")
    m.named("Bar", (0, 1.02, 0.035))
    m.box("Mat_GlowAmber", (0, 1.02, 0.035), (4.4, 0.04, 0.01), n="Bar")
    for s in (-1, 1):                                                             # inner bezel lip
        m.box(METAL, (0, s * 1.275, 0.02), (4.5, 0.05, 0.05), bevel=0.012)
        m.box(METAL, (s * 2.225, 0, 0.02), (0.05, 2.6, 0.05), bevel=0.012)
        for t in (-1, 1):
            m.box("Mat_Silver", (s * 2.2, t * 1.25, 0.03), (0.16, 0.16, 0.05), bevel=0.02)       # corner brackets
            m.cyl(D, (s * 2.2, t * 1.25, 0.05), (s * 2.2, t * 1.25, 0.062), 0.03, seg=10)
        m.box(METAL, (s * 1.6, 0, -0.085), (0.2, 2.2, 0.03), bevel=0.008)        # wall mounts
    m.box("Mat_GlowCyan", (0, -1.33, 0.012), (1.2, 0.012, 0.012))


@model("Station", "station_knowledge_pod")
def knowledge_pod(m):
    D = "Mat_Dark"
    m.lathe(METAL, [(0.45, 0.0), (0.45, 0.06), (0.38, 0.12), (0.0, 0.12)], seg=36)
    m.lathe("Mat_GlowAmber", [(0.385, 0.118), (0.34, 0.126), (0.34, 0.118)], seg=36, closed=True)
    m.lathe(METAL, [(0.24, 0.12), (0.17, 0.2), (0.15, 0.6), (0.17, 1.06), (0.26, 1.14), (0.3, 1.14), (0.3, 1.19), (0.0, 1.19)], seg=36)
    for k in range(12):                                                           # fluting
        m.arc(D, 0.24, 1.04, 0.15, 0.185, k * 30 - 5, k * 30 + 5, 1)
    m.torus("Mat_Silver", (0, 0.6, 0), 0.165, 0.012, (0, 1, 0), 28, 6)
    m.lathe("Mat_GlowAmber", [(0.2, 1.19), (0.27, 1.19), (0.27, 1.2), (0.2, 1.2)], seg=36, closed=True)
    for k in range(3):                                                            # cradle prongs under the core
        a = math.radians(k * 120)
        d = V(math.sin(a), 0, math.cos(a))
        m.tube("Mat_Silver", [d * 0.12 + V(0, 1.19, 0), d * 0.2 + V(0, 1.3, 0), d * 0.17 + V(0, 1.4, 0)], 0.012, seg=6)
    m.named("Core", (0, 1.48, 0))
    m.sph("Mat_GlowAmber", (0, 1.48, 0), 0.15, 24, 12, n="Core")
    g = m.group("Ring", (0, 1.48, 0))                                             # flat here; the game tilts and spins it
    m.torus("Mat_Silver", (0, 1.48, 0), 0.32, 0.01, (0, 1, 0), 40, 6, g=g)
    for k in range(12):
        a = math.radians(k * 30)
        m.box("Mat_Silver", (math.sin(a) * 0.32, 1.48, math.cos(a) * 0.32), (0.09, 0.03, 0.035), rot=(0, k * 30, 0), bevel=0.008, g=g)


@model("Station", "station_service_tower")
def service_tower(m):
    D = "Mat_Dark"
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box(STEEL, (sx * 1.3, 16, sz * 1.3), (0.35, 32, 0.35), bevel=0.04)                # corner columns
            m.box(D, (sx * 1.3, 0.15, sz * 1.3), (0.7, 0.3, 0.7), bevel=0.05)
    m.box("Mat_Grey", (0, 16, -0.4), (1.0, 32, 1.0), bevel=0.03)                                  # elevator shaft
    for k in range(8):
        y = 2 + k * 4
        m.box(STEEL, (0, y, 0), (3.0, 0.2, 3.0), bevel=0.03)                                      # access deck
        for s in (-1, 1):                                                                          # railings
            m.cyl(STEEL, (-1.45, y + 1.0, s * 1.45), (1.45, y + 1.0, s * 1.45), 0.035, seg=6)
            m.cyl(STEEL, (s * 1.45, y + 1.0, -1.45), (s * 1.45, y + 1.0, 1.45), 0.035, seg=6)
            m.cyl(STEEL, (-1.45, y + 0.55, s * 1.45), (1.45, y + 0.55, s * 1.45), 0.025, seg=6)
            m.cyl(STEEL, (s * 1.45, y + 0.55, -1.45), (s * 1.45, y + 0.55, 1.45), 0.025, seg=6)
        if k < 7:                                                                                  # cross-bracing on all four faces
            a = -1.3 if k % 2 == 0 else 1.3
            for s in (-1.3, 1.3):
                m.cyl(STEEL, (a, y, s), (-a, y + 4, s), 0.06, seg=6)
                m.cyl(STEEL, (s, y, a), (s, y + 4, -a), 0.06, seg=6)
                m.cyl(STEEL, (-a, y, s), (a, y + 4, s), 0.04, seg=6)
                m.cyl(STEEL, (s, y, -a), (s, y + 4, a), 0.04, seg=6)
    for y in (6, 14, 22):                                                                          # swing arms towards the rocket (+X)
        m.truss(STEEL, (1.3, y, 0), (5.4, y, 0), 0.7, 0.05, bays=5)
        m.cyl(STEEL, (1.3, y + 2.6, 0), (5.2, y + 0.4, 0), 0.05, seg=6)
        m.box("Mat_White", (5.5, y, 0), (0.25, 0.9, 0.9), bevel=0.04)                             # umbilical plate
        m.box("Mat_GlowAmber", (5.63, y, 0), (0.02, 0.5, 0.12))
        m.tube("Mat_Carbon", [(5.5, y - 0.25, -0.2), (5.8, y - 0.8, -0.25), (6.1, y - 0.7, -0.3)], 0.08, seg=8)   # cryogenic hoses
        m.tube("Mat_Carbon", [(5.5, y + 0.1, 0.2), (5.85, y - 0.3, 0.15), (6.2, y - 0.15, 0.05)], 0.05, seg=8)
        m.tube("Mat_Silver", [(5.5, y + 0.3, 0.0), (5.9, y + 0.1, 0.0), (6.15, y + 0.2, 0.0)], 0.035, seg=8)
        m.sph("Mat_GlowAmber", (5.5, y + 0.6, 0), 0.14, 10, 5)
    m.box(STEEL, (0, 32.1, 0), (3.2, 0.25, 3.2), bevel=0.04)                                       # top deck
    m.cyl(D, (0, 32.2, 0), (0, 40.0, 0), 0.12, 0.04, seg=10)                                      # lightning mast
    for a in (0, 90, 180, 270):                                                                    # mast stays
        d = V(math.sin(math.radians(a + 45)), 0, math.cos(math.radians(a + 45)))
        m.cyl(D, d * 1.8 + V(0, 32.2, 0), V(0, 37.5, 0), 0.02, seg=4)
    m.sph("Mat_GlowRed", (0, 40.2, 0), 0.3, 12, 6)                                                 # aviation beacons
    m.sph("Mat_GlowRed", (1.3, 32.5, 1.3), 0.18, 10, 5)
    m.sph("Mat_GlowRed", (-1.3, 32.5, -1.3), 0.18, 10, 5)


@model("Station", "astronaut_engineer")
def astronaut(m):
    W, G, D, S_ = "Mat_White", "Mat_Grey", "Mat_Dark", "Mat_Silver"
    # ---- skeleton (the character faces +Z, so its left side is -X)
    m.bone("pelvis", (0, 0.9, 0), (0, 1.0, 0))
    m.bone("spine", (0, 1.0, 0), (0, 1.2, 0), "pelvis")
    m.bone("chest", (0, 1.2, 0), (0, 1.45, 0), "spine")
    m.bone("head", (0, 1.45, 0), (0, 1.8, 0), "chest")
    JOINTS = {}
    for s, sd in ((-1, "L"), (1, "R")):
        sh, el, wr = V(s * 0.3, 1.32, 0), V(s * 0.37, 1.04, 0.02), V(s * 0.39, 0.8, 0.08)
        hip, kn, an = V(s * 0.12, 0.86, 0), V(s * 0.13, 0.5, 0.02), V(s * 0.13, 0.13, 0)
        JOINTS[sd] = (sh, el, wr, hip, kn, an)
        m.bone("upperarm." + sd, sh, el, "chest")
        m.bone("lowerarm." + sd, el, wr, "upperarm." + sd)
        m.bone("hand." + sd, wr, wr + V(0, -0.1, 0.02), "lowerarm." + sd)
        m.bone("thigh." + sd, hip, kn, "pelvis")
        m.bone("calf." + sd, kn, an, "thigh." + sd)
        m.bone("foot." + sd, an, V(s * 0.13, 0.02, 0.16), "calf." + sd)

    # ---- torso: hard upper torso, waist ring, hips
    m.b = "chest"
    m.box(W, (0, 1.17, 0), (0.5, 0.5, 0.34), bevel=0.11)
    m.b = "pelvis"
    m.sph(W, (0, 0.9, 0), 0.2, 18, 9, scale=(1.05, 0.8, 0.8))
    m.lathe(S_, [(0.2, 0.93), (0.215, 0.95), (0.2, 0.97), (0.17, 0.95)], seg=24, closed=True, scale=(1.05, 1, 0.8))
    # ---- helmet with neck ring; the visor is its own mesh
    m.b = "chest"
    m.torus(S_, (0, 1.45, 0), 0.165, 0.024, (0, 1, 0), 24, 8)
    m.b = "head"
    m.sph(W, (0, 1.62, 0), 0.19, 24, 12)
    m.sph(D, (0, 1.63, 0.07), 0.5, 20, 10, scale=(0.335, 0.275, 0.235))
    m.named("Visor", (0, 1.63, 0.09))
    m.sph("Mat_Glass", (0, 1.63, 0.09), 0.5, 24, 12, scale=(0.3, 0.24, 0.26), n="Visor")
    for s in (-1, 1):
        m.box(G, (s * 0.19, 1.7, 0.05), (0.04, 0.05, 0.08), bevel=0.008)          # helmet lamps
        m.box("Mat_GlowCyan", (s * 0.19, 1.7, 0.093), (0.028, 0.034, 0.008))
    # ---- PLSS backpack, hoses, chest pack: all carried by the chest
    m.b = "chest"
    for s in (-1, 1):
        m.tube(G, [(s * 0.16, 1.36, -0.2), (s * 0.17, 1.46, -0.16), (s * 0.13, 1.52, -0.1)], 0.018, seg=8)   # life-support hoses
    m.box(G, (0, 1.15, -0.25), (0.42, 0.56, 0.2), bevel=0.05)
    m.box(W, (0, 1.42, -0.24), (0.34, 0.1, 0.16), bevel=0.03)
    m.box(D, (0, 1.0, -0.355), (0.3, 0.16, 0.014), bevel=0.004)
    for k in range(4):
        m.box(G, (0, 0.95 + k * 0.035, -0.364), (0.24, 0.012, 0.006))
    m.cyl(D, (0.12, 1.33, -0.345), (0.12, 1.33, -0.36), 0.045, seg=14)
    m.named("BackpackStatusLight", (0.12, 1.33, -0.365))
    m.sph("Mat_GlowGreen", (0.12, 1.33, -0.365), 0.03, 12, 6, n="BackpackStatusLight")
    m.cyl(S_, (-0.15, 1.42, -0.3), (-0.15, 1.76, -0.32), 0.006, seg=6)             # antenna
    m.sph("Mat_GlowAmber", (-0.15, 1.765, -0.32), 0.015, 8, 4)
    m.box(G, (0, 1.2, 0.19), (0.26, 0.17, 0.08), bevel=0.02)                       # chest telemetry pack
    m.box(SCREEN, (-0.035, 1.2, 0.232), (0.14, 0.09, 0.008))
    for i in range(3):
        m.box("Mat_GlowAmber" if i == 2 else "Mat_GlowGreen", (-0.08 + i * 0.045, 1.185 + i * 0.008, 0.238), (0.028, 0.03 + i * 0.016, 0.004))
    for i, mt in enumerate(("Mat_LedA", "Mat_LedA", "Mat_LedB")):
        m.sph(mt, (0.085, 1.235 - i * 0.035, 0.232), 0.012, 8, 4)
    m.box("Mat_GlowOrange", (0.17, 1.34, 0.168), (0.07, 0.07, 0.008), bevel=0.002)   # mission patch
    m.box("Mat_FlagBlue", (-0.345, 1.26, 0), (0.008, 0.06, 0.09))                     # shoulder flag
    m.box("Mat_FlagRed", (-0.346, 1.245, 0), (0.008, 0.012, 0.09))
    # ---- limbs
    for s, sd in ((-1, "L"), (1, "R")):
        sh, el, wr, hip, kn, an = JOINTS[sd]
        m.b = "upperarm." + sd
        m.sph(W, sh, 0.105, 16, 8)
        m.cyl(W, sh, el, 0.08, 0.07, seg=14)
        for t in (0.35, 0.65):                                                      # arm bellows
            m.torus(W, sh.lerp(el, t), 0.08, 0.012, el - sh, 14, 5)
        m.b = "lowerarm." + sd
        m.sph(W, el, 0.075, 14, 7)
        m.cyl(W, el, wr, 0.07, 0.06, seg=14)
        m.torus(S_, wr, 0.06, 0.012, wr - el, 14, 6)
        m.b = "hand." + sd
        m.sph(G, wr + V(0, -0.07, 0.015), 0.068, 12, 6, scale=(0.9, 1.15, 1))      # glove
        m.b = "thigh." + sd
        m.sph(W, hip, 0.105, 14, 7)
        m.cyl(W, hip, kn, 0.1, 0.085, seg=16)
        m.b = "calf." + sd
        m.sph(W, kn, 0.09, 14, 7)
        m.box(G, kn + V(0, 0, 0.075), (0.11, 0.12, 0.04), bevel=0.015)             # knee pad
        m.cyl(W, kn, an, 0.085, 0.075, seg=16)
        m.torus(S_, an + V(0, 0.01, 0), 0.078, 0.012, (0, 1, 0), 14, 6)
        m.b = "foot." + sd
        m.sph(W, an, 0.078, 12, 6)
        m.box(G, (s * 0.13, 0.065, 0.04), (0.17, 0.13, 0.31), bevel=0.04)          # boot
        m.box(D, (s * 0.13, 0.012, 0.04), (0.18, 0.024, 0.32), bevel=0.008)
    m.b = None

    # ---- animation. Angles are Unity Euler degrees about the character's own axes:
    # +X pitches forward (a hanging limb swings BACK), so forward swings are negative; +Y turns right.
    def gait(ph, A, K0, K1, B, E0, E1, lean, bob, twist):
        sn, cs = math.sin(ph), math.cos(ph)
        pose = {
            "pelvis": ((lean * 0.4, twist * sn, 0), (0, bob * (math.cos(2 * ph) - 1), 0)),
            "spine": (lean * 0.3, 0, 0),
            "chest": (lean * 0.3, -twist * 1.4 * sn, 0),
            "head": (-lean * 0.55, twist * 0.4 * sn, 0),
        }
        for sd, sg in (("L", 1.0), ("R", -1.0)):
            th = -A * sg * sn                                   # thigh swing
            knee = K0 + K1 * max(0.0, sg * cs) ** 1.5           # the knee folds while the leg swings forward
            arm = B * sg * sn                                   # arms swing against the legs
            pose["thigh." + sd] = (th, 0, 0)
            pose["calf." + sd] = (knee, 0, 0)
            pose["foot." + sd] = (max(-35.0, min(35.0, -(th + knee) * 0.8)), 0, 0)
            pose["upperarm." + sd] = (arm, 0, 0)
            pose["lowerarm." + sd] = (-(E0 + E1 * max(0.0, -sg * sn)), 0, 0)
            pose["hand." + sd] = (0, 0, 0)
        return pose

    def idle(ph):
        sn, cs = math.sin(ph), math.cos(ph)
        pose = {
            "pelvis": ((0, 0, 0.7 * sn), (0.006 * sn, -0.003 * (1 - cs), 0)),     # slow weight shift
            "spine": (0.6 * cs, 0, -0.5 * sn),
            "chest": (1.3 * cs, 0, -0.4 * sn),                                    # breathing
            "head": (-1.0 * cs, 2.5 * sn, 0),
        }
        for sd, sg in (("L", 1.0), ("R", -1.0)):
            pose["thigh." + sd] = (0, 0, -0.7 * sn)
            pose["calf." + sd] = (0, 0, 0)
            pose["foot." + sd] = (0, 0, 0)
            pose["upperarm." + sd] = (1.5 * cs, 0, sg * (1.0 + 0.8 * cs))
            pose["lowerarm." + sd] = (-6 - 1.5 * cs, 0, 0)
            pose["hand." + sd] = (0, 0, 0)
        return pose

    for f in range(0, 61, 2): m.pose(f, idle(2 * math.pi * f / 60))
    for f in range(0, 31): m.pose(70 + f, gait(2 * math.pi * f / 30, 26, 6, 42, 20, 12, 14, 4, 0.012, 5))
    for f in range(0, 21): m.pose(110 + f, gait(2 * math.pi * f / 20, 44, 18, 80, 48, 70, 15, 16, 0.03, 9))
    m.clip("Idle", 0, 60, loop=True)
    m.clip("Walk", 70, 100, loop=True)
    m.clip("Run", 110, 130, loop=True)


# ------------------------------------------------------------------------------------------------ room shell

def wall_run(m, length, ribs, clear=(), sign=False, conduits=True, x0=None, x1=None):
    """
    One wall in its own frame: x along the wall, inner face on z = 0, the room on the -z side. The slab is one
    piece from end to end (and 0.2 past both ends, into the corners), so there is no joint to leak.
    """
    T_, WL, WD = "Mat_Trim", "Mat_Wall", "Mat_WallDark"
    a = -length / 2 if x0 is None else x0
    b = length / 2 if x1 is None else x1
    m.box(WD, ((a + b) / 2, 2.5, 0.1), (b - a + 0.4, 5.2, 0.2))
    m.box(T_, ((a + b) / 2, 0.11, -0.01), (b - a, 0.22, 0.02), bevel=0.006)                    # baseboard
    m.box("Mat_Silver", ((a + b) / 2, 0.232, -0.008), (b - a, 0.012, 0.016))
    m.box(T_, ((a + b) / 2, 4.9, -0.01), (b - a, 0.2, 0.02), bevel=0.006)                       # crown moulding
    m.box(WD, ((a + b) / 2, 4.775, -0.006), (b - a, 0.05, 0.012))
    for x in ribs:                                                                               # structural ribs
        m.box(METAL, (x, 2.5, -0.045), (0.28, 4.56, 0.09), bevel=0.02)
        m.box(T_, (x, 0.3, -0.05), (0.34, 0.16, 0.1), bevel=0.015)
        m.box(T_, (x, 4.7, -0.05), (0.34, 0.16, 0.1), bevel=0.015)
        m.box("Mat_GlowCyan", (x, 2.5, -0.092), (0.03, 3.4, 0.01), n="AccentStrips")
    if conduits:                                                                                 # cable conduits under the crown
        for y, r in ((4.6, 0.022), (4.535, 0.016)):
            m.cyl("Mat_Silver", (a + 0.3, y, -0.115), (b - 0.3, y, -0.115), r, seg=8)
        x = a + 1.0
        while x < b - 0.5:
            m.box(T_, (x, 4.57, -0.105), (0.05, 0.14, 0.05), bevel=0.008)
            x += 2.0
    edges = [a] + sorted(ribs) + [b]
    for e0, e1 in zip(edges, edges[1:]):
        lo, hi = e0 + 0.22, e1 - 0.22
        w = hi - lo
        c = (lo + hi) / 2
        if w < 0.8 or any(c0 <= c <= c1 for c0, c1 in clear): continue
        # lower tech panel: raised plate with a vent grille, a status readout and indicator lamps
        m.box(WL, (c, 1.1, -0.015), (w, 1.5, 0.03), bevel=0.012)
        gw = min(1.2, w * 0.45)
        m.box(T_, (c - w / 2 + gw / 2 + 0.15, 0.75, -0.032), (gw, 0.5, 0.006))
        for k in range(6):
            m.box(WD, (c - w / 2 + gw / 2 + 0.15, 0.55 + k * 0.08, -0.037), (gw - 0.08, 0.03, 0.008))
        m.box(T_, (c + w / 2 - 0.5, 1.5, -0.033), (0.62, 0.24, 0.008), bevel=0.003)
        m.box(SCREEN, (c + w / 2 - 0.5, 1.5, -0.038), (0.54, 0.16, 0.004))
        for k, mt in enumerate(("Mat_GlowGreen", "Mat_GlowGreen", "Mat_GlowAmber")):
            m.box(mt, (c + w / 2 - 0.68 + k * 0.09, 1.28, -0.034), (0.05, 0.025, 0.008))
        # upper acoustic panel: a grid of padded tiles in a recessed frame
        top = 3.2 if sign else 4.35
        ph = top - 2.05
        m.box(T_, (c, 2.05 + ph / 2, -0.004), (w, ph, 0.008))
        nu = max(1, int(round(w / 0.85)))
        nv = max(1, int(round(ph / 0.75)))
        m.tiles(WL, (c, 2.05 + ph / 2, -0.02), (1, 0, 0), (0, 1, 0), w - 0.06, ph - 0.06, nu, nv, thick=0.03, gap=0.03, bevel=0.012)


@model("Station", "station_room_shell")
def room_shell(m):
    """Seamless walls, window wall, corner pilasters and coffered ceiling of the Mission Control room
    (interior x -10..10, z -8..8, height 5). Station-root coordinates."""
    T_, WD = "Mat_Trim", "Mat_WallDark"
    m.named("AccentStrips", (0, 0, 0))
    # south wall (z = -8): flat fascia behind the big wall sign
    m.M = T((0, 0, -8)) @ euler(0, 180, 0)
    wall_run(m, 20, [-8, -4, 0, 4, 8], sign=True)
    m.box(T_, (0, 3.78, -0.05), (11.4, 0.95, 0.1), bevel=0.02)
    m.box("Mat_Silver", (0, 3.27, -0.1), (11.4, 0.016, 0.012))
    # west wall (x = -10): the CAD desk bay stays flat. Wall x runs along world +z.
    m.M = T((-10, 0, 0)) @ euler(0, -90, 0)
    wall_run(m, 16, [-6.2, -3.3, 0.3, 4.2], clear=[(-3.3, 0.3)])
    # east wall (x = 10): the briefing board bay stays flat. Wall x runs along world -z.
    m.M = T((10, 0, 0)) @ euler(0, 90, 0)
    wall_run(m, 16, [-4.6, -1.1, 4.1, 6.4], clear=[(-1.1, 4.1)])
    # north wall (z = 8): sill, header and two piers round the panoramic window
    m.M = T((0, 0, 8))
    WB, WT, WH = 0.9, 3.8, 7.5
    m.box(WD, (0, WB / 2 - 0.05, 0.1), (20.4, WB + 0.1, 0.2))                                    # sill
    m.box(WD, (0, (WT + 5.1) / 2, 0.1), (20.4, 5.1 - WT, 0.2))                                   # header
    for s in (-1, 1):
        m.box(WD, (s * (WH + 10.2) / 2, 2.5, 0.1), (10.2 - WH, 5.2, 0.2))                        # piers
        m.box(METAL, (s * (WH + 0.16), (WB + WT) / 2, -0.045), (0.32, WT - WB, 0.09), bevel=0.02)   # window jambs
        m.box("Mat_GlowCyan", (s * (WH + 0.16), (WB + WT) / 2, -0.092), (0.03, WT - WB - 0.5, 0.01), n="AccentStrips")
        c = s * (WH + 0.32 + 10) / 2
        w = 10 - WH - 0.32 - 0.3
        m.box("Mat_Wall", (c, 1.1, -0.015), (w, 1.5, 0.03), bevel=0.012)
        m.tiles("Mat_Wall", (c, 3.1, -0.02), (1, 0, 0), (0, 1, 0), w - 0.06, 2.2, 2, 3, thick=0.03, gap=0.03, bevel=0.012)
    m.box(T_, (0, 0.11, -0.01), (20, 0.22, 0.02), bevel=0.006)
    m.box(T_, (0, 4.95, -0.01), (20, 0.1, 0.02))
    m.box(T_, (0, WB, -0.06), (WH * 2 + 0.2, 0.1, 0.3), bevel=0.02)                              # window ledge
    m.box(T_, (0, WT, -0.02), (WH * 2 + 0.2, 0.1, 0.14), bevel=0.02)                             # window head
    for i in range(-2, 3):
        m.box(T_, (i * 3.75, (WB + WT) / 2, 0.08), (0.07, WT - WB, 0.1), bevel=0.012)            # mullions
    m.box(METAL, (0, WB - 0.3, -0.012), (WH * 2, 0.34, 0.024), bevel=0.008)                      # sill fascia
    for i in range(-7, 8):
        m.box(WD, (i * 1.0, WB - 0.3, -0.026), (0.7, 0.18, 0.006))
    m.M = Matrix.Identity(4)
    # corner pilasters
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box(METAL, (sx * 9.83, 2.5, sz * 7.83), (0.34, 5.0, 0.34), bevel=0.04)
            m.box(T_, (sx * 9.81, 0.2, sz * 7.81), (0.42, 0.4, 0.42), bevel=0.03)
            m.box(T_, (sx * 9.81, 4.8, sz * 7.81), (0.42, 0.4, 0.42), bevel=0.03)
    # ceiling: one slab, coffer beams on the 4 m grid, an inset panel in every coffer
    m.box("Mat_Ceiling", (0, 5.1, 0), (20.4, 0.2, 16.4))
    for x in (-6, -2, 2, 6):
        m.box(T_, (x, 4.965, 0), (0.16, 0.07, 16), bevel=0.012)
    for z in (-4, 0, 4):
        m.box(T_, (0, 4.965, z), (20, 0.07, 0.16), bevel=0.012)
    for ix in range(5):
        for iz in range(4):
            m.box(WD, (-8 + ix * 4, 4.992, -6 + iz * 4), (3.4, 0.016, 3.4), bevel=0.005)
