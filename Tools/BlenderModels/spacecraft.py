# Spacecraft modules. Coordinates are Unity's: +Y up, the bus is centred on the origin, attachment radius R = 0.85,
# the camera looks at the -Z face ("front"). Every module is modelled in place, so Unity just drops it on the bus.
R = 0.85
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
GOLD, GOLDD, ALU, SILVER, WHITE, DARK, CARBON = "Mat_GoldFoil", "Mat_GoldDark", "Mat_Alu", "Mat_Silver", "Mat_White", "Mat_Dark", "Mat_Carbon"
CELL, BELL, BERY, GLASS, GREY = "Mat_SolarCell", "Mat_EngineBell", "Mat_Beryllium", "Mat_Glass", "Mat_Grey"


def box_edges(m, mt, h, t, bevel=0.012):
    """Twelve struts along the edges of a box with half extents h."""
    for i in (-1, 1):
        for j in (-1, 1):
            m.box(mt, (i * h[0], j * h[1], 0), (t, t, h[2] * 2 + t), bevel=bevel)
            m.box(mt, (i * h[0], 0, j * h[2]), (t, h[1] * 2 + t, t), bevel=bevel)
            m.box(mt, (0, i * h[1], j * h[2]), (h[0] * 2 + t, t, t), bevel=bevel)


def star_tracker(m):
    """Star tracker on the front face: bracket, optical baffle tilted 20 degrees up, rim and lens."""
    root = V(-0.1, 0.6, -0.8)
    ax = V(0, math.sin(math.radians(20)), -math.cos(math.radians(20)))
    m.box(ALU, root, (0.22, 0.22, 0.2), bevel=0.02)
    m.cyl(SILVER, root + ax * 0.08, root + ax * 0.12, 0.1, seg=20, bev=0.008)
    # hollow conical baffle with three internal vanes
    prof = [(0.07, 0.1), (0.1, 0.32), (0.108, 0.32), (0.108, 0.3), (0.082, 0.1)]
    m.lathe(DARK, prof, root, ax, 24, closed=True)
    m.torus(SILVER, root + ax * 0.32, 0.104, 0.008, ax, 24, 6)
    for k in (0.17, 0.24):
        m.lathe(DARK, [(0.05, k), (0.09, k), (0.09, k + 0.006), (0.05, k + 0.006)], root, ax, 20, closed=True)
    m.sph(GLASS, root + ax * 0.13, 0.06, 16, 8, scale=(1, 0.35, 1), rot=align(ax))


def quilt_face(m, c, u, v, w, h, nu, nv, out):
    """Gold MLI blanket: darker seam sheet with pillowed foil tiles standing proud of it."""
    c = V(c); out = V(out)
    m.slab(GOLDD, c, u, v, w, h, 0.012)
    m.tiles(GOLD, c + out * 0.012, u, v, w - 0.02, h - 0.02, nu, nv, thick=0.022, gap=0.018, bevel=0.009)


def solar_panel(m, c, u, v, w, h, nu, nv, out, thick=0.03, frame=ALU, back=None):
    """Framed photovoltaic panel: aluminium frame, dark substrate, bevelled cells and silver busbars on the `out` side."""
    c = V(c); out = V(out).normalized(); u_ = V(u).normalized(); v_ = V(v).normalized()
    m.slab(frame, c, u, v, w, h, thick, bevel=0.008)
    m.slab(DARK, c + out * (thick / 2), u, v, w - 0.05, h - 0.05, 0.006)
    m.tiles(CELL, c + out * (thick / 2 + 0.006), u, v, w - 0.07, h - 0.07, nu, nv, thick=0.01, gap=0.014, bevel=0.003)
    for i in range(1, nu):      # busbars between the cell columns
        p = c + u_ * (i * (w - 0.07) / nu - (w - 0.07) / 2) + out * (thick / 2 + 0.008)
        m.slab(SILVER, p, u, v, 0.008, h - 0.08, 0.006)
    if back:
        m.slab(back, c - out * (thick / 2), u, v, w - 0.05, h - 0.05, 0.006)


# ------------------------------------------------------------------------------------------------ buses

@model("Spacecraft", "frame")
def frame(m):
    m.box(DARK, (0, 0, 0), (0.86, 1.06, 0.86), bevel=0.06)
    box_edges(m, "Mat_GlowCyan", (0.75, 0.85, 0.75), 0.05, 0.014)
    for i in (-1, 1):
        for j in (-1, 1):
            for k in (-1, 1):
                m.sph(SILVER, (i * 0.75, j * 0.85, k * 0.75), 0.055, 12, 6)
                m.cyl("Mat_GlowCyan", (i * 0.75, j * 0.85, k * 0.75), (i * 0.4, j * 0.5, k * 0.4), 0.014, seg=6)
    # diagonal bracing on the four side faces
    for s in (-1, 1):
        m.cyl("Mat_GlowCyan", (-0.75, -0.85, s * 0.75), (0.75, 0.85, s * 0.75), 0.012, seg=6)
        m.cyl("Mat_GlowCyan", (s * 0.75, -0.85, 0.75), (s * 0.75, 0.85, -0.75), 0.012, seg=6)


@model("Spacecraft", "str_cubesat")
def str_cubesat(m):
    m.box(DARK, (0, 0, 0), (1.5, 1.7, 1.5), bevel=0.02)
    box_edges(m, ALU, (0.775, 0.875, 0.775), 0.09, 0.014)                      # machined rails
    for i in (-1, 1):
        for j in (-1, 1):
            for k in (-1, 1):                                                     # corner brackets with a fastener
                m.box(SILVER, (i * 0.775, j * 0.875, k * 0.775), (0.17, 0.17, 0.17), bevel=0.025)
                m.cyl(DARK, (i * 0.775, j * 0.875, k * 0.862), (i * 0.775, j * 0.875, k * 0.87), 0.03, seg=10)
    for s in (-1, 1):
        quilt_face(m, (0, 0, s * 0.757), X, Y, 1.36, 1.56, 4, 4, (0, 0, s))       # front / aft blankets
        quilt_face(m, (0, s * 0.857, 0), X, Z, 1.36, 1.36, 4, 4, (0, s, 0))       # top / bottom blankets
        # side walls: machined access panels with recessed seams and fasteners
        m.box(ALU, (s * 0.755, 0, 0), (0.02, 1.56, 1.36), bevel=0.006)
        for k in (-1, 0, 1):
            m.box(DARK, (s * 0.766, k * 0.52, 0), (0.004, 0.012, 1.3))
        for a in (-0.6, 0.6):
            for b in (-0.7, -0.26, 0.26, 0.7):
                m.cyl(SILVER, (s * 0.76, b, a), (s * 0.772, b, a), 0.018, seg=8)
    star_tracker(m)
    m.sph("Mat_LedA", (0.62, 0.76, -0.80), 0.036, 12, 6)
    m.sph("Mat_LedB", (0.62, 0.64, -0.80), 0.036, 12, 6)
    for y in (0.76, 0.64):
        m.cyl(DARK, (0.62, y, -0.775), (0.62, y, -0.80), 0.05, seg=12)


@model("Spacecraft", "str_espa")
def str_espa(m):
    ap = 0.83                                    # apothem of the octagonal core: flats face +-X and +-Z
    rc = ap / math.cos(math.radians(22.5))
    octa = [(rc * math.sin(math.radians(22.5 + k * 45)), rc * math.cos(math.radians(22.5 + k * 45))) for k in range(8)]
    m.prism(GOLDD, octa, -0.88, 0.88)
    flat = 2 * ap * math.tan(math.radians(22.5))
    for k in range(8):
        a = math.radians(k * 45)
        n = V(math.sin(a), 0, math.cos(a))
        u = V(math.cos(a), 0, -math.sin(a))
        m.tiles(GOLD, n * (ap + 0.012), u, Y, flat - 0.05, 1.7, 2, 5, thick=0.024, gap=0.02, bevel=0.01)
        # seam strip down each corner
        cdir = V(math.sin(a + math.radians(22.5)), 0, math.cos(a + math.radians(22.5)))
        m.cyl(GOLDD, cdir * rc + V(0, -0.87, 0), cdir * rc + V(0, 0.87, 0), 0.018, seg=8)
    for s in (-1, 1):                            # deck adapter rings
        m.lathe(ALU, [(0.74, s * 0.88), (0.92, s * 0.88), (0.94, s * 0.90), (0.94, s * 0.94), (0.92, s * 0.96), (0.74, s * 0.96)], seg=40, closed=True)
        m.lathe(DARK, [(0.74, s * 0.9), (0.74, s * 0.93)], seg=32)
        for k in range(12):
            a = math.radians(k * 30 + 15)
            p = V(math.sin(a) * 0.86, s * 0.965, math.cos(a) * 0.86)
            m.cyl(SILVER, p, p + V(0, s * 0.012, 0), 0.022, seg=8)
        for k in range(4):                        # mounting lugs
            a = k * 90 + 45
            m.M = euler(0, a, 0)
            m.box(SILVER, (0, s * 0.92, 0.95), (0.2, 0.1, 0.16), bevel=0.02)
            m.cyl(DARK, (0, s * 0.86, 0.98), (0, s * 0.98, 0.98), 0.035, seg=10)
            m.M = Matrix.Identity(4)
    star_tracker(m)
    for p, mt in (((0.3, 0.975, -0.68), "Mat_LedA"), ((0.42, 0.975, -0.6), "Mat_LedB")):
        m.cyl(DARK, (p[0], 0.96, p[2]), (p[0], 0.975, p[2]), 0.05, seg=12)
        m.sph(mt, p, 0.036, 12, 6)


# ------------------------------------------------------------------------------------------------ Apollo

def lunar_module(m, g=None, beacon=False, antennas=False):
    """
    Apollo Lunar Module, upright, footpads on y = 0, front (hatch and ladder) towards -Z. About 2.5 wide across the
    footpads and 1.4 tall; the caller scales and places it through m.M.
    """
    kw = dict(g=g)
    ap = 0.62
    rc = ap / math.cos(math.radians(22.5))
    octa = [(rc * math.sin(math.radians(22.5 + k * 45)), rc * math.cos(math.radians(22.5 + k * 45))) for k in range(8)]
    # descent stage: octagonal, wrapped in gold foil
    m.prism(GOLDD, octa, 0.36, 0.74, **kw)
    flat = 2 * ap * math.tan(math.radians(22.5))
    for k in range(8):
        a = math.radians(k * 45)
        n = V(math.sin(a), 0, math.cos(a)); u = V(math.cos(a), 0, -math.sin(a))
        m.tiles(GOLD, n * (ap + 0.01) + V(0, 0.55, 0), u, Y, flat - 0.03, 0.36, 2, 2, thick=0.022, gap=0.016, bevel=0.009, **kw)
    m.prism(DARK, [(x * 0.97, z * 0.97) for x, z in octa], 0.74, 0.765, **kw)
    m.prism(DARK, [(x * 0.9, z * 0.9) for x, z in octa], 0.335, 0.36, **kw)
    # descent engine
    m.cyl(ALU, (0, 0.36, 0), (0, 0.3, 0), 0.12, seg=16, **kw)
    m.bell(BELL, (0, 0.31, 0), (0, -1, 0), 0.25, 0.07, 0.25, t=0.012, seg=24, **kw)
    # landing gear on the four flats facing +-X and +-Z
    for k in range(4):
        m.M_leg = euler(0, k * 90, 0)
        Rk = m.M_leg
        def P(x, y, z): return Rk @ V(x, y, z)
        foot = P(0, 0.035, -1.22)
        m.cyl(SILVER, P(0, 0.66, -0.64), P(0, 0.34, -0.93), 0.036, seg=10, **kw)        # upper strut (shock absorber housing)
        m.cyl(ALU, P(0, 0.36, -0.91), foot, 0.024, seg=10, **kw)                         # lower strut (piston)
        m.torus(DARK, P(0, 0.35, -0.92), 0.036, 0.012, P(0, -0.74, -0.67) - P(0, 0, 0), 12, 6, **kw)
        for s in (-1, 1):                                                                 # secondary struts
            m.cyl(ALU, P(s * 0.25, 0.4, -0.6), P(0, 0.3, -0.96), 0.014, seg=8, **kw)
            m.cyl(ALU, P(s * 0.25, 0.7, -0.6), P(0, 0.52, -0.77), 0.012, seg=8, **kw)
        m.lathe(GOLD, [(0.0, 0.0), (0.12, 0.0), (0.17, 0.035), (0.17, 0.05), (0.11, 0.022), (0.0, 0.022)], foot - V(0, 0.035, 0), Y, 20, **kw)
        m.sph(SILVER, foot, 0.03, 10, 5, **kw)
    # boarding ladder down the front leg
    for s in (-1, 1):
        m.cyl(SILVER, (s * 0.07, 0.72, -0.66), (s * 0.07, 0.3, -1.03), 0.008, seg=6, **kw)
    for i in range(7):
        t = i / 6
        p = V(0, 0.7 - 0.38 * t, -0.68 - 0.33 * t)
        m.cyl(SILVER, p + V(-0.07, 0, 0), p + V(0.07, 0, 0), 0.007, seg=6, **kw)
    m.box(DARK, (0, 0.775, -0.74), (0.34, 0.015, 0.3), **kw)                               # porch
    # ascent stage: faceted pressure cabin
    m.lathe(GREY, [(0.0, 0.0), (0.36, 0.0), (0.44, 0.1), (0.44, 0.34), (0.34, 0.47), (0.0, 0.47)], (0, 0.78, 0.0), Y, 8, **kw)
    m.box(GREY, (0, 1.0, -0.33), (0.6, 0.36, 0.3), bevel=0.07, **kw)                       # crew cabin front
    m.box(DARK, (0, 0.98, -0.485), (0.5, 0.3, 0.02), bevel=0.006, **kw)                    # dark front face
    for s in (-1, 1):                                                                       # triangular recessed windows
        tri = [V(s * 0.06, 1.1, -0.5), V(s * 0.24, 1.1, -0.5), V(s * 0.22, 0.97, -0.5)]
        m.plate(ALU, [p + V(0, 0, 0.002) + (p - sum(tri, V(0, 0, 0)) / 3) * 0.18 for p in tri], 0.012, **kw)
        m.plate(GLASS, tri, 0.02, **kw)
    m.box(ALU, (0, 0.87, -0.5), (0.2, 0.17, 0.014), bevel=0.004, **kw)                      # EVA hatch
    m.box(DARK, (0, 0.87, -0.508), (0.15, 0.12, 0.006), **kw)
    for s in (-1, 1):                                                                       # propellant tanks in their bays
        m.sph(GREY, (s * 0.52, 0.98, 0.08), 0.21, 18, 9, **kw)
        m.torus(SILVER, (s * 0.52, 0.98, 0.08), 0.212, 0.008, X, 20, 6, **kw)
        m.box(GOLD, (s * 0.38, 0.86, 0.3), (0.2, 0.12, 0.2), bevel=0.02, **kw)
        # RCS quads on outriggers
        q = V(s * 0.6, 1.18, -0.3)
        m.cyl(ALU, (s * 0.4, 1.12, -0.24), q, 0.012, seg=6, **kw)
        m.box(DARK, q, (0.07, 0.07, 0.07), bevel=0.01, **kw)
        for d in ((0, 1, 0), (0, -1, 0), (s, 0, 0), (0, 0, -1)):
            m.bell(BELL, q + V(d) * 0.03, d, 0.05, 0.008, 0.024, t=0.004, seg=10, n=4, **kw)
    m.box(GREY, (0, 1.0, 0.4), (0.44, 0.3, 0.18), bevel=0.05, **kw)                         # aft equipment bay
    m.box(GOLD, (0, 1.0, 0.495), (0.36, 0.22, 0.014), bevel=0.004, **kw)
    # docking tunnel and drogue ring on top
    m.cyl(ALU, (0, 1.25, 0.0), (0, 1.38, 0.0), 0.15, seg=24, bev=0.01, **kw)
    m.torus(SILVER, (0, 1.385, 0.0), 0.135, 0.016, Y, 24, 8, **kw)
    m.cyl(DARK, (0, 1.37, 0.0), (0, 1.39, 0.0), 0.11, seg=20, **kw)
    if antennas:
        m.cyl(ALU, (0.2, 1.24, -0.2), (0.2, 1.36, -0.26), 0.01, seg=6, **kw)              # rendezvous radar
        m.dish(WHITE, (0.2, 1.36, -0.26), (0, 0.3, -1), 0.09, 0.03, t=0.006, seg=16, n=4, **kw)
        m.cyl(ALU, (-0.3, 1.2, 0.2), (-0.42, 1.42, 0.3), 0.008, seg=6, **kw)               # S-band steerable dish
        m.dish(WHITE, (-0.42, 1.42, 0.3), (-0.4, 0.8, 0.3), 0.11, 0.035, t=0.006, seg=16, n=4, **kw)
    if beacon:
        m.named("Beacon", (0, 0, 0))
        m.sph("Mat_GlowGreen", (0, 1.56, 0), 0.075, 12, 6, g=g, n="Beacon")


@model("Spacecraft", "str_apollo_csm_lm")
def str_apollo(m):
    # ---- Service Module
    m.ycyl(SILVER, (0, 0, 0), 0.84, 1.8, seg=48, bev=0.012)
    for k in range(6):                                                    # sector panel lines
        a = k * 60 + 10
        m.arc(DARK, -0.88, 0.88, 0.838, 0.846, a - 0.35, a + 0.35, 1)
    for y in (-0.55, 0.62):
        m.arc(DARK, y - 0.004, y + 0.004, 0.838, 0.846, 0, 360, 48)
    for k in range(4):                                                    # radiator panels: white frame, gold field
        a = k * 90 + 45
        m.arc(WHITE, -0.7, 0.4, 0.84, 0.856, a - 26, a + 26, 8)
        m.arc(GOLD, -0.64, 0.34, 0.85, 0.864, a - 22, a + 22, 8)
        for j in range(1, 6):
            yy = -0.64 + j * 0.98 / 6
            m.arc(WHITE, yy - 0.006, yy + 0.006, 0.86, 0.868, a - 22, a + 22, 6)
    m.lathe(DARK, [(0.80, 0.88), (0.86, 0.88), (0.86, 0.95), (0.80, 0.95)], seg=48, closed=True)      # umbilical / fairing ring
    m.box(DARK, (0, 0.9, -0.87), (0.16, 0.2, 0.06), bevel=0.015)                                       # umbilical housing
    m.lathe(SILVER, [(0.3, -0.9), (0.82, -0.9), (0.84, -0.93), (0.3, -0.95)], seg=48, closed=True)    # aft heat shield
    # ---- Command Module: silver heat-shield shoulder, white conical cabin, docking tunnel
    cm = [(0.0, 0.95), (0.80, 0.95), (0.845, 0.985), (0.845, 1.02), (0.80, 1.05)]
    m.lathe(SILVER, cm + [(0.0, 1.05)], seg=48)
    cone = [(0.80, 1.05), (0.19, 1.80), (0.15, 1.85), (0.0, 1.85)]
    m.lathe(WHITE, [(0.0, 1.05)] + cone, seg=48)
    slope = math.degrees(math.atan2(0.61, 0.75))
    for k in range(4):                                                    # recessed crew windows + frames
        for s in (-1, 1):
            if k % 2 == 1 and s == 1: continue
            m.M = euler(0, k * 90 + s * 22, 0)
            r_at = 0.80 - 0.61 * (1.34 - 1.05) / 0.75
            m.box(ALU, (0, 1.34, -r_at - 0.004), (0.19, 0.15, 0.016), rot=(slope, 0, 0), bevel=0.004)
            m.box(GLASS, (0, 1.34, -r_at - 0.012), (0.14, 0.1, 0.012), rot=(slope, 0, 0), bevel=0.003)
            m.M = Matrix.Identity(4)
    r_h = 0.80 - 0.61 * (1.5 - 1.05) / 0.75
    m.box(ALU, (0, 1.5, -r_h - 0.004), (0.2, 0.22, 0.016), rot=(slope, 0, 0), bevel=0.004)             # side hatch
    m.box(GLASS, (0, 1.53, -r_h - 0.02), (0.07, 0.07, 0.012), rot=(slope, 0, 0), bevel=0.003)
    for k in range(4):                                                    # CM reaction-control thruster ports
        m.M = euler(0, k * 90 + 45, 0)
        m.box(DARK, (0, 1.14, -0.745), (0.07, 0.05, 0.02), rot=(slope, 0, 0))
        m.M = Matrix.Identity(4)
    m.cyl(ALU, (0, 1.85, 0), (0, 2.0, 0), 0.115, seg=24, bev=0.008)                                     # docking tunnel
    m.torus(SILVER, (0, 2.0, 0), 0.115, 0.018, Y, 24, 8)
    m.cyl(DARK, (0, 2.0, 0), (0, 2.05, 0), 0.08, seg=16)
    # ---- Lunar Module, docked nose-to-nose (upside down). One named child, so the flight sim can undock it.
    g = m.group("DockedLunarModule")
    m.M = T((0, 3.45, 0)) @ euler(0, 0, 180)
    lunar_module(m, g, antennas=True)
    m.M = Matrix.Identity(4)


@model("Spacecraft", "prop_apollo_sps_lmde")
def prop_apollo(m):
    # AJ10-137 Service Propulsion System
    m.cyl(ALU, (0, -0.9, 0), (0, -1.0, 0), 0.2, seg=24, bev=0.01)
    m.cyl(GOLD, (0, -0.94, 0), (0, -1.06, 0), 0.13, seg=20)
    m.bell(BELL, (0, -1.0, 0), (0, -1, 0), 0.93, 0.11, 0.63, t=0.016, seg=40, n=12, glow="Mat_GlowBlue", glow_r=0.8)
    for i, s in enumerate((0.25, 0.5, 0.75)):                        # stiffener hoops on the radiation-cooled extension
        r = 0.11 + (0.63 - 0.11) * (2 * s - s * s) ** 0.7
        m.torus(SILVER if i == 0 else BELL, (0, -1.0 - 0.93 * s, 0), r + 0.004, 0.012, Y, 40, 6)
    for a in (45, 135):                                               # gimbal actuators
        d = V(math.sin(math.radians(a)), 0, math.cos(math.radians(a)))
        m.cyl(SILVER, d * 0.5 + V(0, -0.92, 0), d * 0.2 + V(0, -1.12, 0), 0.02, seg=8)
        m.cyl(ALU, d * 0.5 + V(0, -0.92, 0), d * 0.38 + V(0, -1.0, 0), 0.032, seg=8)
    # four Marquardt R-4D reaction-control quads round the Service Module
    for i in range(4):
        m.M = euler(0, i * 90, 0)
        m.box(ALU, (0, 0.55, R + 0.045), (0.17, 0.17, 0.09), bevel=0.02)
        m.box(GOLD, (0, 0.55, R + 0.0), (0.24, 0.24, 0.02), bevel=0.006)
        for d in ((0, 1, 0), (0, -1, 0), (1, 0, 0), (-1, 0, 0)):
            o = V(0, 0.55, R + 0.085) + V(d) * 0.085 + V(0, 0, 0.02)
            m.cyl(SILVER, o - V(d) * 0.03, o, 0.022, seg=10)
            m.bell(BELL, o, V(d) + V(0, 0, 0.18), 0.085, 0.012, 0.04, t=0.005, seg=14, n=5)
        m.M = Matrix.Identity(4)


@model("Spacecraft", "pl_apollo_alsep")
def pl_alsep(m):
    # central station
    m.box(WHITE, (0.95, 2.9, 0), (0.32, 0.34, 0.5), bevel=0.025)
    m.box(GOLD, (1.115, 2.9, 0), (0.014, 0.28, 0.44), bevel=0.004)
    for z in (-0.15, 0, 0.15):
        m.box(SILVER, (1.125, 2.9, z), (0.008, 0.26, 0.012))
    m.cyl(SILVER, (1.0, 3.07, -0.1), (1.0, 3.2, -0.1), 0.008, seg=6)                    # helical antenna mast
    m.helix(GOLD, (1.0, 3.2, -0.1), Y, 0.018, 0.026, 0.03, 4, 0.006, steps=10)
    # passive seismometer: gold drum with a wide mylar skirt
    m.cyl(GOLD, (0.82, 2.9, 0.45), (1.08, 2.9, 0.45), 0.13, seg=24, bev=0.02)
    m.lathe(GOLDD, [(0.13, 0.0), (0.21, -0.02), (0.21, -0.012), (0.13, 0.012)], (1.08, 2.9, 0.45), X, 24, closed=True)
    m.cyl(SILVER, (0.80, 2.9, 0.45), (0.82, 2.9, 0.45), 0.05, seg=12)
    # laser ranging retroreflector: tilted aluminium tray of blue corner cubes
    m.M = T((1.0, 2.92, -0.48)) @ euler(0, 0, -25)
    m.box(ALU, (0, 0, 0), (0.05, 0.34, 0.34), bevel=0.01)
    m.tiles("Mat_GlowBlue", (0.03, 0, 0), Y, Z, 0.3, 0.3, 5, 5, thick=0.014, gap=0.016, bevel=0.004)
    m.cyl(SILVER, (-0.02, -0.17, 0.1), (-0.12, -0.2, 0.1), 0.01, seg=6)
    m.cyl(SILVER, (-0.02, -0.17, -0.1), (-0.12, -0.2, -0.1), 0.01, seg=6)
    m.M = Matrix.Identity(4)
    # the flag on its pole (stowed sideways against the descent stage)
    m.cyl(SILVER, (0.89, 2.75, 0), (1.61, 2.75, 0), 0.009, seg=8)
    m.sph(GOLD, (1.615, 2.75, 0), 0.016, 8, 4)
    m.cyl(SILVER, (1.58, 2.75, 0), (1.58, 2.75, 0.27), 0.005, seg=6)                    # top crossbar keeps it unfurled
    for i in range(7):
        x = 1.385 + (i + 0.5) * 0.19 / 7
        m.box("Mat_FlagRed" if i % 2 == 0 else WHITE, (x, 2.75, 0.14), (0.19 / 7 - 0.0005, 0.008, 0.25))
    m.box("Mat_FlagBlue", (1.385 + 0.19 * 5.5 / 7, 2.75, 0.065), (0.19 * 3 / 7, 0.012, 0.1))


# ------------------------------------------------------------------------------------------------ power

@model("Spacecraft", "pwr_body")
def pwr_body(m):
    for s in (-1, 1):
        solar_panel(m, (s * (R + 0.035), 0, 0), Z, Y, 1.14, 1.34, 3, 4, (s, 0, 0), thick=0.03)
        for y in (-0.6, 0.6):
            for z in (-0.5, 0.5):
                m.cyl(SILVER, (s * (R + 0.0), y, z), (s * (R + 0.056), y, z), 0.022, seg=8)


@model("Spacecraft", "pwr_array")
def pwr_array(m):
    for s in (-1, 1):
        # root boom and yoke truss
        m.cyl(ALU, (s * R, 0, 0), (s * (R + 0.3), 0, 0), 0.04, seg=12, bev=0.008)
        m.sph(SILVER, (s * (R + 0.3), 0, 0), 0.06, 12, 6)
        for k in (-1, 1):
            m.cyl(SILVER, (s * (R + 0.3), 0, 0), (s * (R + 0.6), 0, k * 0.6), 0.018, seg=8)
            m.cyl(SILVER, (s * (R + 0.45), 0, k * 0.3), (s * (R + 0.6), 0, 0), 0.012, seg=6)
        m.cyl(SILVER, (s * (R + 0.6), 0, -0.62), (s * (R + 0.6), 0, 0.62), 0.016, seg=8)
        for i in range(3):
            x = s * (R + 0.6 + 0.62 + i * 1.28)
            solar_panel(m, (x, 0, 0), X, Z, 1.22, 1.5, 4, 6, (0, 1, 0), thick=0.035, back=GOLDD)
            joint = s * (R + 0.6 + i * 1.28)
            for k in (-1, 1):                                    # deployment hinges
                m.cyl(SILVER, (joint, 0, k * 0.5 - 0.07), (joint, 0, k * 0.5 + 0.07), 0.03, seg=12, bev=0.006)
                m.box(ALU, (joint, 0, k * 0.5), (0.1, 0.012, 0.09), bevel=0.003)
        # rear stiffener spars
        m.box(ALU, (s * (R + 0.6 + 1.92), -0.036, 0), (3.84, 0.03, 0.05), bevel=0.006)
        for k in (-1, 1):
            m.box(ALU, (s * (R + 0.6 + 1.92), -0.03, k * 0.55), (3.84, 0.018, 0.03))


@model("Spacecraft", "pwr_rtg")
def pwr_rtg(m):
    m.M = T((0.8, -0.55, -0.8)) @ euler(35, 45, 0)
    m.ycyl(BELL, (0, 0, 0), 0.15, 0.92, seg=24, bev=0.02)
    for s in (-1, 1):
        m.lathe(DARK, [(0.15, s * 0.46), (0.19, s * 0.47), (0.19, s * 0.5), (0.1, s * 0.54), (0.0, s * 0.54)], seg=24)
        m.lathe("Mat_RtgGlow", [(0.105, s * 0.541), (0.105, s * 0.548)], seg=20)
    for k in range(8):                                   # radial heat-rejection fins, tapered at both ends
        a = math.radians(k * 45)
        d = V(math.sin(a), 0, math.cos(a)); side = V(math.cos(a), 0, -math.sin(a))
        m.plate(DARK, [d * 0.14 + V(0, -0.42, 0), d * 0.33 + V(0, -0.34, 0), d * 0.36 + V(0, 0, 0), d * 0.33 + V(0, 0.34, 0), d * 0.14 + V(0, 0.42, 0)], 0.014)
        # glowing plutonium heat-source slots between the fins
        a2 = math.radians(k * 45 + 22.5)
        d2 = V(math.sin(a2), 0, math.cos(a2)); s2 = V(math.cos(a2), 0, -math.sin(a2))
        m.slab("Mat_RtgGlow", d2 * 0.152, s2, Y, 0.05, 0.72, 0.012, bevel=0.004)
    for y in (-0.3, 0, 0.3):
        m.torus(SILVER, (0, y, 0), 0.153, 0.008, Y, 24, 6)
    m.cyl(ALU, (0, 0.0, 0), (-0.02, 0.1, 0.34), 0.03, seg=8)       # mounting strut back to the bus
    m.M = Matrix.Identity(4)


# ------------------------------------------------------------------------------------------------ propulsion

@model("Spacecraft", "prop_coldgas")
def prop_coldgas(m):
    corners = [V(-0.6, -0.965, -0.6), V(0.6, -0.965, -0.6), V(0.6, -0.965, 0.6), V(-0.6, -0.965, 0.6)]
    for i, c in enumerate(corners):
        m.cyl(DARK, (c.x, -0.9, c.z), (c.x, -1.0, c.z), 0.07, seg=16, bev=0.01)
        m.bell(SILVER, (c.x, -0.99, c.z), (0, -1, 0), 0.085, 0.018, 0.062, t=0.006, seg=18, n=6, glow="Mat_GlowCyan", glow_r=0.75)
        m.cyl(SILVER, c, corners[(i + 1) % 4], 0.014, seg=8)      # feed manifold
        m.sph(SILVER, c, 0.024, 10, 5)
        m.box(ALU, (c.x * 0.5 + corners[(i + 1) % 4].x * 0.5, -0.94, c.z * 0.5 + corners[(i + 1) % 4].z * 0.5), (0.07, 0.05, 0.07), bevel=0.01)   # latch valve
    bottle = V(0.35, -1.06, -0.3)
    m.sph(GOLD, bottle, 0.15, 24, 12)
    m.torus(GOLDD, bottle, 0.151, 0.012, Y, 24, 6)
    m.torus(GOLDD, bottle, 0.151, 0.008, X, 24, 6)
    m.cyl(SILVER, bottle + V(0, 0.14, 0), bottle + V(0, 0.19, 0), 0.03, seg=10)
    m.tube(SILVER, [bottle + V(0, 0.17, 0), V(0.35, -0.9, -0.45), V(0.35, -0.965, -0.6)], 0.012, seg=6)
    for k in (-1, 1):
        m.cyl(ALU, bottle + V(k * 0.1, 0.1, 0), V(0.35 + k * 0.14, -0.9, -0.3), 0.01, seg=6)


@model("Spacecraft", "prop_biprop")
def prop_biprop(m):
    m.cyl(ALU, (0, -0.9, 0), (0, -1.0, 0), 0.17, seg=24, bev=0.01)
    m.cyl(SILVER, (0, -1.0, 0), (0, -1.06, 0), 0.1, seg=20, bev=0.008)          # injector head
    m.torus(GOLD, (0, -1.07, 0), 0.085, 0.022, Y, 24, 8)                         # gold-wrapped throat
    m.bell(BELL, (0, -1.05, 0), (0, -1, 0), 0.63, 0.065, 0.37, t=0.013, seg=36, n=11, glow="Mat_GlowBlue", glow_r=0.8)
    m.torus(SILVER, (0, -1.672, 0), 0.378, 0.011, Y, 36, 6)
    for s in (0.3, 0.6):
        r = 0.065 + (0.37 - 0.065) * (2 * s - s * s) ** 0.7
        m.torus(BELL, (0, -1.05 - 0.63 * s, 0), r + 0.004, 0.008, Y, 36, 6)
    for s in (-1, 1):                                                             # fuel / oxidiser tanks with gold equators
        c = V(s * 0.55, -1.15, 0.45)
        m.sph(WHITE, c, 0.225, 28, 14)
        m.torus(GOLD, c, 0.226, 0.014, Y, 28, 6)
        m.cyl(SILVER, c + V(0, 0.2, 0), c + V(0, 0.26, 0), 0.04, seg=10)
        m.cyl(ALU, c + V(0, 0.2, -0.05), (s * 0.5, -0.9, 0.38), 0.012, seg=6)
        # braided feed line: main pipe with close-wound collars
        pts = [c + V(-s * 0.05, -0.2, -0.05), V(s * 0.36, -1.3, 0.22), V(s * 0.18, -1.12, 0.1), V(s * 0.08, -1.03, 0.03)]
        m.tube(SILVER, pts, 0.016, seg=8)
        for a, b in zip(pts, pts[1:]):
            for t in (0.25, 0.5, 0.75):
                p = V(a).lerp(V(b), t)
                m.torus(ALU, p, 0.017, 0.005, V(b) - V(a), 8, 4)
    he = V(-0.3, -1.1, -0.45)
    m.sph(GOLD, he, 0.16, 24, 12)                                                 # helium pressurant sphere
    m.torus(GOLDD, he, 0.161, 0.01, Y, 24, 6)
    m.cyl(SILVER, he + V(0, 0.15, 0), he + V(0, 0.2, 0), 0.028, seg=10)
    m.tube(SILVER, [he + V(0.05, 0.14, 0.05), V(-0.16, -0.95, -0.2), V(-0.08, -0.98, -0.08)], 0.011, seg=6)


# ------------------------------------------------------------------------------------------------ payloads

@model("Spacecraft", "pl_camera")
def pl_camera(m):
    c = V(0.3, 0.35, 0)
    fz = V(0, 0, -1)
    m.box(GOLD, c + V(0, 0, -R - 0.06), (0.5, 0.48, 0.12), bevel=0.03)                      # foil-wrapped electronics
    m.cyl(GOLD, c + V(0, 0, -R - 0.11), c + V(0, 0, -R - 0.17), 0.235, seg=32, bev=0.012)   # mounting collar
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        p = c + V(math.cos(a) * 0.21, math.sin(a) * 0.21, -R - 0.17)
        m.cyl(SILVER, p, p + V(0, 0, -0.012), 0.014, seg=8)
    # optical baffle barrel (hollow) with stray-light vanes
    prof = [(0.15, 0.0), (0.19, 0.0), (0.19, 0.3), (0.2, 0.31), (0.2, 0.34), (0.165, 0.34), (0.165, 0.08), (0.15, 0.08)]
    m.lathe(DARK, prof, c + V(0, 0, -R - 0.12), fz, 36, closed=True)
    for k in (0.16, 0.22, 0.28):
        m.lathe(DARK, [(0.13, k), (0.166, k), (0.166, k + 0.006), (0.13, k + 0.006)], c + V(0, 0, -R - 0.12), fz, 32, closed=True)
    m.torus(SILVER, c + V(0, 0, -R - 0.455), 0.183, 0.012, fz, 36, 6)
    # lens stack: coated front element in a gold retaining ring
    m.torus(GOLD, c + V(0, 0, -R - 0.21), 0.14, 0.014, fz, 32, 6)
    m.lathe("Mat_GlowBlue", [(0.127, 0.085), (0.127, 0.089)], c + V(0, 0, -R - 0.12), fz, 32)
    m.sph(GLASS, c + V(0, 0, -R - 0.2), 0.135, 32, 10, scale=(1, 0.22, 1), rot=align(fz))
    # rectangular sunshade hood
    m.box(DARK, c + V(0, 0.25, -R - 0.34), (0.48, 0.02, 0.44), bevel=0.006)
    for s in (-1, 1):
        m.plate(DARK, [c + V(s * 0.235, 0.25, -R - 0.12), c + V(s * 0.235, 0.25, -R - 0.56), c + V(s * 0.235, 0.05, -R - 0.12)], 0.014)
    m.cyl(ALU, c + V(0, 0.2, -R - 0.14), c + V(0, 0.25, -R - 0.2), 0.012, seg=6)


@model("Spacecraft", "pl_neutron")
def pl_neutron(m):
    c = V(-0.45, 1.0, -0.3)
    m.box(GOLD, c, (0.46, 0.16, 0.46), bevel=0.025)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl(SILVER, c + V(sx * 0.19, 0.08, sz * 0.19), c + V(sx * 0.19, 0.092, sz * 0.19), 0.018, seg=8)
    m.cyl(ALU, c + V(0, 0.08, 0), c + V(0, 0.12, 0), 0.17, seg=28, bev=0.008)
    dome = [(0.15, 0.12), (0.15, 0.3)] + [(0.15 * math.cos(math.radians(a)), 0.3 + 0.15 * math.sin(math.radians(a))) for a in (15, 30, 45, 60, 75)] + [(0.0, 0.45)]
    m.lathe(WHITE, dome, c, Y, 28)
    m.torus(GOLDD, c + V(0, 0.3, 0), 0.151, 0.008, Y, 28, 6)
    # twin helium-3 proportional counter tubes alongside the dome
    for s in (-1, 1):
        m.cyl(SILVER, c + V(-0.18, 0.13, s * 0.2), c + V(0.18, 0.13, s * 0.2), 0.03, seg=12, bev=0.008)
        m.cyl(DARK, c + V(0.18, 0.13, s * 0.2), c + V(0.21, 0.13, s * 0.2), 0.018, seg=8)
    m.tube(DARK, [c + V(0.2, 0.05, 0.1), c + V(0.3, 0.0, 0.1), c + V(0.3, -0.08, 0.1)], 0.012, seg=6)


def rover(m, g=None, dish=False, beacon=False):
    """Six-wheeled micro rover, upright, body centre at the origin, +Z forward. About 1.25 wide and 1.2 long."""
    kw = dict(g=g)
    m.box(WHITE, (0, 0, 0), (0.86, 0.26, 1.0), bevel=0.04, **kw)                 # warm electronics box
    m.box(GOLD, (0, -0.135, 0), (0.7, 0.014, 0.84), bevel=0.004, **kw)
    m.slab(DARK, (0, 0.135, 0), X, Z, 0.8, 0.94, 0.012, **kw)
    m.tiles(CELL, (0, 0.146, -0.06), X, Z, 0.76, 0.76, 4, 4, thick=0.012, gap=0.014, bevel=0.003, **kw)
    wheel = [(0.07, -0.05), (0.14, -0.05), (0.155, -0.03), (0.155, 0.03), (0.14, 0.05), (0.07, 0.05)]
    for s in (-1, 1):
        zs = (-0.45, 0.0, 0.45)
        for z in zs:
            c = V(s * 0.6, -0.17, z)
            m.lathe(DARK, wheel, c, X, 20, closed=True, **kw)
            m.lathe(ALU, [(0.0, -0.035), (0.03, -0.045), (0.075, -0.03), (0.075, 0.03), (0.03, 0.045), (0.0, 0.035)], c, X, 12, **kw)
            for k in range(12):                                                  # cleats
                a = math.radians(k * 30)
                p = c + V(0, math.cos(a) * 0.158, math.sin(a) * 0.158)
                m.add(box_mesh((0.1, 0.012, 0.02)), SILVER, T(p) @ euler(math.degrees(a), 0, 0), **kw)
            m.cyl(ALU, c + V(-s * 0.05, 0, 0), c + V(-s * 0.1, 0, 0), 0.02, seg=8, **kw)
        # rocker-bogie suspension
        piv = V(s * 0.47, -0.02, 0.12); bog = V(s * 0.5, -0.1, -0.22)
        m.sph(SILVER, piv, 0.04, 10, 5, **kw)
        m.cyl(ALU, piv, V(s * 0.5, -0.17, 0.45), 0.022, seg=8, **kw)            # rocker to the front wheel
        m.cyl(ALU, piv, bog, 0.022, seg=8, **kw)
        m.sph(SILVER, bog, 0.032, 10, 5, **kw)
        m.cyl(ALU, bog, V(s * 0.5, -0.17, 0.0), 0.018, seg=8, **kw)             # bogie to the middle and rear wheels
        m.cyl(ALU, bog, V(s * 0.5, -0.17, -0.45), 0.018, seg=8, **kw)
    m.cyl(ALU, (-0.47, -0.02, 0.12), (0.47, -0.02, 0.12), 0.014, seg=6, **kw)   # differential bar
    # mast with a stereo camera head
    m.cyl(ALU, (0.3, 0.13, 0.4), (0.3, 0.62, 0.4), 0.022, seg=10, **kw)
    m.cyl(SILVER, (0.3, 0.13, 0.4), (0.3, 0.17, 0.4), 0.04, seg=10, **kw)
    m.box(DARK, (0.3, 0.67, 0.4), (0.28, 0.11, 0.11), bevel=0.02, **kw)
    for s in (-1, 1):
        m.cyl(SILVER, (0.3 + s * 0.08, 0.67, 0.45), (0.3 + s * 0.08, 0.67, 0.475), 0.034, seg=12, **kw)
        m.sph(GLASS, (0.3 + s * 0.08, 0.67, 0.475), 0.027, 12, 6, scale=(1, 1, 0.4), **kw)
    m.cyl(SILVER, (-0.3, 0.13, -0.38), (-0.3, 0.5, -0.38), 0.008, seg=6, **kw)  # UHF whip
    if dish:
        m.cyl(ALU, (-0.2, 0.13, 0.1), (-0.2, 0.42, 0.1), 0.016, seg=8, **kw)
        m.dish(WHITE, (-0.2, 0.42, 0.1), (-0.15, 1, -0.2), 0.3, 0.08, t=0.012, seg=24, n=6, **kw)
        ax = V(-0.15, 1, -0.2).normalized()
        for k in range(8):                                                       # umbrella ribs
            a = math.radians(k * 45)
            side = ax.cross(V(0, 0, 1)).normalized(); up = ax.cross(side)
            rim = V(-0.2, 0.42, 0.1) + ax * 0.08 + (side * math.cos(a) + up * math.sin(a)) * 0.3
            m.cyl(GOLD, V(-0.2, 0.42, 0.1) + ax * 0.004, rim, 0.006, seg=5, **kw)
        m.cyl(DARK, V(-0.2, 0.42, 0.1), V(-0.2, 0.42, 0.1) + ax * 0.2, 0.008, seg=6, **kw)
        m.sph(GOLD, V(-0.2, 0.42, 0.1) + ax * 0.2, 0.025, 8, 4, **kw)
    if beacon:
        m.named("Beacon", (0, 0, 0))
        m.sph("Mat_GlowGreen", (0, 0.86, 0), 0.06, 12, 6, g=g, n="Beacon")


@model("Spacecraft", "pl_rover")
def pl_rover(m):
    # stowed flat against the aft face: the rover's deck points away from the bus
    m.M = T((0, -0.1, R + 0.34)) @ euler(90, 0, 0)
    rover(m)
    m.M = Matrix.Identity(4)
    for s in (-1, 1):                                    # hold-down brackets
        m.box(ALU, (s * 0.3, -0.1, R + 0.1), (0.1, 0.5, 0.2), bevel=0.015)
        m.cyl(SILVER, (s * 0.3, -0.1, R + 0.18), (s * 0.3, -0.1, R + 0.22), 0.03, seg=8)


@model("Spacecraft", "pl_mag_plasma")
def pl_mag(m):
    base = V(-0.62, 0.7, 0.62)
    d = V(-1, 0.55, 1).normalized()
    m.box(ALU, base + V(0.06, 0, -0.06), (0.2, 0.2, 0.2), rot=(0, 45, 0), bevel=0.02)       # deployment hinge bracket
    m.M = T(base) @ align(d)
    m.cyl(ALU, (0, 0, 0), (0, 0.24, 0), 0.08, seg=20, bev=0.012)
    m.cyl(SILVER, (-0.1, 0.1, 0), (0.1, 0.1, 0), 0.03, seg=10)
    m.cyl(CARBON, (0, 0.2, 0), (0, 2.36, 0), 0.03, 0.018, seg=16)                           # tapered carbon-fibre boom
    for y in (0.75, 1.5, 2.2):
        m.cyl(SILVER, (0, y - 0.03, 0), (0, y + 0.03, 0), 0.036, seg=14, bev=0.006)
    # Faraday cup plasma sensor mid-boom, looking sideways
    m.cyl(ALU, (0, 1.25, 0), (0.1, 1.25, 0), 0.016, seg=8)
    cup = [(0.0, 0.0), (0.075, 0.0), (0.08, 0.01), (0.08, 0.13), (0.066, 0.13), (0.066, 0.03), (0.0, 0.03)]
    m.lathe(GOLD, cup, (0.1, 1.25, 0), X, 24)
    m.lathe(DARK, [(0.064, 0.06), (0.064, 0.066)], (0.1, 1.25, 0), X, 20)
    m.torus(SILVER, (0.23, 1.25, 0), 0.073, 0.007, X, 24, 6)
    m.box(WHITE, (0, 1.72, 0), (0.14, 0.11, 0.14), bevel=0.02)                              # sensor electronics
    m.box(GOLD, (0, 1.72, 0.072), (0.1, 0.07, 0.008))
    # fluxgate magnetometer at the tip, in a light cage
    m.cyl(ALU, (0, 2.3, 0), (0, 2.38, 0), 0.04, seg=12, bev=0.006)
    m.named("FluxgateSensor")
    m.sph("Mat_GlowCyan", (0, 2.46, 0), 0.1, 24, 12)
    for ax in (X, Y, Z):
        m.torus(SILVER, (0, 2.46, 0), 0.112, 0.006, ax, 24, 5)
    m.M = Matrix.Identity(4)


@model("Spacecraft", "pl_lola_lidar")
def pl_lola(m):
    c = V(-0.36, -0.42, 0)
    fz = V(0, 0, -1)
    m.box(GOLD, c + V(0, 0, -R - 0.13), (0.62, 0.44, 0.24), bevel=0.03)                     # machined gold optical bench
    m.box(WHITE, c + V(0, 0.235, -R - 0.13), (0.56, 0.02, 0.2), bevel=0.006)                # radiator
    for sx in (-1, 1):
        for sy in (-1, 1):
            p = c + V(sx * 0.27, sy * 0.18, -R - 0.25)
            m.cyl(SILVER, p, p + V(0, 0, -0.012), 0.016, seg=8)
    for s in (-1, 1):                                                                        # twin beryllium receiver telescopes
        o = c + V(s * 0.15, 0.08, -R - 0.24)
        prof = [(0.07, 0.0), (0.1, 0.0), (0.1, 0.3), (0.106, 0.31), (0.106, 0.33), (0.088, 0.33), (0.088, 0.05), (0.07, 0.05)]
        m.lathe(BERY, prof, o, fz, 28, closed=True)
        m.sph(GLASS, o + fz * 0.07, 0.086, 24, 8, scale=(1, 0.25, 1), rot=align(fz))
        m.torus(SILVER, o + fz * 0.33, 0.097, 0.007, fz, 28, 6)
        for k in range(3):                                                                    # secondary mirror spider
            a = math.radians(k * 120 + 30)
            m.cyl(DARK, o + fz * 0.3, o + fz * 0.3 + V(math.cos(a), math.sin(a), 0) * 0.088, 0.004, seg=4)
        m.cyl(DARK, o + fz * 0.28, o + fz * 0.31, 0.022, seg=10)
    lo = c + V(0, -0.13, -R - 0.24)                                                           # laser transmitter (beam expander)
    m.lathe(DARK, [(0.04, 0.0), (0.058, 0.0), (0.062, 0.12), (0.048, 0.12), (0.04, 0.03)], lo, fz, 20, closed=True)
    m.named("LaserLens")
    m.lathe("Mat_GlowGreen", [(0.047, 0.095), (0.047, 0.103)], lo, fz, 20)
    for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):                                 # the five-beam pattern
        m.sph("Mat_GlowGreen", lo + fz * 0.108 + V(dx * 0.02, dy * 0.02, 0), 0.006, 6, 3)
    m.torus(SILVER, lo + fz * 0.12, 0.055, 0.005, fz, 20, 5)


@model("Spacecraft", "pl_ice_drill")
def pl_drill(m):
    dx = dz = -0.78
    ax = V(dx - 0.14, 0, dz - 0.14)                                                           # drill axis (x, z)
    m.box(ALU, (dx + 0.14, -0.1, dz + 0.14), (0.34, 0.12, 0.34), rot=(0, 45, 0), bevel=0.02)  # deck bracket
    m.box(ALU, (dx + 0.02, -0.1, dz + 0.02), (0.1, 0.08, 0.36), rot=(0, 45, 0), bevel=0.01)
    for s in (-1, 1):                                                                          # twin guide rails
        rx, rz = dx - 0.14 + s * 0.075, dz - 0.14 - s * 0.075
        m.box(ALU, (rx, -0.2, rz), (0.036, 1.9, 0.036), rot=(0, 45, 0), bevel=0.006)
        m.box(SILVER, (rx, -1.16, rz), (0.06, 0.03, 0.06), rot=(0, 45, 0), bevel=0.006)
    for y in (-1.1, -0.45, 0.2, 0.74):                                                         # rail cross ties
        m.cyl(ALU, (dx - 0.14 - 0.075, y, dz - 0.14 + 0.075), (dx - 0.14 + 0.075, y, dz - 0.14 - 0.075), 0.012, seg=6)
    m.cyl(SILVER, ax + V(0, -1.66, 0), ax + V(0, 0.7, 0), 0.02, seg=12)                        # drill string
    m.helix(BERY, ax + V(0, -1.64, 0), Y, 0.018, 0.075, 0.115, 11.5, 0.012, steps=16)          # helical auger flight
    m.lathe(DARK, [(0.0, -1.86), (0.03, -1.8), (0.062, -1.7), (0.062, -1.64), (0.0, -1.64)], (ax.x, 0, ax.z), Y, 12)   # carbide bit
    for k in range(4):
        a = math.radians(k * 90)
        m.plate(SILVER, [ax + V(0, -1.87, 0), ax + V(math.sin(a) * 0.07, -1.7, math.cos(a) * 0.07), ax + V(0, -1.66, 0)], 0.01)
    m.cyl(GOLD, ax + V(0, 0.69, 0), ax + V(0, 0.95, 0), 0.12, seg=24, bev=0.02)                # percussive motor head
    m.cyl(DARK, ax + V(0, 0.95, 0), ax + V(0, 0.99, 0), 0.07, seg=16, bev=0.008)
    m.torus(SILVER, ax + V(0, 0.82, 0), 0.122, 0.008, Y, 24, 6)
    m.box(ALU, ax + V(0, 0.64, 0), (0.24, 0.07, 0.12), rot=(0, 45, 0), bevel=0.012)            # carriage
    m.tube(DARK, [ax + V(0.05, 0.9, 0.05), V(dx + 0.05, 0.75, dz + 0.05), V(dx + 0.1, 0.56, dz + 0.1)], 0.012, seg=6)
    # MSolo mass spectrometer
    m.box(WHITE, (dx + 0.1, 0.42, dz + 0.1), (0.3, 0.26, 0.3), rot=(0, 45, 0), bevel=0.03)
    m.box(GOLD, (dx + 0.1, 0.555, dz + 0.1), (0.24, 0.012, 0.24), rot=(0, 45, 0), bevel=0.004)
    m.cyl(DARK, (dx + 0.02, 0.42, dz + 0.02), (dx - 0.07, 0.42, dz - 0.07), 0.04, seg=12, bev=0.006)   # sample inlet
    m.cyl(ALU, (dx + 0.14, 0.29, dz + 0.14), (dx + 0.14, -0.04, dz + 0.14), 0.02, seg=8)


# ------------------------------------------------------------------------------------------------ communications

@model("Spacecraft", "com_sband")
def com_sband(m):
    m.box(GOLD, (0.4, 0.915, 0.35), (0.48, 0.02, 0.48), bevel=0.006)
    m.box(WHITE, (0.4, 0.935, 0.35), (0.4, 0.024, 0.4), bevel=0.008)
    m.box(ALU, (0.4, 0.95, 0.35), (0.2, 0.008, 0.2), bevel=0.002)
    m.cyl(SILVER, (0.4, 0.95, 0.35), (0.4, 0.962, 0.35), 0.012, seg=8)
    m.cyl(ALU, (0.55, 0.9, -0.5), (0.55, 0.96, -0.5), 0.05, seg=14, bev=0.008)
    m.cyl(SILVER, (0.55, 0.95, -0.5), (0.55, 1.9, -0.5), 0.014, 0.008, seg=8)
    m.helix(GOLD, (0.55, 0.98, -0.5), Y, 0.014, 0.03, 0.035, 5, 0.006, steps=10)
    m.sph(SILVER, (0.55, 1.9, -0.5), 0.018, 8, 4)


@model("Spacecraft", "com_xband")
def com_xband(m):
    m.cyl(SILVER, (0, 0.9, 0.35), (0, 0.96, 0.35), 0.1, seg=20, bev=0.01)
    m.cyl(ALU, (0, 0.95, 0.35), (0, 1.62, 0.35), 0.04, seg=14)
    m.cyl(GOLD, (0, 1.5, 0.35), (0, 1.6, 0.35), 0.075, seg=18, bev=0.012)
    m.box(DARK, (0, 1.66, 0.35), (0.16, 0.12, 0.14), bevel=0.02)                 # two-axis gimbal
    m.cyl(SILVER, (-0.1, 1.66, 0.35), (0.1, 1.66, 0.35), 0.03, seg=10)
    ax = V(0, math.cos(math.radians(25)), -math.sin(math.radians(25)))
    o = V(0, 1.72, 0.35)
    m.dish(WHITE, o, ax, 0.85, 0.2, t=0.016, seg=40, n=8)
    side = V(1, 0, 0); up = ax.cross(side).normalized()
    for k in range(8):                                                            # back-up ribs
        a = math.radians(k * 45)
        rd = side * math.cos(a) + up * math.sin(a)
        m.cyl(ALU, o - ax * 0.02, o + rd * 0.8 + ax * (0.2 * 0.885 - 0.03), 0.012, seg=5)
    horn = o + ax * 0.62
    for k in range(3):                                                            # feed struts
        a = math.radians(90 + k * 120)
        rd = side * math.cos(a) + up * math.sin(a)
        m.cyl(DARK, o + rd * 0.78 + ax * 0.17, horn, 0.011, seg=6)
    m.lathe(GOLD, [(0.0, 0.0), (0.03, 0.0), (0.075, -0.12), (0.07, -0.12), (0.025, -0.01), (0.0, -0.01)], horn, ax, 16)
    m.sph(SILVER, horn, 0.035, 10, 5)
    m.cyl(ALU, o, o + ax * 0.08, 0.05, seg=12)
