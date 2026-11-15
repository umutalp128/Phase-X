# Launch vehicles. Each stands upright along +Y with its engine exits on y = 0 and its nose tip at y = 1.6
# (PhaseXModelLibrary.RocketModelHeight). The game scales them for the hologram stand, the launch pad and flight.
H = 1.6
I4 = Matrix.Identity(4)


def tube_y(m, mt, y0, y1, r, r2=None, seg=36, **kw):
    m.cyl(mt, (0, y0, 0), (0, y1, 0), r, r2, seg=seg, **kw)


def nose(m, mt, y0, r, length, blunt=0.0, seg=36, **kw):
    m.lathe(mt, ogive_pts(r, y0, length, 12, blunt), seg=seg, **kw)


def bells(m, mt, pts, y_top, length, r0, r1, seg=14, **kw):
    for x, z in pts:
        m.bell(mt, (x, y_top, z), (0, -1, 0), length, r0, r1, t=max(r1 * 0.1, 0.0015), seg=seg, n=6, **kw)


def ring_pts(n, r, phase=0.0):
    return [(r * math.sin(math.radians(phase + k * 360 / n)), r * math.cos(math.radians(phase + k * 360 / n))) for k in range(n)]


def hoops(m, mt, ys, r, t=0.0025, seg=36):
    for y in ys:
        m.torus(mt, (0, y, 0), r, t, (0, 1, 0), seg, 5)


def corrugation(m, mt, y0, y1, r, n=40, depth=0.003):
    """Vertical stiffening ribs round a stage (intertanks, interstages)."""
    for k in range(n):
        a = k * 360 / n
        m.arc(mt, y0, y1, r - 0.001, r + depth, a - 360 / n * 0.22, a + 360 / n * 0.22, 1)


def grid_fin(m, ang, r, y):
    """A deployed titanium lattice fin sticking straight out of the hull."""
    old = m.M
    m.M = old @ euler(0, ang, 0)
    w, d, t = 0.056, 0.05, 0.007
    c = V(0, y, -(r + d / 2 + 0.006))
    m.box("Mat_Alu", c + V(0, 0, d / 2 + 0.002), (0.03, 0.012, 0.012), bevel=0.002)      # hinge
    for s in (-1, 1):
        m.box("Mat_Alu", c + V(s * w / 2, 0, 0), (0.004, t, d), bevel=0.001)
        m.box("Mat_Alu", c + V(0, 0, s * d / 2), (w, t, 0.004), bevel=0.001)
    for k in (-1, 0, 1):
        m.box("Mat_Alu", c + V(k * w / 4, 0, 0), (0.002, t, d))
        m.box("Mat_Alu", c + V(0, 0, k * d / 4), (w, t, 0.002))
    m.M = old


def landing_leg(m, ang, r, y0, y1):
    """A folded carbon-fibre landing leg lying against the hull."""
    mid = y0 + (y1 - y0) * 0.55
    m.arc("Mat_Carbon", y0, mid, r, r + 0.009, ang - 13, ang + 13, 4)
    m.arc("Mat_Carbon", mid, y1, r, r + 0.007, ang - 7, ang + 7, 3)
    m.arc("Mat_Silver", y1 - 0.004, y1 + 0.006, r, r + 0.009, ang - 4, ang + 4, 2)


def falcon_core(m, r=0.072, booster=False):
    """Falcon 9 first stage (and, unless it is a side booster, its second stage and fairing)."""
    W, C, D = "Mat_White", "Mat_Carbon", "Mat_Dark"
    top1 = 1.0
    tube_y(m, D, 0.055, 0.085, r * 1.02, bev=0.004)                             # octaweb
    bells(m, "Mat_EngineBell", ring_pts(8, r * 0.7) + [(0, 0)], 0.07, 0.07, 0.007, 0.02)
    tube_y(m, W, 0.085, top1, r)
    hoops(m, "Mat_Silver", (0.36, 0.72), r, 0.0015)
    m.arc(D, 0.1, top1 - 0.02, r, r + 0.004, -4, 4, 1)                       # cable raceway
    for k in range(4):
        landing_leg(m, 45 + k * 90, r, 0.09, 0.4)
        grid_fin(m, 45 + k * 90, r, top1 - 0.035)
    if booster:
        tube_y(m, D, top1, top1 + 0.012, r * 1.005)
        nose(m, W, top1 + 0.012, r, 0.16, blunt=0.01)
        return
    m.arc("Mat_FlagRed", 0.5, 0.8, r, r + 0.002, 171, 189, 3)                      # red stripe
    tube_y(m, C, top1, 1.09, r * 1.004)                                         # carbon interstage
    tube_y(m, W, 1.09, 1.23, r)
    hoops(m, D, (1.092,), r, 0.002)
    fr = r * 1.38
    m.lathe(W, [(r, 1.23), (fr, 1.275), (fr, 1.42)], seg=36)                    # fairing boat-tail and barrel
    nose(m, W, 1.42, fr, H - 1.42, blunt=0.012)
    m.arc(D, 1.275, 1.42, fr - 0.001, fr + 0.001, 89.6, 90.4, 1)                 # fairing split line
    m.arc(D, 1.275, 1.42, fr - 0.001, fr + 0.001, 269.6, 270.4, 1)


def srb(m, ang, dist, r, y0, y1, cant=12.0, mt="Mat_White"):
    """Solid strap-on booster with a canted nosecone leaning in towards the core."""
    a = math.radians(ang)
    d = V(math.sin(a), 0, math.cos(a))
    old = m.M
    m.M = old @ T(d * dist)
    m.bell("Mat_EngineBell", (0, y0 + 0.04, 0), (0, -1, 0), 0.04, r * 0.3, r * 0.75, t=0.002, seg=16, n=5)
    tube_y(m, mt, y0 + 0.03, y1, r, seg=20)
    tube_y(m, "Mat_Dark", y0 + 0.03, y0 + 0.05, r * 1.02, seg=20)
    hoops(m, "Mat_Dark", (y0 + (y1 - y0) * 0.36, y0 + (y1 - y0) * 0.68), r, 0.0018, 20)
    m.sph(mt, (0, y1, 0), r, 20, 8)
    axis = V(0, math.cos(math.radians(cant)), 0) - d * math.sin(math.radians(cant))
    m.lathe(mt, ogive_pts(r, 0, r * 4.2, 8), (0, y1, 0), axis, 20)
    m.M = old
    for y in (y0 + 0.1, y1 - 0.06):                                             # attach struts
        m.cyl("Mat_Silver", d * (dist - r) + V(0, y, 0), d * (dist - r - 0.02) + V(0, y + 0.012, 0), 0.004, seg=5)


# ------------------------------------------------------------------------------------------------

@model("Rockets", "lv_small")
def lv_small(m):                        # Rocket Lab Electron
    r = 0.082
    C = "Mat_Carbon"
    tube_y(m, "Mat_Dark", 0.05, 0.08, r * 1.01, bev=0.004)
    bells(m, "Mat_Silver", ring_pts(8, r * 0.68) + [(0, 0)], 0.07, 0.07, 0.008, 0.024)
    tube_y(m, C, 0.08, 1.0, r)
    hoops(m, "Mat_Grey", (0.3, 0.62), r, 0.002)
    tube_y(m, "Mat_Silver", 1.0, 1.05, r * 1.01, bev=0.003)                      # interstage band
    tube_y(m, C, 1.05, 1.24, r)
    m.arc("Mat_White", 0.66, 0.9, r, r + 0.0018, 166, 194, 4)                    # white livery panel
    m.arc("Mat_White", 1.1, 1.2, r, r + 0.0018, 170, 190, 3)
    tube_y(m, "Mat_Grey", 1.238, 1.246, r * 1.008)
    nose(m, C, 1.246, r, H - 1.246, blunt=0.012)


@model("Rockets", "lv_pegasus_xl")
def lv_pegasus(m):                      # Northrop Grumman Pegasus XL
    r = 0.075
    W, D = "Mat_White", "Mat_Dark"
    m.bell("Mat_EngineBell", (0, 0.07, 0), (0, -1, 0), 0.07, 0.02, 0.055, t=0.004, seg=24, n=6)
    tube_y(m, W, 0.06, 0.74, r)                                                  # stage 1 (Orion 50S XL)
    tube_y(m, D, 0.74, 0.76, r * 1.01)
    tube_y(m, W, 0.76, 1.08, r)                                                  # stage 2
    tube_y(m, D, 1.08, 1.095, r * 1.01)
    tube_y(m, W, 1.095, 1.26, r)                                                 # stage 3 + payload
    nose(m, W, 1.26, r, H - 1.26, blunt=0.014)
    m.arc("Mat_FlagBlue", 0.86, 0.98, r, r + 0.0015, 60, 120, 4)
    # delta wing on a saddle fairing, on the -Z side (the "top" of the rocket when it flies horizontally)
    z = -(r + 0.006)
    m.box(D, (0, 0.6, -r * 0.92), (0.11, 0.52, 0.03), bevel=0.01)
    for s in (-1, 1):
        m.plate(W, [V(0, 0.9, z), V(s * 0.32, 0.46, z), V(s * 0.32, 0.38, z), V(0, 0.36, z)], 0.012)
        m.plate("Mat_Carbon", [V(0, 0.915, z), V(s * 0.328, 0.465, z), V(s * 0.32, 0.46, z), V(0, 0.9, z)], 0.014)   # leading edge
    for k in range(3):                                                           # three aft fins
        a = math.radians(60 + k * 120)
        d = V(math.sin(a), 0, math.cos(a))
        m.plate(W, [d * r + V(0, 0.07, 0), d * (r + 0.14) + V(0, 0.07, 0), d * (r + 0.14) + V(0, 0.12, 0), d * r + V(0, 0.26, 0)], 0.008)


@model("Rockets", "lv_firefly_alpha")
def lv_firefly(m):                      # Firefly Alpha
    r = 0.09
    C, G = "Mat_Carbon", "Mat_Grey"
    tube_y(m, "Mat_Dark", 0.055, 0.085, r * 1.01, bev=0.004)
    bells(m, "Mat_EngineBell", ring_pts(4, r * 0.52, 45), 0.075, 0.075, 0.012, 0.036, seg=18)
    tube_y(m, C, 0.085, 0.96, r)
    hoops(m, C, [0.13 + k * 0.075 for k in range(11)], r, 0.0032)                # carbon-composite ribs
    tube_y(m, "Mat_Dark", 0.96, 1.0, r * 1.012)
    tube_y(m, G, 1.0, 1.2, r)
    hoops(m, "Mat_Silver", (1.1,), r, 0.002)
    fr = r * 1.14
    m.lathe(G, [(r, 1.2), (fr, 1.25), (fr, 1.4)], seg=36)                        # slightly bulbous fairing
    nose(m, G, 1.4, fr, H - 1.4, blunt=0.014)
    m.arc("Mat_GlowOrange", 0.5, 0.56, r, r + 0.0016, 150, 210, 4)               # livery band
    m.arc("Mat_White", 1.28, 1.36, fr, fr + 0.0016, 164, 196, 3)


@model("Rockets", "lv_medium")
def lv_medium(m):                       # SpaceX Falcon 9
    falcon_core(m)


@model("Rockets", "lv_heavy")
def lv_heavy(m):                        # SpaceX Falcon Heavy
    r = 0.072
    falcon_core(m, r)
    for s in (-1, 1):
        m.M = T((s * (2 * r + 0.004), 0, 0))
        falcon_core(m, r, booster=True)
        m.M = I4
        for y in (0.14, 0.93):                                                   # core-to-booster struts
            m.cyl("Mat_Dark", (s * r * 0.9, y, 0), (s * (r + 0.01), y, 0), 0.008, seg=8)


@model("Rockets", "lv_atlas_v")
def lv_atlas(m):                        # ULA Atlas V 551
    r = 0.07
    B, W = "Mat_Bronze", "Mat_White"
    tube_y(m, "Mat_Dark", 0.075, 0.1, r * 1.01, bev=0.004)
    bells(m, "Mat_EngineBell", [(-0.03, 0), (0.03, 0)], 0.085, 0.085, 0.01, 0.028, seg=20)      # twin-chamber RD-180
    m.box("Mat_Silver", (0, 0.085, 0), (0.06, 0.02, 0.03), bevel=0.004)
    tube_y(m, B, 0.1, 0.93, r)                                                   # Common Core Booster
    hoops(m, B, [0.14 + k * 0.05 for k in range(16)], r, 0.0022)                 # isogrid / stiffening rings
    corrugation(m, B, 0.5, 0.58, r, 36, 0.0025)
    m.arc("Mat_Dark", 0.1, 0.93, r, r + 0.0035, -4, 4, 1)
    fr = 0.1
    m.lathe(W, [(r, 0.93), (r, 0.955), (fr, 1.01), (fr, 1.36)], seg=40)          # interstage, boat-tail and 5 m fairing over the Centaur
    nose(m, W, 1.36, fr, H - 1.36, blunt=0.014)
    hoops(m, "Mat_Silver", (0.957,), r, 0.002)
    m.arc("Mat_FlagBlue", 1.2, 1.26, fr, fr + 0.0016, 158, 202, 4)
    m.arc("Mat_Dark", 1.01, 1.36, fr - 0.001, fr + 0.001, 89.6, 90.4, 1)
    m.arc("Mat_Dark", 1.01, 1.36, fr - 0.001, fr + 0.001, 269.6, 270.4, 1)
    for ang in (90, 270):                                                        # two solid rocket boosters
        srb(m, ang, r + 0.03, 0.027, 0.02, 0.6, 12)


@model("Rockets", "lv_vulcan_centaur")
def lv_vulcan(m):                       # ULA Vulcan Centaur
    r = 0.095
    W, S = "Mat_White", "Mat_Silver"
    tube_y(m, "Mat_Dark", 0.075, 0.1, r * 1.01, bev=0.004)
    bells(m, "Mat_EngineBell", [(-0.044, 0), (0.044, 0)], 0.09, 0.09, 0.014, 0.04, seg=22)      # two BE-4
    tube_y(m, W, 0.1, 0.9, r)
    hoops(m, S, (0.3, 0.52), r, 0.002)
    m.arc("Mat_FlagRed", 0.14, 0.4, r, r + 0.0016, 140, 220, 6)                  # red livery sweep
    m.arc("Mat_FlagRed", 0.4, 0.5, r, r + 0.0016, 162, 198, 3)
    tube_y(m, "Mat_Dark", 0.9, 0.94, r * 1.008)
    tube_y(m, S, 0.94, 1.16, r)                                                  # Centaur V (stainless)
    hoops(m, "Mat_Dark", (1.0, 1.1), r, 0.0015)
    tube_y(m, W, 1.16, 1.38, r * 1.004)                                          # fairing
    nose(m, W, 1.38, r * 1.004, H - 1.38, blunt=0.016)
    m.arc("Mat_Dark", 1.16, 1.38, r, r + 0.002, 89.6, 90.4, 1)
    m.arc("Mat_Dark", 1.16, 1.38, r, r + 0.002, 269.6, 270.4, 1)
    for k in range(4):                                                           # four GEM 63XL solids
        srb(m, 45 + k * 90, r + 0.033, 0.03, 0.02, 0.62, 14)


@model("Rockets", "lv_saturn_v")
def lv_saturn(m):                       # NASA Saturn V
    W, D, S = "Mat_White", "Mat_Dark", "Mat_Silver"
    r1 = 0.08
    # ---- S-IC first stage: five F-1 engines, four engine fairings with fins
    bells(m, "Mat_EngineBell", ring_pts(4, r1 * 0.62, 45) + [(0, 0)], 0.085, 0.085, 0.012, 0.031, seg=22)
    for x, z in ring_pts(4, r1 * 0.62, 45) + [(0, 0)]:
        m.cyl(S, (x, 0.08, z), (x, 0.1, z), 0.014, seg=10)
    tube_y(m, D, 0.07, 0.1, r1 * 0.98)
    tube_y(m, W, 0.09, 0.6, r1, seg=48)
    for k in range(4):
        a = 45 + k * 90
        m.M = euler(0, a, 0)
        m.lathe(W, [(0.036, 0.0), (0.034, 0.06), (0.0, 0.2)], (0, 0.075, r1 * 0.74), (0, 1, 0), 16, scale=(1, 1, 1))   # conical engine fairing
        m.plate(D, [V(0, 0.08, r1 + 0.012), V(0, 0.08, r1 + 0.07), V(0, 0.12, r1 + 0.07), V(0, 0.22, r1 + 0.014)], 0.006)   # fin
        m.M = I4
        m.arc(D, 0.1, 0.33, r1, r1 + 0.0014, a - 22.5 + 45, a + 22.5 + 45, 5)    # black roll-pattern panels between the fins
    for k in range(4):                                                           # upper S-IC checker band
        m.arc(D, 0.5, 0.585, r1, r1 + 0.0014, k * 90, k * 90 + 45, 5)
    corrugation(m, W, 0.34, 0.4, r1, 48, 0.002)                                  # intertank
    corrugation(m, W, 0.19, 0.22, r1, 48, 0.002)
    for k in range(4):                                                           # "USA" / flag marks
        m.arc("Mat_FlagRed", 0.42, 0.48, r1, r1 + 0.0016, k * 90 + 18, k * 90 + 24, 1)
    # ---- S-II second stage
    corrugation(m, D, 0.6, 0.64, r1, 48, 0.002)                                  # corrugated interstage
    tube_y(m, W, 0.6, 0.96, r1, seg=48)
    hoops(m, D, (0.642,), r1, 0.0016, 48)
    corrugation(m, W, 0.9, 0.955, r1, 48, 0.002)
    for k in range(4):
        m.arc(D, 0.655, 0.69, r1, r1 + 0.0014, k * 90 + 10, k * 90 + 35, 3)
    # ---- tapered interstage and S-IVB third stage
    r3 = 0.052
    m.lathe(W, [(r1, 0.96), (r3, 1.03)], seg=48)
    for k in range(4):
        m.arc(D, 0.962, 0.985, r1 * 0.93, r1 * 0.985, k * 90, k * 90 + 45, 4)
    corrugation(m, D, 0.985, 1.028, (r1 + r3) / 2 * 0.86, 40, 0.004)
    tube_y(m, W, 1.03, 1.2, r3, seg=40)
    for k in range(4):
        m.arc(D, 1.035, 1.075, r3, r3 + 0.0014, k * 90 + 45, k * 90 + 90, 4)
    hoops(m, D, (1.14,), r3, 0.0014, 40)
    # ---- Spacecraft-LM Adapter, Service Module, Command Module
    r4 = 0.031
    m.lathe(W, [(r3, 1.2), (r4, 1.3)], seg=40)
    hoops(m, D, (1.2, 1.3), r3 * 0.99, 0.0012, 40)
    tube_y(m, S, 1.3, 1.37, r4, seg=32)
    hoops(m, W, (1.335,), r4, 0.0012, 32)
    for k in range(4):
        m.box(D, V(math.sin(math.radians(k * 90)) * (r4 + 0.003), 1.35, math.cos(math.radians(k * 90)) * (r4 + 0.003)), (0.006, 0.006, 0.006))
    m.lathe(W, [(r4, 1.37), (r4 * 1.02, 1.375), (0.008, 1.425), (0.0, 1.427)], seg=32)   # Apollo Command Module cone
    # ---- Launch Escape System: lattice tower, motor, canted nozzles
    m.truss(S, (0, 1.42, 0), (0, 1.5, 0), 0.02, 0.0016, bays=4)
    m.lathe(W, [(0.014, 1.5), (0.0125, 1.505), (0.0105, 1.505), (0.0105, 1.575)] + ogive_pts(0.0105, 1.575, H - 1.575, 6)[1:], seg=16)
    for k in range(4):
        a = math.radians(45 + k * 90)
        d = V(math.sin(a), 0, math.cos(a))
        m.bell("Mat_EngineBell", d * 0.008 + V(0, 1.508, 0), d * 0.6 + V(0, -1, 0), 0.016, 0.003, 0.007, t=0.001, seg=8, n=4)
