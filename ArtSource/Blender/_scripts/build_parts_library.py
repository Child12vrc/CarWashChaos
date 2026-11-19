# (Re)generates ArtSource/Blender/Parts/Parts_Library.blend from parts_lib.py and exports every part to
# Assets/_Project/Art/Models/Parts/<Category>/<Name>.glb.
#   blender -b --factory-startup --python ArtSource/Blender/_scripts/build_parts_library.py
# Hand edits: open Parts_Library.blend, edit the SM_<Name> mesh, run export_all.py (vehicle files link these meshes,
# so they update on reopen). Re-running THIS script overwrites hand edits.
import bpy, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import parts_lib as L
import export_all

bpy.ops.wm.read_factory_settings(use_empty=True)
L.make_mats()
cats = {"Interior": L.INTERIOR_LIBRARY}
exp = L.collection("Export")
L.collection("Exterior")  # filled by the exterior pass
rows = []
for cat, lib in cats.items():
    ccol = L.collection(cat)
    x = 0.0
    for name, fn in lib.items():
        g = fn()
        o = g.obj(col=ccol)
        o.name = name
        o.data.name = "SM_" + name
        e_col = L.collection("EXP_" + name, exp, export_path=f"Assets/_Project/Art/Models/Parts/{cat}/{name}.glb", zero_root=1)
        e_col.objects.link(o)
        if name == "SteeringWheel":
            for hn, p in L.hand_points().items():
                e = bpy.data.objects.new(hn, None)
                e.empty_display_size = 0.03
                ccol.objects.link(e)
                e_col.objects.link(e)
                e.parent = o
                e.location = p
        bpy.context.view_layer.update()
        d = o.dimensions
        o.location = (x + d.x / 2, 0, 0)  # laid out in a row along +X for browsing
        x += d.x + 0.25
        rows.append((cat, name, L.tris(o), tuple(round(v, 3) for v in d)))
L.preview_setup(target=(x / 2, 0, 0.3), eye=(x / 2, -x * 0.9 - 1.5, x * 0.45 + 1.0), lens=35)
L.save_blend(L.LIBRARY_BLEND)
export_all.export_current()
for r in rows:
    print("PART", *r)
