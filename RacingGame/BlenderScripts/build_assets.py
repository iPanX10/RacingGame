import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Matrix

# Output folder
def get_out_dir():
    argv = sys.argv
    if "--" in argv:
        args = argv[argv.index("--") + 1:]
        if "--out" in args:
            return os.path.abspath(args[args.index("--out") + 1])
    if bpy.data.filepath:
        base = os.path.dirname(bpy.data.filepath)
    else:
        try:
            base = os.path.dirname(os.path.abspath(__file__))
        except NameError:
            base = os.getcwd()
    return os.path.abspath(os.path.join(base, "..", "Assets", "Models"))

OUT_DIR = get_out_dir()
os.makedirs(OUT_DIR, exist_ok=True)

# Helper scene / material
def reset_scene():
    """Kosongkan scene tanpa memuat ulang file (aman dijalankan dari editor teks Blender)."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.worlds):
        for d in list(coll):
            coll.remove(d)


_mat_cache = {}

def make_material(name, color, metallic=0.0, roughness=0.5, emission=0.0):
    key = (name, color, metallic, roughness, emission)
    if key in _mat_cache and _mat_cache[key].name in bpy.data.materials:
        return _mat_cache[key]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.metallic = metallic
    mat.roughness = roughness
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission > 0:
            if "Emission Color" in bsdf.inputs:          # Blender 4.x
                bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
            elif "Emission" in bsdf.inputs:              # Blender 3.x
                bsdf.inputs["Emission"].default_value = (*color, 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission
    _mat_cache[key] = mat
    return mat

def new_object(name, bm, material=None, location=(0, 0, 0), parent=None):
    """Ubah bmesh menjadi object, tambahkan material, smooth/flat shading."""
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    if material:
        obj.data.materials.append(material)
    if parent:
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj

def shade_smooth(obj, angle_deg=40):
    """Smooth shading dengan batas sudut (tanpa operator, aman headless)."""
    for p in obj.data.polygons:
        p.use_smooth = True
    try:                                  # Blender 4.1+
        obj.data.shade_auto_smooth(angle=math.radians(angle_deg))
    except Exception:
        try:                              # Blender 3.x / 4.0
            obj.data.use_auto_smooth = True
            obj.data.auto_smooth_angle = math.radians(angle_deg)
        except Exception:
            pass

def shade_flat(obj):
    for p in obj.data.polygons:
        p.use_smooth = False

def box(bm, size, offset=(0, 0, 0)):
    """Tambah kotak (sx, sy, sz) berpusat di offset ke bmesh."""
    ret = bmesh.ops.create_cube(bm, size=1.0)
    for v in ret["verts"]:
        v.co.x = v.co.x * size[0] + offset[0]
        v.co.y = v.co.y * size[1] + offset[1]
        v.co.z = v.co.z * size[2] + offset[2]
    return ret["verts"]

def cylinder(bm, radius, depth, segments=24, offset=(0, 0, 0), axis="Z", r2=None):
    """Silinder / kerucut terpotong. axis = Z, X atau Y."""
    ret = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segments,
        radius1=radius, radius2=radius if r2 is None else r2, depth=depth,
    )
    verts = ret["verts"]
    if axis == "X":
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0),
                         matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
    elif axis == "Y":
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0),
                         matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    for v in verts:
        v.co += Vector(offset)
    return verts

def bevel_all(bm, offset=0.05, segments=2):
    bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                    offset=offset, segments=segments, affect="EDGES")

def export_fbx(objects, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )
    print("[OK] export:", path)
    return path

# 1. MOBIL
def build_car():
    reset_scene()
    _mat_cache.clear()

    paint = make_material("Car_Paint", (0.75, 0.04, 0.05), metallic=0.6, roughness=0.25)
    glass = make_material("Car_Glass", (0.05, 0.09, 0.13), metallic=0.1, roughness=0.05)
    trim = make_material("Car_Trim", (0.03, 0.03, 0.035), metallic=0.2, roughness=0.6)
    tire = make_material("Car_Tire", (0.02, 0.02, 0.02), roughness=0.9)
    rim = make_material("Car_Rim", (0.75, 0.77, 0.80), metallic=1.0, roughness=0.25)
    head = make_material("Car_Headlight", (1.0, 0.95, 0.8), emission=3.0)
    tail = make_material("Car_Taillight", (1.0, 0.05, 0.03), emission=3.0)
    stripe = make_material("Car_Stripe", (0.95, 0.95, 0.95), metallic=0.3, roughness=0.3)

    # Ukuran (meter): panjang 4.2, lebar 1.9, tinggi ~1.2 ; depan = -Y
    L, W = 4.2, 1.9
    ground_clear = 0.22
    wheel_r = 0.34
    wheel_w = 0.26
    wheelbase = 2.55
    track = 1.62          # jarak kiri-kanan pusat roda

    root = bpy.data.objects.new("Car", None)
    bpy.context.collection.objects.link(root)

    # --- Body bawah (sasis + bodi utama) ---
    bm = bmesh.new()
    box(bm, (W, L, 0.55), (0, 0, ground_clear + 0.275))
    bevel_all(bm, 0.14, 3)
    body = new_object("Body", bm, paint, parent=root)
    shade_flat(body)

    # --- Kap mesin (hood) & bagasi: panel miring ---
    bm = bmesh.new()
    box(bm, (W - 0.12, 1.25, 0.14), (0, -1.38, ground_clear + 0.60))
    bevel_all(bm, 0.05, 2)
    hood = new_object("Hood", bm, paint, parent=root)
    shade_flat(hood)

    bm = bmesh.new()
    box(bm, (W - 0.12, 0.95, 0.16), (0, 1.55, ground_clear + 0.62))
    bevel_all(bm, 0.05, 2)
    trunk = new_object("Trunk", bm, paint, parent=root)
    shade_flat(trunk)

    # --- Kabin: kotak yang bagian atasnya disempitkan (tapered) ---
    bm = bmesh.new()
    verts = box(bm, (1.62, 1.85, 0.52), (0, 0.15, ground_clear + 0.55 + 0.26))
    top_z = max(v.co.z for v in verts)
    for v in verts:
        if abs(v.co.z - top_z) < 1e-4:
            v.co.x *= 0.80
            v.co.y *= 0.72
    bevel_all(bm, 0.06, 2)
    cabin = new_object("Cabin", bm, glass, parent=root)
    shade_flat(cabin)

    # --- Atap (panel bodi di atas kaca) ---
    bm = bmesh.new()
    box(bm, (1.18, 1.25, 0.05), (0, 0.18, ground_clear + 0.55 + 0.52 + 0.015))
    bevel_all(bm, 0.02, 2)
    roof = new_object("Roof", bm, paint, parent=root)
    shade_flat(roof)

    # --- Strip balap (racing stripe) di tengah ---
    bm = bmesh.new()
    box(bm, (0.34, 1.26, 0.012), (0, -1.38, ground_clear + 0.675))
    box(bm, (0.34, 0.96, 0.012), (0, 1.55, ground_clear + 0.70))
    box(bm, (0.34, 1.25, 0.012), (0, 0.18, ground_clear + 0.55 + 0.52 + 0.047))
    st = new_object("Stripe", bm, stripe, parent=root)
    shade_flat(st)

    # --- Bumper depan/belakang + splitter + diffuser ---
    bm = bmesh.new()
    box(bm, (W + 0.04, 0.22, 0.30), (0, -L / 2 + 0.05, ground_clear + 0.20))
    box(bm, (W - 0.1, 0.42, 0.04), (0, -L / 2 - 0.12, ground_clear + 0.03))
    bevel_all(bm, 0.03, 2)
    fbump = new_object("Bumper_Front", bm, trim, parent=root)
    shade_flat(fbump)

    bm = bmesh.new()
    box(bm, (W + 0.04, 0.22, 0.30), (0, L / 2 - 0.05, ground_clear + 0.20))
    box(bm, (W - 0.3, 0.30, 0.06), (0, L / 2 + 0.02, ground_clear + 0.04))
    bevel_all(bm, 0.03, 2)
    rbump = new_object("Bumper_Rear", bm, trim, parent=root)
    shade_flat(rbump)

    # --- Spoiler belakang ---
    bm = bmesh.new()
    box(bm, (W - 0.1, 0.42, 0.05), (0, L / 2 - 0.20, ground_clear + 1.02))
    box(bm, (0.06, 0.20, 0.30), (-0.65, L / 2 - 0.20, ground_clear + 0.86))
    box(bm, (0.06, 0.20, 0.30), (0.65, L / 2 - 0.20, ground_clear + 0.86))
    bevel_all(bm, 0.012, 1)
    spoiler = new_object("Spoiler", bm, trim, parent=root)
    shade_flat(spoiler)

    # --- Lampu depan (headlight) ---
    bm = bmesh.new()
    box(bm, (0.50, 0.06, 0.15), (-0.62, -L / 2 - 0.005, ground_clear + 0.42))
    box(bm, (0.50, 0.06, 0.15), (0.62, -L / 2 - 0.005, ground_clear + 0.42))
    hl = new_object("Headlights", bm, head, parent=root)
    shade_flat(hl)

    # --- Lampu belakang (taillight) ---
    bm = bmesh.new()
    box(bm, (0.62, 0.06, 0.12), (-0.55, L / 2 + 0.005, ground_clear + 0.45))
    box(bm, (0.62, 0.06, 0.12), (0.55, L / 2 + 0.005, ground_clear + 0.45))
    tl = new_object("Taillights", bm, tail, parent=root)
    shade_flat(tl)

    # --- Spion samping ---
    bm = bmesh.new()
    box(bm, (0.12, 0.10, 0.08), (-W / 2 - 0.08, -0.55, ground_clear + 0.74))
    box(bm, (0.12, 0.10, 0.08), (W / 2 + 0.08, -0.55, ground_clear + 0.74))
    mir = new_object("Mirrors", bm, trim, parent=root)
    shade_flat(mir)

    # --- Roda (4 objek terpisah; origin = pusat roda) ---
    pos = {
        "FL": (-track / 2, -wheelbase / 2),
        "FR": (track / 2, -wheelbase / 2),
        "RL": (-track / 2, wheelbase / 2),
        "RR": (track / 2, wheelbase / 2),
    }
    for tag, (x, y) in pos.items():
        side = -1 if x < 0 else 1
        # Ban
        bm = bmesh.new()
        cylinder(bm, wheel_r, wheel_w, 28, axis="X")
        wheel = new_object("Wheel_" + tag, bm, tire,
                           location=(x, y, wheel_r), parent=root)
        shade_smooth(wheel, 50)

        # Velg (disk logam + 5 jari)
        bm = bmesh.new()
        cylinder(bm, wheel_r * 0.68, wheel_w + 0.02, 28, axis="X")
        for i in range(5):
            ang = i * 2 * math.pi / 5
            ret = bmesh.ops.create_cube(bm, size=1.0)
            for v in ret["verts"]:
                v.co.x = v.co.x * (wheel_w + 0.04)
                v.co.y = v.co.y * 0.07
                v.co.z = v.co.z * (wheel_r * 1.2)
            bmesh.ops.rotate(bm, verts=ret["verts"], cent=(0, 0, 0),
                             matrix=Matrix.Rotation(ang, 3, "X"))
        rimobj = new_object("Rim_" + tag, bm, rim, parent=wheel)
        rimobj.location = (0, 0, 0)
        rimobj.matrix_parent_inverse = wheel.matrix_world.inverted()
        shade_smooth(rimobj, 30)


    # --- Pivot Empty (opsional) untuk kamera & posisi pengemudi ---
    cam = bpy.data.objects.new("CameraTarget", None)
    bpy.context.collection.objects.link(cam)
    cam.location = (0, 0.3, 1.2)
    cam.parent = root

    export_fbx([root] + [o for o in bpy.data.objects if o is not root], "Car.fbx")

# 2. KERUCUT LALU LINTAS
def build_cone():
    reset_scene()
    _mat_cache.clear()
    orange = make_material("Cone_Orange", (1.0, 0.35, 0.02), roughness=0.5)
    white = make_material("Cone_White", (0.95, 0.95, 0.95), roughness=0.5)
    black = make_material("Cone_Base", (0.04, 0.04, 0.04), roughness=0.7)

    root = bpy.data.objects.new("Cone", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    box(bm, (0.5, 0.5, 0.04), (0, 0, 0.02))
    bevel_all(bm, 0.01, 1)
    base = new_object("Cone_Base", bm, black, parent=root)
    shade_flat(base)

    bm = bmesh.new()
    cylinder(bm, 0.20, 0.55, 20, offset=(0, 0, 0.04 + 0.275), r2=0.04)
    body = new_object("Cone_Body", bm, orange, parent=root)
    shade_flat(body)

    bm = bmesh.new()
    cylinder(bm, 0.115, 0.10, 20, offset=(0, 0, 0.04 + 0.30), r2=0.095)
    ring = new_object("Cone_Ring", bm, white, parent=root)
    ring.scale = (1.0, 1.0, 1.0)
    shade_smooth(ring)

    export_fbx([root] + [o for o in bpy.data.objects if o is not root], "Cone.fbx")

# 3. BARRIER (pembatas)
def build_barrier():
    reset_scene()
    _mat_cache.clear()
    red = make_material("Barrier_Red", (0.85, 0.05, 0.05), roughness=0.6)
    white = make_material("Barrier_White", (0.95, 0.95, 0.95), roughness=0.6)
    dark = make_material("Barrier_Leg", (0.08, 0.08, 0.09), roughness=0.7)

    root = bpy.data.objects.new("Barrier", None)
    bpy.context.collection.objects.link(root)

    # Panjang 3 m di sumbu X, tinggi 0.9 m
    red_bm = bmesh.new()
    white_bm = bmesh.new()
    for i in range(6):
        x = -1.25 + i * 0.5
        target = red_bm if i % 2 == 0 else white_bm
        box(target, (0.5, 0.18, 0.28), (x, 0, 0.62))
    r_obj = new_object("Barrier_PanelRed", red_bm, red, parent=root)
    w_obj = new_object("Barrier_PanelWhite", white_bm, white, parent=root)
    shade_flat(r_obj)
    shade_flat(w_obj)

    legs = bmesh.new()
    for x in (-1.2, 1.2):
        box(legs, (0.12, 0.12, 0.50), (x, 0, 0.25))
        box(legs, (0.12, 0.45, 0.06), (x, 0, 0.03))
    leg_obj = new_object("Barrier_Legs", legs, dark, parent=root)
    shade_flat(leg_obj)

    export_fbx([root] + [o for o in bpy.data.objects if o is not root], "Barrier.fbx")

# 4. POHON
def build_tree():
    reset_scene()
    _mat_cache.clear()
    bark = make_material("Tree_Bark", (0.32, 0.20, 0.10), roughness=0.9)
    leaf1 = make_material("Tree_Leaf", (0.10, 0.45, 0.12), roughness=0.8)

    root = bpy.data.objects.new("Tree", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    cylinder(bm, 0.22, 1.6, 8, offset=(0, 0, 0.8), r2=0.16)
    trunk = new_object("Tree_Trunk", bm, bark, parent=root)
    shade_flat(trunk)

    bm = bmesh.new()
    cylinder(bm, 1.5, 1.7, 9, offset=(0, 0, 2.3), r2=0.15)
    cylinder(bm, 1.2, 1.5, 9, offset=(0, 0, 3.25), r2=0.10)
    cylinder(bm, 0.85, 1.3, 9, offset=(0, 0, 4.15), r2=0.05)
    leaves = new_object("Tree_Leaves", bm, leaf1, parent=root)
    shade_flat(leaves)

    export_fbx([root] + [o for o in bpy.data.objects if o is not root], "Tree.fbx")

# 5. GERBANG START / FINISH
def build_start_gate():
    reset_scene()
    _mat_cache.clear()
    metal = make_material("Gate_Metal", (0.18, 0.18, 0.20), metallic=0.8, roughness=0.4)
    black = make_material("Gate_Black", (0.02, 0.02, 0.02), roughness=0.5)
    white = make_material("Gate_White", (0.97, 0.97, 0.97), roughness=0.5)
    light = make_material("Gate_Light", (0.1, 1.0, 0.2), emission=4.0)

    root = bpy.data.objects.new("StartGate", None)
    bpy.context.collection.objects.link(root)

    # Lebar bukaan 12 m, tinggi 6 m. Jalan melewati sumbu Y (maju = -Y).
    bm = bmesh.new()
    box(bm, (0.5, 0.5, 6.0), (-6.25, 0, 3.0))
    box(bm, (0.5, 0.5, 6.0), (6.25, 0, 3.0))
    box(bm, (13.0, 0.6, 0.8), (0, 0, 6.0))
    bevel_all(bm, 0.03, 1)
    frame = new_object("Gate_Frame", bm, metal, parent=root)
    shade_flat(frame)

    # Papan kotak-kotak (checker) di balok atas
    b_bm = bmesh.new()
    w_bm = bmesh.new()
    cols, rows = 26, 2
    cw = 12.6 / cols
    ch = 0.7 / rows
    for r in range(rows):
        for c in range(cols):
            x = -6.3 + cw * (c + 0.5)
            z = 5.65 + ch * (r + 0.5)
            target = b_bm if (r + c) % 2 == 0 else w_bm
            box(target, (cw, 0.04, ch), (x, -0.32, z))
    bl = new_object("Gate_CheckerBlack", b_bm, black, parent=root)
    wh = new_object("Gate_CheckerWhite", w_bm, white, parent=root)
    shade_flat(bl)
    shade_flat(wh)

    # Lampu hijau di bawah balok
    bm = bmesh.new()
    for i in range(5):
        box(bm, (0.5, 0.2, 0.2), (-2.0 + i, -0.1, 5.5))
    lt = new_object("Gate_Lights", bm, light, parent=root)
    shade_flat(lt)

    export_fbx([root] + [o for o in bpy.data.objects if o is not root], "StartGate.fbx")

# Preview render (Workbench) -> Assets/Models/Car_preview.png
def build_car_for_preview():
    """Bangun ulang mobil lalu render gambar pratinjau (Workbench, cepat)."""
    build_car()  # scene tetap berisi mobil setelah export
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("W")
    world.color = (0.12, 0.13, 0.16)
    scene.world = world

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (-5.2, -6.2, 2.6)
    direction = Vector((0, 0, 0.7)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    # lantai
    bm = bmesh.new()
    box(bm, (30, 30, 0.02), (0, 0, -0.01))
    floor = new_object("Floor", bm, make_material("Floor", (0.25, 0.26, 0.28), roughness=0.9))

    scene.render.filepath = os.path.join(OUT_DIR, "Car_preview.png")
    bpy.ops.render.render(write_still=True)
    print("[OK] preview:", scene.render.filepath)


def main():
    print("Output folder:", OUT_DIR)
    build_cone()
    build_barrier()
    build_tree()
    build_start_gate()
    build_car_for_preview()     # membangun + export Car.fbx + render preview
    print("Selesai! Semua FBX ada di:", OUT_DIR)

if __name__ == "__main__":
    import traceback
    try:
        main()
    except Exception:
        print("=" * 60)
        print("ERROR - kirim teks di bawah ini ke Claude:")
        traceback.print_exc()
        print("=" * 60)
        raise