# Export every child collection of "Export" to its .glb (path = the collection's custom property "export_path",
# relative to the project root, e.g. Assets/_Project/Art/Models/Interiors/Interior_V01.glb).
#
#  - In Blender (file open): Scripting tab → open this file → Run Script  → exports the CURRENT .blend only.
#  - Command line, all .blend files under ArtSource/Blender (or only names containing a filter word):
#      blender -b --factory-startup --python ArtSource/Blender/export_all.py -- [filter ...]
#
# Collection custom properties:
#   export_path  (required)  where the .glb goes, relative to the project root
#   zero_root    (default 1) the top-level objects are moved to the world origin while exporting
#                            (lets a part / interior sit inside its car in the .blend but export with origin = its own pivot)
import bpy, os, sys

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\GameDev\Projects\MyGame3D\ArtSource\Blender"
PROJ = os.path.abspath(os.path.join(HERE, "..", ".."))


def _objects(col):
    seen = []
    for o in col.all_objects:
        if o not in seen:
            seen.append(o)
    return seen


def export_collection(col):
    rel = col.get("export_path")
    if not rel:
        return None
    path = os.path.join(PROJ, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    objs = _objects(col)
    tops = [o for o in objs if o.parent not in objs]
    saved = {o: o.matrix_world.copy() for o in tops}
    if col.get("zero_root", 1):
        for o in tops:
            o.location = (0, 0, 0)
    bpy.context.view_layer.update()
    # everything must be visible + selectable to be exported with use_selection
    hidden = [(o, o.hide_get(), o.hide_select) for o in objs]
    bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode != 'OBJECT' else None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.hide_set(False)
        o.hide_select = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = tops[0] if tops else None
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_yup=True, export_apply=True,
                              export_texcoords=True, export_normals=True, export_materials='EXPORT',
                              export_cameras=False, export_lights=False, export_extras=False)
    for o, h, hs in hidden:
        o.hide_set(h)
        o.hide_select = hs
        o.select_set(False)
    for o, mw in saved.items():
        o.matrix_world = mw
    bpy.context.view_layer.update()
    return rel


def export_current():
    exp = bpy.data.collections.get("Export")
    done = []
    if exp is None:
        print("[export] no 'Export' collection in", bpy.data.filepath)
        return done
    for c in exp.children_recursive:
        r = export_collection(c)
        if r:
            done.append(r)
            print("[export]", os.path.basename(bpy.data.filepath), "→", r)
    return done


def export_all(filters=()):
    files = []
    for root, _, names in os.walk(HERE):
        for n in names:
            if n.endswith(".blend") and (not filters or any(f.lower() in n.lower() for f in filters)):
                files.append(os.path.join(root, n))
    # the parts library first (vehicle files link it)
    files.sort(key=lambda p: (0 if "Parts_Library" in p else 1, p))
    for f in files:
        bpy.ops.wm.open_mainfile(filepath=f)
        export_current()


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if bpy.data.filepath and "--all" not in args:
        export_current()  # a .blend is open (UI, or `blender -b file.blend --python export_all.py`)
    else:
        export_all([a for a in args if a != "--all"])
