# Batch H helpers: decimation to a triangle budget, UV2-safe decimation, side-by-side LOD previews.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix


def tris(o):
    if o.type != 'MESH':
        return 0
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def weld(o, dist=1e-5):
    """Merge the vertices that the glTF import split on UV / normal seams (UVs stay per loop)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bm.to_mesh(o.data)
    bm.free()


def _apply_decimate(o, ratio, **kw):
    m = o.modifiers.new("Dec", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = max(0.0005, min(1.0, ratio))
    m.use_collapse_triangulate = True
    for k, v in kw.items():
        setattr(m, k, v)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev)
    o.modifiers.remove(m)
    return me


def decimate_to(o, target, tol=0.06, **kw):
    """Collapse-decimate `o` in place until it has ~target triangles (binary search on the ratio)."""
    src = o.data
    n0 = tris(o)
    if n0 <= target:
        return n0
    lo, hi = 0.0, 1.0
    ratio = target / n0
    best = None
    for _ in range(12):
        me = _apply_decimate(o, ratio, **kw)
        n = sum(len(p.vertices) - 2 for p in me.polygons)
        if best is None or abs(n - target) < abs(best[1] - target):
            if best:
                bpy.data.meshes.remove(best[0])
            best = (me, n)
        else:
            bpy.data.meshes.remove(me)
        if abs(n - target) <= target * tol:
            break
        if n > target:
            hi = ratio
        else:
            lo = ratio
        ratio = (lo + hi) / 2
    me, n = best
    me.validate(clean_customdata=False)
    for i, mat in enumerate(src.materials):   # keep the slots (clearing them would reset the face indices)
        if i < len(me.materials):
            me.materials[i] = mat
        else:
            me.materials.append(mat)
    name = src.name
    o.data = me
    if src.users == 0:
        bpy.data.meshes.remove(src)
    me.name = name
    return n


def copy_obj(o, name, col=None):
    c = o.copy()
    c.data = o.data.copy()
    c.name = name
    c.data.name = "SM_" + name
    (col or o.users_collection[0]).objects.link(c)
    return c


def empty(name, parent, col, matrix_local=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.05
    col.objects.link(e)
    e.parent = parent
    if matrix_local is not None:
        e.matrix_parent_inverse = Matrix.Identity(4)
        e.matrix_basis = matrix_local
    return e


def preview_lods(groups, path, spacing, eye_dir=Vector((0.55, -1.0, 0.35)), height=None, lens=40):
    """groups = [[objects of LOD0], [LOD1], [LOD2]] (each group shown at x = i*spacing). Renders one PNG then restores."""
    sc = bpy.context.scene
    moved = []
    allo = [o for g in groups for o in g]
    for i, g in enumerate(groups):
        for o in g:
            if o.parent in allo:
                continue
            moved.append((o, o.matrix_world.copy()))
            o.matrix_world = Matrix.Translation(Vector((i * spacing, 0, 0))) @ o.matrix_world
    vis = {}
    for o in sc.objects:
        if o.type in ('MESH', 'CURVE') and o not in allo and not o.name.startswith("Preview"):
            vis[o] = o.hide_render
            o.hide_render = True
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in allo if o.type == 'MESH' for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi_ = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi_) / 2
    size = (hi_ - lo).length
    cam = sc.camera
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(hi_.x - lo.x, (hi_.z - lo.z) * 2.5) * 1.1
    d = eye_dir.normalized() * size * 2.0
    cam.location = c + d
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = 1500, 600
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o, m in moved:
        o.matrix_world = m
    for o, h in vis.items():
        o.hide_render = h
    bpy.context.view_layer.update()
