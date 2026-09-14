extends Control
## Native asset review only; this does not replace the current playable table.

const MAT_SIZE := Vector2(1.8, 1.8 * 941.0 / 1672.0)
var cameras: Array[Camera3D] = []
var viewports: Array[SubViewport] = []
var cloth_material: ShaderMaterial
var detail_enabled := false
var target := Vector3.ZERO
var view_scale := 1.0
var overhead := false
var dragging := false
var selected_view := "fit"
var capture_path := ""
var status: Label

func _ready() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--asset-capture="): capture_path = arg.trim_prefix("--asset-capture=")
        if arg.begins_with("--asset-view="): selected_view = arg.trim_prefix("--asset-view=")
    RenderingServer.set_default_clear_color(Color("090c10"))
    var page := VBoxContainer.new()
    add_child(page)
    page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    page.add_theme_constant_override("separation", 10)
    var title := Label.new()
    title.text = "PLAYMAT · MATERIAL & FRAME REVIEW"
    title.add_theme_font_override("font", load("res://Art/Fonts/WeatheredQuill-Regular.ttf"))
    title.add_theme_font_size_override("font_size", 28)
    title.add_theme_color_override("font_color", Color("d6bc82"))
    title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    page.add_child(title)
    var controls := HBoxContainer.new()
    controls.alignment = BoxContainer.ALIGNMENT_CENTER
    page.add_child(controls)
    for item in [["Fit · 1", "fit"], ["Fabric close-up · 2", "cloth"], ["Frame close-up · 3", "frame"], ["Top view · 4", "top"]]:
        var button := Button.new()
        button.text = item[0]
        button.pressed.connect(func(): set_view(item[1]))
        controls.add_child(button)
    var detail_button := Button.new()
    detail_button.text = "Toggle fabric detail · D"
    detail_button.pressed.connect(toggle_detail)
    controls.add_child(detail_button)
    var pair := HBoxContainer.new()
    pair.size_flags_vertical = Control.SIZE_EXPAND_FILL
    page.add_child(pair)
    for proposed in [false, true]:
        var column := VBoxContainer.new()
        column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
        pair.add_child(column)
        var label := Label.new()
        label.text = "ORIGINAL · 1672 × 941" if not proposed else "TOPAZ DERIVATIVE · 6688 × 3764 / separate frame prototype"
        label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
        label.add_theme_font_size_override("font_size", 18)
        column.add_child(label)
        var container := SubViewportContainer.new()
        container.stretch = true
        container.size_flags_vertical = Control.SIZE_EXPAND_FILL
        column.add_child(container)
        var viewport := SubViewport.new()
        viewport.own_world_3d = true
        viewport.msaa_3d = Viewport.MSAA_4X
        viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
        container.add_child(viewport)
        viewports.append(viewport)
        build_surface(viewport, proposed)
    status = Label.new()
    status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
    status.add_theme_font_size_override("font_size", 16)
    page.add_child(status)
    resized.connect(update_cameras)
    set_view(selected_view)
    if not capture_path.is_empty():
        for _i in range(24): await get_tree().process_frame
        if cameras.size() != 2 or not is_instance_valid(cloth_material):
            push_error("Asset review is missing required imported resources.")
            get_tree().quit(1)
            return
        await RenderingServer.frame_post_draw
        var result := get_viewport().get_texture().get_image().save_png(capture_path)
        print("MAT_ASSET_CAPTURE: ", selected_view, " / ", result, " / ", capture_path)
        get_tree().quit(0 if result == OK else 1)

func build_surface(viewport: SubViewport, proposed: bool) -> void:
    var env_node := WorldEnvironment.new()
    var environment := Environment.new()
    environment.background_mode = Environment.BG_COLOR
    environment.background_color = Color("090c10")
    environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
    environment.tonemap_exposure = 1.0
    env_node.environment = environment
    viewport.add_child(env_node)
    var fabric := ShaderMaterial.new()
    fabric.shader = load("res://Art/MatFidelity/fabric.gdshader")
    fabric.set_shader_parameter("artwork", load("res://Art/navy-mat-v4.png"))
    fabric.set_shader_parameter("detail_strength", 0.0)
    if proposed:
        var model := load("res://Art/MatFidelity/navy-mat-separated.tscn").instantiate() as Node3D
        viewport.add_child(model)
        cloth_material = model.get_node("FabricInsert").material_override
    else:
        var plate := MeshInstance3D.new()
        var plane := PlaneMesh.new()
        plane.size = MAT_SIZE
        plate.mesh = plane
        plate.material_override = fabric
        plate.position.y = .006
        viewport.add_child(plate)
    var camera := Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.near = .001
    camera.far = 10
    viewport.add_child(camera)
    camera.current = true
    cameras.append(camera)
    viewport.size_changed.connect(update_cameras)

func set_view(name: String) -> void:
    selected_view = name
    overhead = name == "top"
    if name == "cloth":
        target = Vector3(-.08, 0, .04)
        view_scale = .15
    elif name == "frame":
        target = Vector3(-.70, 0, .47)
        view_scale = .23
    else:
        target = Vector3.ZERO
        view_scale = 1.0
    update_cameras()

func toggle_detail() -> void:
    detail_enabled = not detail_enabled
    cloth_material.set_shader_parameter("detail_strength", .13 if detail_enabled else 0.0)
    update_cameras()

func update_cameras() -> void:
    for i in cameras.size():
        var camera := cameras[i]
        var pixel_size := viewports[i].size
        var aspect := float(pixel_size.x) / maxf(1, pixel_size.y)
        camera.size = maxf(1.12, MAT_SIZE.x / aspect * 1.08) * view_scale
        camera.position = target + (Vector3(0, 2, 0) if overhead else Vector3(0, 1.4, 1.65))
        camera.look_at(target, Vector3.FORWARD if overhead else Vector3.UP)
    if is_instance_valid(status):
        status.text = "Wheel: zoom · Drag: pan · Esc: close  |  Extra threads: %s  |  Lossless WebP · source-matched UVs · frame prototype only" % ("on" if detail_enabled else "off")

func _input(event: InputEvent) -> void:
    if event is InputEventKey and event.pressed:
        match event.keycode:
            KEY_1: set_view("fit")
            KEY_2: set_view("cloth")
            KEY_3: set_view("frame")
            KEY_4: set_view("top")
            KEY_D: toggle_detail()
            KEY_ESCAPE: get_tree().quit()
    if event is InputEventMouseButton:
        if event.button_index == MOUSE_BUTTON_LEFT:
            dragging = event.pressed and event.position.y > 100
        if event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
            view_scale = clampf(view_scale * (.88 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.12), .06, 1.6)
            update_cameras()
    if event is InputEventMouseMotion and dragging and not cameras.is_empty():
        var amount := cameras[0].size / maxf(1, viewports[0].size.y)
        target += Vector3(-event.relative.x * amount, 0, -event.relative.y * amount / (.65 if not overhead else 1.0))
        update_cameras()
