"""
PHASE X model generator library (run inside Blender, see README.md in this folder).

Every model is described in UNITY coordinates (metres, +Y up, left-handed) and converted to Blender space when the
mesh is created, so the numbers in the model scripts are the numbers Unity sees after import.

Geometry is collected per (group, material): one mesh object per material, so every Unity renderer has exactly one
material and the game can swap it freely (X-ray ghosting, status lights).
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

OUT_ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))) if "__file__" in globals() else "",
                        "Assets", "_PhaseX", "Resources", "Models")
SCENE_NAME = "PhaseX_Build"

# name: (r, g, b, metallic, roughness, emission strength)   -- preview only; Unity binds its own URP materials by name
MATS = {
    "Mat_GoldFoil": (0.86, 0.64, 0.20, 0.9, 0.25, 0), "Mat_GoldDark": (0.66, 0.46, 0.12, 0.9, 0.35, 0),
    "Mat_Alu": (0.74, 0.76, 0.80, 0.85, 0.35, 0), "Mat_Silver": (0.82, 0.84, 0.88, 1.0, 0.15, 0),
    "Mat_White": (0.90, 0.90, 0.92, 0.2, 0.4, 0), "Mat_Dark": (0.10, 0.10, 0.12, 0.6, 0.5, 0),
    "Mat_Carbon": (0.05, 0.05, 0.06, 0.3, 0.45, 0), "Mat_Bronze": (0.72, 0.45, 0.20, 0.85, 0.3, 0),
    "Mat_SolarCell": (0.05, 0.12, 0.42, 0.45, 0.1, 0), "Mat_EngineBell": (0.20, 0.20, 0.22, 0.8, 0.45, 0),
    "Mat_Beryllium": (0.62, 0.64, 0.66, 0.85, 0.3, 0), "Mat_Glass": (0.02, 0.03, 0.08, 0.3, 0.05, 0),
    "Mat_SteelRed": (0.35, 0.12, 0.08, 0.5, 0.6, 0), "Mat_FlagRed": (0.75, 0.10, 0.12, 0, 0.6, 0),
    "Mat_FlagBlue": (0.10, 0.15, 0.50, 0, 0.6, 0), "Mat_Metal": (0.16, 0.18, 0.22, 0.6, 0.45, 0),
    "Mat_Grey": (0.42, 0.44, 0.47, 0.6, 0.55, 0), "Mat_Screen": (0.02, 0.06, 0.10, 0, 0.2, 1),
    "Mat_GlowCyan": (0.2, 0.85, 1.0, 0, 0.5, 3), "Mat_GlowAmber": (1.0, 0.68, 0.18, 0, 0.5, 3),
    "Mat_GlowGreen": (0.2, 1.0, 0.45, 0, 0.5, 3), "Mat_GlowRed": (1.0, 0.25, 0.2, 0, 0.5, 3),
    "Mat_GlowBlue": (0.4, 0.6, 1.0, 0, 0.5, 3), "Mat_GlowOrange": (1.0, 0.5, 0.15, 0, 0.5, 3),
    "Mat_LedA": (0.3, 1.0, 0.5, 0, 0.5, 3), "Mat_LedB": (1.0, 0.68, 0.18, 0, 0.5, 3),
    "Mat_RtgGlow": (1.0, 0.42, 0.1, 0, 0.5, 3), "Mat_Beam": (0.3, 0.8, 1.0, 0, 0.5, 1),
    "Mat_Wall": (0.17, 0.20, 0.25, 0.1, 0.65, 0), "Mat_WallDark": (0.10, 0.115, 0.145, 0.2, 0.6, 0),
    "Mat_Ceiling": (0.07, 0.08, 0.10, 0.1, 0.7, 0), "Mat_Trim": (0.05, 0.055, 0.07, 0.6, 0.4, 0),
    "Mat_FxCore": (1.0, 0.95, 0.8, 0, 0.5, 6), "Mat_FxFlame": (1.0, 0.55, 0.15, 0, 0.5, 4),
    "Mat_FxOuter": (1.0, 0.35, 0.1, 0, 0.5, 2), "Mat_FxPuff": (0.9, 0.95, 1.0, 0, 0.5, 1),
}


def U(v):
    """Unity (x, y up, z) -> Blender (x, y, z up). A reflection, like Unity's own FBX import."""
    return Vector((-v[0], -v[2], v[1]))


def V(*a):
    return Vector(a[0]) if len(a) == 1 else Vector(a)


def euler(ex=0.0, ey=0.0, ez=0.0):
    """Unity Quaternion.Euler(x, y, z) as a 4x4 matrix (Z first, then X, then Y)."""
    r = math.radians
    return Matrix.Rotation(r(ey), 4, 'Y') @ Matrix.Rotation(r(ex), 4, 'X') @ Matrix.Rotation(r(ez), 4, 'Z')


def align(d, frm=(0, 1, 0)):
    """Rotation that turns +Y (or frm) onto direction d."""
    d = Vector(d)
    if d.length < 1e-9:
        return Matrix.Identity(4)
    return Vector(frm).rotation_difference(d.normalized()).to_matrix().to_4x4()


def T(p):
    return Matrix.Translation(Vector(p))


def S(x, y=None, z=None):
    if y is None: y, z = x, x
    return Matrix.Diagonal((x, y, z, 1.0))


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        r, g, b, met, rough, emis = MATS[name]
        m = bpy.data.materials.new(name)
        m.diffuse_color = (r, g, b, 1.0)
        m.metallic = met
        m.roughness = rough
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
            bsdf.inputs["Metallic"].default_value = met
            bsdf.inputs["Roughness"].default_value = rough
            if emis > 0 and "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (r, g, b, 1.0)
                bsdf.inputs["Emission Strength"].default_value = emis
    return m


# ---------------------------------------------------------------------------------------------- raw mesh generators

def lathe_mesh(prof, seg=24, closed=False):
    """Revolve a profile of (radius, y) points round +Y. Open profiles are capped on the axis."""
    prof = [(float(r), float(y)) for r, y in prof]
    if not closed:
        if prof[0][0] > 1e-6: prof.insert(0, (0.0, prof[0][1]))
        if prof[-1][0] > 1e-6: prof.append((0.0, prof[-1][1]))
    verts, faces, rings = [], [], []
    for r, y in prof:
        if r < 1e-6:
            rings.append([len(verts)]); verts.append((0.0, y, 0.0))
        else:
            idx = []
            for i in range(seg):
                a = 2 * math.pi * i / seg
                idx.append(len(verts)); verts.append((r * math.cos(a), y, r * math.sin(a)))
            rings.append(idx)
    pairs = list(zip(rings, rings[1:]))
    if closed: pairs.append((rings[-1], rings[0]))
    for A, B in pairs:
        if len(A) == 1 and len(B) == 1: continue
        for i in range(seg):
            j = (i + 1) % seg
            if len(A) == 1: faces.append((A[0], B[i], B[j]))
            elif len(B) == 1: faces.append((A[i], A[j], B[0]))
            else: faces.append((A[i], A[j], B[j], B[i]))
    return verts, faces


def box_mesh(size, bevel=0.0, segments=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bevel = min(bevel, min(size) * 0.45)
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=segments, profile=0.5, affect='EDGES')
    bm.verts.ensure_lookup_table()
    for i, v in enumerate(bm.verts): v.index = i
    verts = [tuple(v.co) for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return verts, faces


def prism_mesh(poly, y0, y1):
    """Extrude a polygon given as (x, z) points from y0 to y1."""
    n = len(poly)
    verts = [(x, y0, z) for x, z in poly] + [(x, y1, z) for x, z in poly]
    faces = [tuple(range(n)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return verts, faces


def bell_profile(length, r0, r1, t=0.012, n=9, power=0.7):
    """Closed (r, y) loop of a contoured nozzle bell hanging down from y=0 (throat) to y=-length (exit)."""
    outer = []
    for i in range(n + 1):
        s = i / n
        outer.append((r0 + (r1 - r0) * (2 * s - s * s) ** power, -length * s))
    inner = [(max(r - t, 0.002), y) for r, y in outer]
    lip = (outer[-1][0] + t * 0.6, outer[-1][1])
    return outer[:-1] + [lip, (lip[0], lip[1] - t * 0.5), (inner[-1][0], inner[-1][1] - t * 0.5)] + inner[::-1]


def ogive_pts(r, y0, length, n=10, blunt=0.0):
    """(r, y) points of a tangent-ogive nose from radius r at y0 up to the tip at y0 + length."""
    rho = (r * r + length * length) / (2 * r)
    pts = []
    for i in range(n + 1):
        y = length * i / n
        rr = math.sqrt(max(rho * rho - y * y, 0.0)) + r - rho
        pts.append((max(rr, 0.0), y0 + y))
    if blunt > 0:
        pts = [p for p in pts if p[0] > blunt] + [(blunt, pts[-1][1] - blunt * 0.4), (0.0, pts[-1][1] - blunt * 0.1)]
    return pts


# ---------------------------------------------------------------------------------------------- model

class Model:
    def __init__(self, sub, name):
        self.sub, self.name = sub, name
        self.groups = {}      # name -> (pivot, parent)
        self.pivots = {}      # named mesh object -> pivot
        self.buckets = {}     # (group, objname, mat) -> bmesh
        self.g = None         # current group
        self.M = Matrix.Identity(4)   # current local transform, applied before placement
        # ---- rig and animation (all optional; a model without clips is exported as a static mesh)
        self.bones = []       # (name, head, tail, parent) in Unity coordinates
        self.b = None         # current bone: geometry added while it is set is skinned to it
        self.pose_keys = []   # (frame, {bone: (unity euler, unity offset or None)})
        self.obj_keys = []    # (object or group name, frame, offset, unity euler, scale)
        self.clips = []       # (name, first frame, last frame, loop)
        self.fps = 30

    # ---- structure
    def group(self, name, pivot=(0, 0, 0), parent=None):
        self.groups[name] = (Vector(pivot), parent)
        return name

    # ---- rig and animation
    def bone(self, name, head, tail, parent=None):
        """A bone of the model's armature (Unity coordinates). Use `m.b = name` to skin the geometry that follows."""
        self.bones.append((name, Vector(head), Vector(tail), parent))
        return name

    def clip(self, name, first, last, loop=False):
        """Declare an animation clip as a frame range of the timeline. Unity splits the take by these ranges."""
        self.clips.append((name, int(first), int(last), bool(loop)))

    def pose(self, frame, bones):
        """Key bones at a frame: {bone: (ex, ey, ez)} or {bone: ((ex, ey, ez), (dx, dy, dz))}, Unity Euler degrees
        about the character's own axes and an optional offset in metres."""
        self.pose_keys.append((frame, bones))

    def key(self, name, frame, pos=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
        """Key a group or named object at a frame: offset from its pivot, Unity Euler rotation, scale (number or xyz)."""
        self.obj_keys.append((name, frame, Vector(pos), tuple(rot), scale))

    def named(self, name, pivot=(0, 0, 0)):
        self.pivots[name] = Vector(pivot)
        return name

    def _group_world(self, g):
        p = Vector((0, 0, 0))
        while g is not None:
            piv, parent = self.groups[g]
            p += piv
            g = parent
        return p

    def _bm(self, m, g, n):
        key = (g, n or m, m)
        if key not in self.buckets:
            self.buckets[key] = bmesh.new()
            if self.bones: self.buckets[key].verts.layers.deform.verify()    # before any vertex exists
        return self.buckets[key]

    def add(self, mesh, m, M=None, g=None, n=None, bone=None):
        verts, faces = mesh
        g = g if g is not None else self.g
        bm = self._bm(m, g, n)
        origin = self._group_world(g) + (self.pivots[n] if n in self.pivots else Vector((0, 0, 0)))
        if self.bones: origin = Vector((0, 0, 0))            # skinned meshes stay in armature space
        X = self.M @ (M if M is not None else Matrix.Identity(4))
        vs = [bm.verts.new(U((X @ Vector(v)) - origin)) for v in verts]
        bone = bone or self.b
        if self.bones and bone is not None:
            idx = [b[0] for b in self.bones].index(bone)
            dl = bm.verts.layers.deform.active
            for v in vs: v[dl][idx] = 1.0
        for f in faces:
            try: bm.faces.new([vs[i] for i in f])
            except ValueError: pass

    # ---- primitives (all in Unity coordinates)
    def box(self, m, c, size, rot=None, bevel=0.0, **kw):
        R = euler(*rot) if isinstance(rot, (tuple, list)) else (rot if rot is not None else Matrix.Identity(4))
        self.add(box_mesh(size, bevel), m, T(c) @ R, **kw)

    def lathe(self, m, prof, o=(0, 0, 0), axis=(0, 1, 0), seg=24, closed=False, scale=None, **kw):
        X = T(o) @ align(axis)
        if scale is not None: X = X @ S(*scale)
        self.add(lathe_mesh(prof, seg, closed), m, X, **kw)

    def cyl(self, m, a, b, r, r2=None, seg=20, bev=0.0, **kw):
        a, b = Vector(a), Vector(b)
        h = (b - a).length
        r2 = r if r2 is None else r2
        if bev > 0:
            bev = min(bev, h * 0.4, max(min(r, r2) * 0.8, 1e-4))
            prof = [(max(r - bev, 0), 0), (r, bev), (r2, h - bev), (max(r2 - bev, 0), h)]
        else:
            prof = [(r, 0), (r2, h)]
        self.lathe(m, prof, a, b - a, seg, **kw)

    def ycyl(self, m, c, r, h, r2=None, seg=24, bev=0.0, **kw):
        """Upright cylinder centred on c."""
        c = Vector(c)
        self.cyl(m, c - Vector((0, h / 2, 0)), c + Vector((0, h / 2, 0)), r, r2, seg, bev, **kw)

    def sph(self, m, c, r, seg=20, rings=10, scale=(1, 1, 1), rot=None, **kw):
        prof = [(r * math.sin(math.pi * i / rings), -r * math.cos(math.pi * i / rings)) for i in range(rings + 1)]
        prof[0] = (0.0, -r); prof[-1] = (0.0, r)
        R = euler(*rot) if isinstance(rot, (tuple, list)) else (rot if rot is not None else Matrix.Identity(4))
        self.add(lathe_mesh(prof, seg), m, T(c) @ R @ S(*scale), **kw)

    def torus(self, m, c, R, r, axis=(0, 1, 0), seg=28, tseg=8, **kw):
        prof = [(R + r * math.cos(2 * math.pi * i / tseg), r * math.sin(2 * math.pi * i / tseg)) for i in range(tseg)]
        self.lathe(m, prof, c, axis, seg, closed=True, **kw)

    def tube(self, m, pts, r, seg=8, **kw):
        pts = [Vector(p) for p in pts]
        for a, b in zip(pts, pts[1:]):
            self.cyl(m, a, b, r, seg=seg, **kw)
        for p in pts[1:-1]:
            self.sph(m, p, r, seg=seg, rings=4, **kw)

    def bell(self, m, o, axis, length, r0, r1, t=0.012, seg=24, n=9, power=0.7, glow=None, glow_r=0.8, **kw):
        """Nozzle bell: throat at o, opening along axis. Optional glowing disk just inside the exit."""
        X = T(o) @ align(axis, (0, -1, 0))
        self.add(lathe_mesh(bell_profile(length, r0, r1, t, n, power), seg, closed=True), m, X, **kw)
        if glow:
            self.add(lathe_mesh([(r1 * glow_r, -length * 0.93), (r1 * glow_r, -length * 0.96)], seg), glow, X, **kw)

    def dish(self, m, o, axis, R, depth, t=0.02, seg=32, n=8, **kw):
        """Parabolic reflector opening along axis, vertex at o."""
        front = [(R * i / n, depth * (i / n) ** 2) for i in range(n + 1)]
        back = [(r, y - t) for r, y in front]
        prof = front + [(R + t * 0.5, depth), (R + t * 0.5, depth - t)] + back[::-1][1:]
        self.lathe(m, prof, o, axis, seg, **kw)

    def prism(self, m, poly, y0, y1, M=None, **kw):
        self.add(prism_mesh(poly, y0, y1), m, M, **kw)

    def plate(self, m, pts, thick, **kw):
        """A flat plate through 3D points (convex outline), given thickness along its normal."""
        pts = [Vector(p) for p in pts]
        nrm = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized() * (thick / 2)
        n = len(pts)
        verts = [tuple(p - nrm) for p in pts] + [tuple(p + nrm) for p in pts]
        faces = [tuple(range(n)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        self.add((verts, faces), m, **kw)

    def helix(self, m, o, axis, r_in, r_out, pitch, turns, thick, steps=14, **kw):
        """A screw flight (auger) winding round an axis."""
        verts, faces = [], []
        n = int(turns * steps)
        for i in range(n + 1):
            a = 2 * math.pi * i / steps
            y = pitch * i / steps
            c, s = math.cos(a), math.sin(a)
            verts += [(r_in * c, y, r_in * s), (r_out * c, y, r_out * s), (r_out * c, y + thick, r_out * s), (r_in * c, y + thick * 2.2, r_in * s)]
        for i in range(n):
            a, b = i * 4, (i + 1) * 4
            for k in range(4):
                faces.append((a + k, a + (k + 1) % 4, b + (k + 1) % 4, b + k))
        faces.append((0, 1, 2, 3)); faces.append((n * 4, n * 4 + 1, n * 4 + 2, n * 4 + 3))
        self.add((verts, faces), m, T(o) @ align(axis), **kw)

    def arc(self, m, y0, y1, r_in, r_out, a0, a1, steps=6, o=(0, 0, 0), **kw):
        """Curved plate round the Y axis from angle a0 to a1 (degrees, 0 = +Z, 90 = +X)."""
        verts, faces = [], []
        for i in range(steps + 1):
            a = math.radians(a0 + (a1 - a0) * i / steps)
            s, c = math.sin(a), math.cos(a)
            verts += [(r_in * s, y0, r_in * c), (r_out * s, y0, r_out * c), (r_out * s, y1, r_out * c), (r_in * s, y1, r_in * c)]
        for i in range(steps):
            a, b = i * 4, (i + 1) * 4
            for k in range(4): faces.append((a + k, a + (k + 1) % 4, b + (k + 1) % 4, b + k))
        faces.append((0, 1, 2, 3)); faces.append((steps * 4, steps * 4 + 1, steps * 4 + 2, steps * 4 + 3))
        self.add((verts, faces), m, T(o), **kw)

    def tiles(self, m, c, u, v, w, h, nu, nv, thick=0.02, gap=0.012, bevel=0.006, **kw):
        """A nu x nv grid of bevelled tiles on the plane through c spanned by unit vectors u and v."""
        c, u, v = Vector(c), Vector(u).normalized(), Vector(v).normalized()
        n = u.cross(v).normalized()
        R = Matrix((u, n, v)).transposed().to_4x4()
        tw, th = w / nu, h / nv
        mesh = box_mesh((tw - gap, thick, th - gap), bevel)
        for i in range(nu):
            for j in range(nv):
                p = c + u * ((i + 0.5) * tw - w / 2) + v * ((j + 0.5) * th - h / 2)
                self.add(mesh, m, T(p) @ R, **kw)

    def slab(self, m, c, u, v, w, h, thick, bevel=0.0, **kw):
        """One box on the plane through c spanned by u and v (w along u, h along v)."""
        c, u, v = Vector(c), Vector(u).normalized(), Vector(v).normalized()
        n = u.cross(v).normalized()
        R = Matrix((u, n, v)).transposed().to_4x4()
        self.add(box_mesh((w, thick, h), bevel), m, T(c) @ R, **kw)

    def truss(self, m, a, b, w, r, bays=4, **kw):
        """Square lattice truss from a to b: four longerons and zig-zag bracing."""
        a, b = Vector(a), Vector(b)
        ax = (b - a).normalized()
        side = ax.cross(Vector((0, 1, 0)))
        if side.length < 1e-3: side = ax.cross(Vector((1, 0, 0)))
        side.normalize(); up = ax.cross(side).normalized()
        cs = [(side * sx + up * sy) * (w / 2) for sx, sy in ((1, 1), (1, -1), (-1, -1), (-1, 1))]
        for c in cs: self.cyl(m, a + c, b + c, r, seg=6, **kw)
        for i in range(bays + 1):
            p = a + (b - a) * (i / bays)
            for k in range(4): self.cyl(m, p + cs[k], p + cs[(k + 1) % 4], r * 0.8, seg=6, **kw)
            if i < bays:
                q = a + (b - a) * ((i + 1) / bays)
                for k in range(4):
                    self.cyl(m, p + cs[k], q + cs[(k + 1) % 4], r * 0.7, seg=6, **kw)

    # ---- output
    def _finish(self, bm):
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        thr = math.radians(38)
        for f in bm.faces: f.smooth = True
        for e in bm.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > thr: e.smooth = False
        if bm.loops.layers.uv.get("UVMap") is not None: return     # the model script wrote its own UVs
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                c = l.vert.co
                l[uv].uv = (c[(ax + 1) % 3], c[(ax + 2) % 3])

    def build(self):
        scene = _work_scene()
        coll = bpy.data.collections.new(self.name)
        scene.collection.children.link(coll)
        objs, empties = [], {}
        for g, (piv, parent) in self.groups.items():
            e = bpy.data.objects.new(g, None)
            e.empty_display_size = 0.1
            coll.objects.link(e); empties[g] = e; objs.append(e)
        for g, (piv, parent) in self.groups.items():
            empties[g].location = U(piv)
            if parent is not None: empties[g].parent = empties[parent]
        tris = 0
        for (g, oname, m), bm in self.buckets.items():
            self._finish(bm)
            name = oname if oname != m else (g or self.name) + "_" + m[4:]
            me = bpy.data.meshes.new(name)
            bm.to_mesh(me);
            tris += sum(len(f.verts) - 2 for f in bm.faces)
            bm.free()
            me.materials.append(mat(m))
            o = bpy.data.objects.new(name, me)
            coll.objects.link(o); objs.append(o)
            if oname in self.pivots and not self.bones: o.location = U(self.pivots[oname])
            if g is not None: o.parent = empties[g]
        self.buckets = {}
        self.objs, self.coll, self.tris = objs, coll, tris
        self.arm = None
        if self.bones: self._build_armature(scene, coll)
        if self.clips: self._build_animation(scene, empties)
        return objs

    def _build_armature(self, scene, coll):
        """Create the armature, then bind every mesh to it (each vertex fully to the bone it was added under)."""
        data = bpy.data.armatures.new(self.name + "_Rig")
        arm = bpy.data.objects.new("Armature", data)
        coll.objects.link(arm)
        bpy.context.view_layer.update()
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT')
        made = {}
        for name, head, tail, parent in self.bones:
            eb = data.edit_bones.new(name)
            eb.head, eb.tail, eb.roll = U(head), U(tail), 0.0
            if parent is not None: eb.parent = made[parent]
            made[name] = eb
        bpy.ops.object.mode_set(mode='OBJECT')
        for o in self.objs:
            if o.type != 'MESH': continue
            for name, _, _, _ in self.bones: o.vertex_groups.new(name=name)
            mod = o.modifiers.new("Armature", 'ARMATURE')
            mod.object = arm
            o.parent = arm
        self.objs.insert(0, arm)
        self.arm = arm

    def _build_animation(self, scene, empties):
        scene.render.fps = self.fps
        scene.frame_start = min(c[1] for c in self.clips)
        scene.frame_end = max(c[2] for c in self.clips)
        try: bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
        except Exception: pass
        C = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))            # Unity -> Blender, as a matrix (its own inverse transpose)
        Ci = C.inverted()
        def rot_b(e): return C @ euler(*e).to_3x3() @ Ci           # Unity Euler -> Blender rotation matrix
        if self.arm is not None:
            last = {}
            for frame, bones in sorted(self.pose_keys, key=lambda k: k[0]):
                for name, val in bones.items():
                    e, off = (val if isinstance(val[0], (tuple, list)) else (val, None))
                    pb = self.arm.pose.bones[name]
                    rest = self.arm.data.bones[name].matrix_local.to_3x3()
                    q = (rest.inverted() @ rot_b(e) @ rest).to_quaternion()
                    if name in last and last[name].dot(q) < 0: q.negate()
                    last[name] = q.copy()
                    pb.rotation_mode = 'QUATERNION'
                    pb.rotation_quaternion = q
                    pb.keyframe_insert("rotation_quaternion", frame=frame)
                    if off is not None:
                        pb.location = rest.inverted() @ U(off)
                        pb.keyframe_insert("location", frame=frame)
        targets = dict(empties)
        for o in self.objs:
            if o.type == 'MESH': targets.setdefault(o.name, o)
        base = {}
        for name, frame, off, e, sc in sorted(self.obj_keys, key=lambda k: k[1]):
            o = targets[name]
            if name not in base: base[name] = o.location.copy()
            o.rotation_mode = 'XYZ'
            o.location = base[name] + U(off)
            o.rotation_euler = rot_b(e).to_euler('XYZ', o.rotation_euler)
            sx, sy, sz = (sc, sc, sc) if isinstance(sc, (int, float)) else sc
            o.scale = (sx, sz, sy)
            for ch in ("location", "rotation_euler", "scale"): o.keyframe_insert(ch, frame=frame)
        scene.frame_set(scene.frame_start)

    def export(self, keep=False):
        objs = self.build()
        path = os.path.join(OUT_ROOT, self.sub, self.name + ".fbx")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.context.view_layer.update()
        for o in bpy.context.view_layer.objects: o.select_set(False)
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        sidecar = os.path.splitext(path)[0] + ".clips.json"
        if self.clips:
            # Animated model: one take over the whole timeline. "Apply Transform" is known to break armatures and
            # animation, so it is off here; Unity gets the same result through the root objects' rotation.
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=True, global_scale=1.0, apply_unit_scale=True,
                apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                object_types={'EMPTY', 'MESH', 'ARMATURE'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                add_leaf_bones=False, use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_bones=True,
                bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
            import json
            with open(sidecar, "w") as f:      # read by PhaseXModelImportSettings to split the take into clips
                json.dump({"fps": self.fps, "clips": [{"name": n, "first": a, "last": b, "loop": l} for n, a, b, l in self.clips]}, f, indent=1)
        else:
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=True, global_scale=1.0, apply_unit_scale=True,
                apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                object_types={'EMPTY', 'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE', bake_anim=False)
            if os.path.exists(sidecar): os.remove(sidecar)
        info = {"file": self.sub + "/" + self.name + ".fbx", "tris": self.tris, "objects": len(objs), "clips": [c[0] for c in self.clips]}
        if not keep: self.discard()
        return info

    def discard(self):
        for o in self.objs:
            me = o.data
            act = o.animation_data.action if o.animation_data else None
            bpy.data.objects.remove(o)
            if act is not None and act.users == 0: bpy.data.actions.remove(act)
            if me is not None and me.users == 0:
                (bpy.data.armatures if isinstance(me, bpy.types.Armature) else bpy.data.meshes).remove(me)
        bpy.data.collections.remove(self.coll)

    def preview(self, path, view=(0.55, 0.4, -1.0), res=640, frame=None):
        """Workbench render of the built model (call build() or export(keep=True) first)."""
        scene = _work_scene()
        if frame is not None: scene.frame_set(frame)
        lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
        bpy.context.view_layer.update()
        for o in self.objs:
            if o.type != 'MESH': continue
            for v in o.data.vertices:
                w = o.matrix_world @ v.co
                for i in range(3): lo[i] = min(lo[i], w[i]); hi[i] = max(hi[i], w[i])
        c = (lo + hi) / 2
        rad = (hi - lo).length / 2
        cam_data = bpy.data.cameras.new("pxcam"); cam_data.lens = 70
        cam = bpy.data.objects.new("pxcam", cam_data)
        scene.collection.objects.link(cam)
        d = U(view).normalized()
        cam.location = c + d * rad * 4.4
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        cam_data.clip_start = 0.01; cam_data.clip_end = 1000
        scene.camera = cam
        r = scene.render
        r.engine = 'BLENDER_WORKBENCH'
        scene.display.shading.light = 'STUDIO'
        scene.display.shading.color_type = 'MATERIAL'
        scene.display.shading.show_cavity = True
        scene.display.shading.show_object_outline = False
        scene.display.shading.show_specular_highlight = True
        r.resolution_x = r.resolution_y = res
        r.film_transparent = False
        r.image_settings.file_format = 'PNG'
        r.filepath = path
        if scene.world is None:
            scene.world = bpy.data.worlds.new("pxworld")
        scene.world.color = (0.03, 0.035, 0.05)
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(cam); bpy.data.cameras.remove(cam_data)


def _work_scene():
    """The scene the generator works in: its own scene in an interactive Blender, the startup scene with -b."""
    if bpy.context.window is None: return bpy.context.scene
    scene = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    if bpy.context.window.scene != scene: bpy.context.window.scene = scene
    return scene


REGISTRY = {}   # "Sub/name" -> builder function returning a Model


def model(sub, name):
    def deco(fn):
        REGISTRY[sub + "/" + name] = (sub, name, fn)
        return fn
    return deco


def run(only=None, preview_dir=None, view=(0.55, 0.4, -1.0), frames=None):
    """Build and export every registered model (or those whose key contains one of `only`)."""
    out = []
    for key, (sub, name, fn) in REGISTRY.items():
        if only and not any(o in key for o in only): continue
        m = Model(sub, name)
        fn(m)
        info = m.export(keep=preview_dir is not None)
        if preview_dir is not None:
            os.makedirs(preview_dir, exist_ok=True)
            if frames:
                for fr in frames: m.preview(os.path.join(preview_dir, "%s_f%03d.png" % (name, fr)), view, frame=fr)
            else:
                m.preview(os.path.join(preview_dir, name + ".png"), view)
            m.discard()
        out.append(info)
    return out
