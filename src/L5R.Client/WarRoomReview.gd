extends Node3D
## Isolated architecture review, not a live match. Room geometry comes from Blender.
var camera: Camera3D
var reference: TextureRect
var status: Label
var view_name := "composition"
var capture_path := ""
var clean := false
var zoom := 1.0
var pan := Vector3.ZERO
var drag := false
var room: Node3D

func _ready() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--room-capture="): capture_path = arg.trim_prefix("--room-capture=")
        if arg.begins_with("--room-view="): view_name = arg.trim_prefix("--room-view=")
        clean = clean or arg == "--clean"
    room = load("res://Art/WarRoom/war-room-m1.glb").instantiate()
    add_child(room)
    var cloth := MeshInstance3D.new()
    var plane := PlaneMesh.new()
    plane.size = Vector2(1.8, 1.8 * 941.0 / 1672.0)
    cloth.mesh = plane
    var fabric := ShaderMaterial.new()
    fabric.shader = load("res://Art/MatFidelity/fabric.gdshader")
    fabric.set_shader_parameter("artwork", load("res://Art/navy-mat-topaz-4x.webp"))
    fabric.set_shader_parameter("detail_strength", 0.0)
    cloth.material_override = fabric
    add_child(cloth)
    var frame := load("res://Art/LacquerFrame/lacquer-frame-a.glb").instantiate() as Node3D
    var wood := ShaderMaterial.new()
    wood.shader = load("res://Art/LacquerFrame/lacquer-frame.gdshader")
    wood.set_shader_parameter("source_art", load("res://Art/LacquerFrame/approved-frame-a.png"))
    for mesh in frame.find_children("*", "MeshInstance3D"):
        mesh.material_override = wood
    add_child(frame)
    var env := Environment.new()
    env.background_mode = Environment.BG_COLOR
    env.background_color = Color("100e0b")
    env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    env.ambient_light_color = Color("b0b9c6")
    env.ambient_light_energy = .34
    env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
    var world := WorldEnvironment.new()
    world.environment = env
    add_child(world)
    # Preliminary stationary light rig. Prop production and candle animation come later.
    lamp(Vector3(-1.59, .10, -1.28), Color("ffc385"), 1.20, 1.6)
    lamp(Vector3(1.57, .12, -1.46), Color("ffc385"), 1.20, 1.6)
    lamp(Vector3(-.83, .15, .42), Color("ffac60"), .70, .70)
    lamp(Vector3(.78, .15, -.41), Color("ffac60"), .65, .68)
    lamp(Vector3(0, .15, -3.0), Color("ffad57"), .48, .95)
    var fill := DirectionalLight3D.new()
    fill.rotation_degrees = Vector3(-68, -22, 0)
    fill.light_color = Color("b4bdcc")
    fill.light_energy = .30
    fill.shadow_enabled = true
    add_child(fill)
    camera = Camera3D.new()
    camera.current = true
    camera.near = .02
    camera.far = 30
    add_child(camera)
    get_viewport().msaa_3d = Viewport.MSAA_4X
    var overlay := CanvasLayer.new()
    add_child(overlay)
    reference = TextureRect.new()
    reference.texture = ImageTexture.create_from_image(Image.load_from_file(
        ProjectSettings.globalize_path("res://../../assets/table/war-room/approved-command-chamber.png")))
    reference.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    reference.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    reference.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    reference.mouse_filter = Control.MOUSE_FILTER_IGNORE
    reference.visible = false
    overlay.add_child(reference)
    status = Label.new()
    status.text = "ARCHITECTURE · MILESTONE 1     1 Composition  ·  2 Overview  ·  3 Joinery  ·  R Reference  ·  Wheel / drag: inspect  ·  Home Reset"
    status.add_theme_font_override("font", load("res://Art/Fonts/Alegreya.ttf"))
    status.add_theme_font_size_override("font_size", 16)
    status.add_theme_color_override("font_color", Color("f2dfb6"))
    status.add_theme_color_override("font_shadow_color", Color.BLACK)
    status.add_theme_constant_override("shadow_offset_x", 1)
    status.add_theme_constant_override("shadow_offset_y", 1)
    overlay.add_child(status)
    status.visible = not clean
    get_viewport().size_changed.connect(layout)
    layout()
    print("WAR_ROOM_REVIEW: isolated Blender architecture; unchanged mat/frame. No gameplay commands or Meshy jobs.")
    if not capture_path.is_empty():
        set_process_input(false)
        for _i in range(30): await get_tree().process_frame
        reference.hide()
        await RenderingServer.frame_post_draw
        var result := get_viewport().get_texture().get_image().save_png(capture_path)
        print("WAR_ROOM_CAPTURE: ", view_name, " / ", result)
        get_tree().quit(0 if result == OK else 1)

func lamp(pos: Vector3, color: Color, energy: float, radius: float) -> void:
    var light := OmniLight3D.new()
    light.position = pos
    light.light_color = color
    light.light_energy = energy
    light.omni_range = radius
    light.omni_attenuation = 1.0
    light.shadow_enabled = true
    light.shadow_bias = .025
    add_child(light)

func layout() -> void:
    var size := get_viewport().get_visible_rect().size
    if is_instance_valid(status): status.position = Vector2(20, size.y - 30)
    var aspect := size.x / maxf(1, size.y)
    if view_name == "composition":
        # Blender 30.5 mm / 36 mm sensor with an off-centre lens. Same .64 plane pitch
        # as the game; exact live TableSurface integration is a later milestone.
        var plane_width := camera.near * 36.0 / 30.5 * zoom
        camera.keep_aspect = Camera3D.KEEP_HEIGHT
        camera.set_frustum(plane_width / aspect, Vector2(.061, -.047) * plane_width, camera.near, 30)
        camera.position = pan + Vector3(0, 2.28 * .64, 2.28 * sqrt(1.0 - .64 * .64))
        camera.look_at(pan)
    elif view_name == "overview":
        camera.set_perspective(44 * zoom, .02, 30)
        camera.position = pan + Vector3(0, 3.5, 4.5)
        camera.look_at(pan + Vector3(0, .06, -.55))
    else:
        camera.set_perspective(40 * zoom, .02, 30)
        camera.position = pan + Vector3(-1.28, .61, 1.45)
        camera.look_at(pan + Vector3(-.65, -.11, -.35))

func _input(event: InputEvent) -> void:
    if not capture_path.is_empty(): return
    if event is InputEventKey and event.pressed:
        match event.keycode:
            KEY_1: view_name = "composition"; zoom = 1.0; pan = Vector3.ZERO
            KEY_2: view_name = "overview"; zoom = 1.0; pan = Vector3.ZERO
            KEY_3: view_name = "joinery"; zoom = 1.0; pan = Vector3.ZERO
            KEY_R: reference.visible = not reference.visible
            KEY_HOME: zoom = 1.0; pan = Vector3.ZERO
            KEY_ESCAPE: get_tree().quit()
        layout()
    if event is InputEventMouseButton:
        if event.button_index in [MOUSE_BUTTON_LEFT, MOUSE_BUTTON_MIDDLE]: drag = event.pressed
        if event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
            zoom = clampf(zoom * (.90 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.10), .45, 1.6)
            layout()
    if event is InputEventMouseMotion and drag:
        pan += Vector3(-event.relative.x, 0, -event.relative.y) * .001 * zoom
        layout()
