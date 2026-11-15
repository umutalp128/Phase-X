# Animated flight models: the fairing / upper-stage separation rig and the effect meshes (engine plume, impact
# flash). Each has one take; m.clip() names the frame ranges Unity turns into clips. Unity coordinates (+Y up).
I4 = Matrix.Identity(4)


def half_shell(prof, a0, a1, seg, t):
    """Half of a body of revolution with wall thickness t: profile of (r, y), from angle a0 to a1 (0 = +Z, 90 = +X)."""
    verts, faces = [], []
    n = len(prof)
    for i in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        sn, cs = math.sin(a), math.cos(a)
        for r, y in prof:
            verts.append((r * sn, y, r * cs))
            ri = max(r - t, 0.0005)
            verts.append((ri * sn, y, ri * cs))
    def idx(i, j, inner): return (i * n + j) * 2 + (1 if inner else 0)
    for i in range(seg):
        for j in range(n - 1):
            faces.append((idx(i, j, 0), idx(i + 1, j, 0), idx(i + 1, j + 1, 0), idx(i, j + 1, 0)))
            faces.append((idx(i, j, 1), idx(i, j + 1, 1), idx(i + 1, j + 1, 1), idx(i + 1, j, 1)))
        faces.append((idx(i, 0, 0), idx(i, 0, 1), idx(i + 1, 0, 1), idx(i + 1, 0, 0)))                 # bottom rim
        faces.append((idx(i, n - 1, 0), idx(i + 1, n - 1, 0), idx(i + 1, n - 1, 1), idx(i, n - 1, 1)))  # tip
    for i in (0, seg):
        for j in range(n - 1):
            faces.append((idx(i, j, 0), idx(i, j + 1, 0), idx(i, j + 1, 1), idx(i, j, 1)))             # cut edges
    return verts, faces


@model("Simulation", "sim_separation")
def sim_separation(m):
    """
    Payload fairing and upper stage, in the rockets' units (fairing radius 0.1). Origin: centre of the fairing base,
    +Y towards the nose. Clip "Separate": the fairing halves hinge open and tumble away, cold-gas puffs fire at the
    separation plane, the spent upper stage pulls back and tumbles; everything shrinks away at the end.
    """
    W, D = "Mat_White", "Mat_Dark"
    r, t = 0.1, 0.006
    prof = [(r * 0.86, 0.0), (r, 0.03), (r, 0.2)] + ogive_pts(r, 0.2, 0.2, 8, blunt=0.012)[1:-1]
    for name, a0, sx in (("FairingHalf_A", 0, 1), ("FairingHalf_B", 180, -1)):
        g = m.group(name, (sx * r, 0, 0))
        m.add(half_shell(prof, a0 + 1.0, a0 + 179.0, 14, t), W, g=g)
        m.add(half_shell([(r + 0.0015, 0.05), (r + 0.0015, 0.075)], a0 + 1.0, a0 + 179.0, 14, 0.003), D, g=g)      # livery band
        for y in (0.1, 0.16, 0.24):                                                                    # stiffening ribs inside
            m.add(half_shell([(r - t, y), (r - t, y + 0.008)], a0 + 2.0, a0 + 178.0, 12, 0.006), "Mat_Alu", g=g)
        m.add(half_shell([(r - t + 0.0005, 0.03), (r - t + 0.0005, 0.2)], a0 + 3.0, a0 + 177.0, 12, 0.002), "Mat_GoldDark", g=g)   # acoustic blanket
    g = m.group("UpperStage", (0, -0.16, 0))
    m.lathe("Mat_Silver", [(0.07, -0.03), (r * 0.86, 0.0), (r * 0.8, 0.012), (0.06, 0.012)], seg=28, closed=True, g=g)   # payload adapter
    m.cyl(W, (0, -0.3, 0), (0, -0.03, 0), 0.075, seg=28, bev=0.006, g=g)
    for y in (-0.06, -0.27):
        m.torus(D, (0, y, 0), 0.0755, 0.003, (0, 1, 0), 28, 5, g=g)
    for k in range(4):                                                                                 # pressurant bottles
        a = math.radians(45 + k * 90)
        m.sph("Mat_GoldFoil", (math.sin(a) * 0.05, -0.315, math.cos(a) * 0.05), 0.02, 10, 5, g=g)
    m.cyl("Mat_Alu", (0, -0.3, 0), (0, -0.33, 0), 0.03, seg=14, g=g)
    m.bell("Mat_EngineBell", (0, -0.325, 0), (0, -1, 0), 0.09, 0.012, 0.05, t=0.003, seg=22, n=7, g=g)
    for k in range(4):                                                                                 # cold-gas puffs at the separation plane
        a = math.radians(k * 90 + 45)
        d = V(math.sin(a), 0, math.cos(a))
        nm = m.named("Puff_%d" % k, d * 0.11 + V(0, 0.01, 0))
        m.sph("Mat_FxPuff", d * 0.11 + V(0, 0.01, 0), 0.035, 10, 5, scale=(1.0, 0.7, 1.0), n=nm)
        for f, sc, out in ((0, 0.0, 0.0), (8, 0.0, 0.0), (11, 1.0, 0.02), (17, 1.9, 0.09), (27, 0.0, 0.2), (75, 0.0, 0.2)):
            m.key(nm, f, pos=d * out, scale=sc)
    for name, sx in (("FairingHalf_A", 1), ("FairingHalf_B", -1)):
        m.key(name, 0)
        m.key(name, 8)
        m.key(name, 20, pos=(sx * 0.04, 0.0, 0), rot=(0, 0, -sx * 32))
        m.key(name, 40, pos=(sx * 0.3, -0.08, sx * 0.04), rot=(sx * 12, sx * 10, -sx * 78))
        m.key(name, 58, pos=(sx * 0.62, -0.26, sx * 0.1), rot=(sx * 28, sx * 26, -sx * 118))
        m.key(name, 75, pos=(sx * 0.95, -0.5, sx * 0.17), rot=(sx * 44, sx * 40, -sx * 150), scale=0.0)
    m.key("UpperStage", 0)
    m.key("UpperStage", 10)
    m.key("UpperStage", 22, pos=(0, -0.07, 0))
    m.key("UpperStage", 55, pos=(0.03, -0.62, 0.05), rot=(22, 0, 14))
    m.key("UpperStage", 75, pos=(0.06, -1.0, 0.1), rot=(40, 0, 26), scale=0.0)
    m.clip("Separate", 0, 75)


@model("Simulation", "fx_exhaust_plume")
def fx_exhaust_plume(m):
    """
    Rocket engine plume: nozzle at the origin, 1 long towards -Y. Three nested shells (translucent outer flame,
    hotter mid flame, white-hot core) and a row of shock diamonds. Clip "Flicker" (loop): each layer pulses at its
    own rate, so the plume never repeats visibly.
    """
    def drop(r0, r1, y1, y2, n=10):
        pts = [(r0, 0.0)]
        for i in range(1, n + 1):
            s = i / n
            if s < 0.3: pts.append((r0 + (r1 - r0) * math.sin(s / 0.3 * math.pi / 2), y1 * s / 0.3))
            else:
                u = (s - 0.3) / 0.7
                pts.append((r1 * (1 - u) ** 0.75 * (1 + 0.07 * math.sin(u * 14)), y1 + (y2 - y1) * u))
        pts[-1] = (0.0, y2)
        return pts
    layers = (("PlumeOuter", "Mat_FxOuter", drop(0.07, 0.16, -0.3, -1.0), 1, 0.0, 0.07, 0.13),
              ("PlumeMid", "Mat_FxFlame", drop(0.05, 0.105, -0.24, -0.78), 2, 1.3, 0.05, 0.1),
              ("PlumeCore", "Mat_FxCore", drop(0.03, 0.055, -0.15, -0.5), 3, 2.1, 0.04, 0.12))
    for name, mt, prof, k, ph, ar, ay in layers:
        m.named(name, (0, 0, 0))
        m.lathe(mt, prof, seg=20, n=name)
        for f in range(0, 25, 2):
            w = 2 * math.pi * f / 24
            m.key(name, f, scale=(1 + ar * math.sin(k * w + ph), 1 + ay * math.sin(k * w + ph + 1.1), 1 + ar * math.sin(k * w + ph + 0.4)))
    m.named("ShockDiamonds", (0, 0, 0))
    for y, r in ((-0.2, 0.04), (-0.36, 0.032), (-0.5, 0.024), (-0.62, 0.016)):
        m.lathe("Mat_FxCore", [(0.0, y + r * 1.6), (r, y), (0.0, y - r * 1.6)], seg=12, n="ShockDiamonds")
    for f in range(0, 25, 2):
        w = 2 * math.pi * f / 24
        m.key("ShockDiamonds", f, pos=(0, 0.012 * math.sin(2 * w), 0), scale=(1 + 0.12 * math.sin(3 * w), 1 + 0.06 * math.sin(2 * w + 1), 1 + 0.12 * math.sin(3 * w)))
    m.clip("Flicker", 0, 24, loop=True)


@model("Simulation", "fx_impact_flash")
def fx_impact_flash(m):
    """
    Crisis impact flash, about 1 across at its peak. Clip "Burst" (0.9 s): a white-hot core swells and collapses,
    spikes shoot out and turn, a shock ring races outward and flattens away, sparks fly.
    """
    m.named("FlashCore", (0, 0, 0))
    m.sph("Mat_FxCore", (0, 0, 0), 0.2, 20, 10, n="FlashCore")
    m.named("FlashSpikes", (0, 0, 0))
    dirs = []
    for i in range(14):                                           # evenly spread directions (Fibonacci sphere)
        y = 1 - 2 * (i + 0.5) / 14
        rr = math.sqrt(1 - y * y); a = i * 2.39996
        dirs.append(V(math.cos(a) * rr, y, math.sin(a) * rr))
    for i, d in enumerate(dirs):
        ln = 0.5 if i % 2 == 0 else 0.34
        m.lathe("Mat_FxFlame", [(0.0, 0.0), (0.035, 0.08), (0.0, ln)], (0, 0, 0), d, 8, n="FlashSpikes")
    m.named("FlashRing", (0, 0, 0))
    m.torus("Mat_FxOuter", (0, 0, 0), 0.2, 0.018, (0, 1, 0), 36, 6, n="FlashRing")
    m.named("FlashSparks", (0, 0, 0))
    for i, d in enumerate(dirs[::2]):
        m.sph("Mat_FxCore", d * 0.3, 0.018, 6, 3, n="FlashSparks")
    for f, sc in ((0, 0.0), (3, 1.35), (8, 1.0), (16, 0.6), (27, 0.0)): m.key("FlashCore", f, scale=sc)
    for f, sc, ry in ((0, 0.0, 0), (4, 1.25, 8), (10, 0.9, 22), (18, 0.45, 34), (24, 0.0, 40), (27, 0.0, 40)): m.key("FlashSpikes", f, rot=(0, ry, 0), scale=sc)
    for f, sc, sy in ((0, 0.2, 1.0), (10, 1.7, 0.8), (20, 2.6, 0.4), (27, 3.1, 0.0)): m.key("FlashRing", f, rot=(25, 0, 10), scale=(sc, sy, sc))
    for f, sc in ((0, 0.0), (2, 0.5), (14, 1.7), (22, 2.3), (27, 0.0)): m.key("FlashSparks", f, rot=(0, -f * 2.0, 0), scale=sc)
    m.clip("Burst", 0, 27)
