extends Control
## Native comparison of approved image pixels with the independently modelled frame.
const MAT_SIZE := Vector2(1.8, 1.8 * 941.0 / 1672.0)
var camera: Camera3D
var viewport: SubViewport
var reference: TextureRect
var source: Texture2D
var target := Vector3.ZERO
var scale_factor := 1.0
var dragging := false
var view_name := "corner"
var capture_path := ""

func _ready() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--asset-capture="): capture_path = arg.trim_prefix("--asset-capture=")
        if arg.begins_with("--asset-view="): view_name = arg.trim_prefix("--asset-view=")
    source = load("res://Art/LacquerFrame/approved-frame-a.png")
    RenderingServer.set_default_clear_color(Color("100f0e"))
    var page := VBoxContainer.new()
    add_child(page)
    page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    page.add_theme_constant_override("separation", 12)
    var title := Label.new()
    title.text = "OPTION A · CORNER JOIN"
    title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    title.add_theme_font_override("font", load("res://Art/Fonts/WeatheredQuill-Regular.ttf"))
    title.add_theme_font_size_override("font_size", 30)
    title.add_theme_color_override("font_color", Color("d6bc82"))
    page.add_child(title)
    var controls := HBoxContainer.new()
    controls.alignment = BoxContainer.ALIGNMENT_CENTER
    page.add_child(controls)
    for item in [["Corner join · 1", "corner"], ["Whole mat · 2", "fit"], ["Overhead · 3", "top"]]:
        var button := Button.new()
        button.text = item[0]
        button.pressed.connect(func(): set_view(item[1]))
        controls.add_child(button)
    var pair := HBoxContainer.new()
    pair.size_flags_vertical = Control.SIZE_EXPAND_FILL
    page.add_child(pair)
    for label_text in ["APPROVED CONCEPT", "GODOT · BLENDER FRAME + APPROVED TOPAZ CLOTH"]:
        var column := VBoxContainer.new()
        column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
        pair.add_child(column)
        var label := Label.new()
        label.text = label_text
        label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
        column.add_child(label)
        if pair.get_child_count() == 1:
            reference = TextureRect.new()
            reference.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
            reference.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
            reference.size_flags_vertical = Control.SIZE_EXPAND_FILL
            column.add_child(reference)
        else:
            var container := SubViewportContainer.new()
            container.stretch = true
            container.size_flags_vertical = Control.SIZE_EXPAND_FILL
            column.add_child(container)
            viewport = SubViewport.new()
            viewport.own_world_3d = true
            viewport.msaa_3d = Viewport.MSAA_4X
            viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
            container.add_child(viewport)
    var status := Label.new()
    status.text = "Wheel: zoom · Drag: pan · Esc: close  |  30 mm rails · 3 mm raised lip · independent cloth · no Meshy credits"
    status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    page.add_child(status)
    build_surface()
    viewport.size_changed.connect(update_camera)
    set_view(view_name)
    if not capture_path.is_empty():
        for _i in range(24): await get_tree().process_frame
        await RenderingServer.frame_post_draw
        var result := get_viewport().get_texture().get_image().save_png(capture_path)
        print("LACQUER_FRAME_CAPTURE: ", view_name, " / ", result)
        get_tree().quit(0 if result == OK else 1)

func build_surface() -> void:
    var environment := Environment.new()
    environment.background_mode = Environment.BG_COLOR
    environment.background_color = Color("100f0e")
    environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
    environment.tonemap_exposure = 1.0
    var world := WorldEnvironment.new()
    world.environment = environment
    viewport.add_child(world)
    var cloth := MeshInstance3D.new()
    var plane := PlaneMesh.new()
    plane.size = MAT_SIZE
    cloth.mesh = plane
    var fabric := ShaderMaterial.new()
    fabric.shader = load("res://Art/MatFidelity/fabric.gdshader")
    fabric.set_shader_parameter("artwork", load("res://Art/navy-mat-topaz-4x.webp"))
    fabric.set_shader_parameter("detail_strength", 0.0)
    cloth.material_override = fabric
    viewport.add_child(cloth)
    var frame := load("res://Art/LacquerFrame/lacquer-frame-a.glb").instantiate() as Node3D
    var wood := ShaderMaterial.new()
    wood.shader = load("res://Art/LacquerFrame/lacquer-frame.gdshader")
    wood.set_shader_parameter("source_art", source)
    for mesh in frame.find_children("*", "MeshInstance3D"):
        mesh.material_override = wood
    viewport.add_child(frame)
    camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.near = .001
    camera.far = 10.0
    camera.current = true
    viewport.add_child(camera)

func set_view(value: String) -> void:
    view_name = value
    var atlas := AtlasTexture.new()
    atlas.atlas = source
    atlas.region = Rect2(15, 592, 768, 358) if value == "corner" else Rect2(15, 35, 1557, 527)
    reference.texture = atlas
    target = Vector3(-.86, 0, .48825) if value == "corner" else Vector3.ZERO
    scale_factor = .06 if value == "corner" else 1.0
    update_camera()

func update_camera() -> void:
    if not is_instance_valid(camera): return
    var aspect := float(viewport.size.x) / maxf(1, viewport.size.y)
    camera.projection = Camera3D.PROJECTION_PERSPECTIVE if view_name == "fit" else Camera3D.PROJECTION_ORTHOGONAL
    camera.size = maxf(1.12, MAT_SIZE.x / aspect * 1.08) * scale_factor
    if view_name == "corner":
        camera.position = target + Vector3(.70, 1.10, 1.45)
    elif view_name == "fit":
        camera.fov = 22
        camera.position = target + Vector3(0, 3.5, 4.125)*scale_factor
    else:
        camera.position = target + (Vector3(0, 2, 0) if view_name == "top" else Vector3(0, 1.4, 1.65))
    camera.look_at(target, Vector3.FORWARD if view_name == "top" else Vector3.UP)

func _input(event: InputEvent) -> void:
    if event is InputEventKey and event.pressed:
        match event.keycode:
            KEY_1: set_view("corner")
            KEY_2: set_view("fit")
            KEY_3: set_view("top")
            KEY_ESCAPE: get_tree().quit()
    if event is InputEventMouseButton:
        if event.button_index == MOUSE_BUTTON_LEFT: dragging = event.pressed and event.position.x > size.x * .5
        if event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
            scale_factor = clampf(scale_factor * (.88 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.12), .04, 1.6)
            update_camera()
    if event is InputEventMouseMotion and dragging:
        var amount := camera.size / maxf(1, viewport.size.y)
        target += Vector3(-event.relative.x * amount, 0, -event.relative.y * amount / .65)
        update_camera()
