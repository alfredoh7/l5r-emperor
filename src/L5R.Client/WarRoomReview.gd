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
    room = load("res://FortressHall.tscn").instantiate()
    add_child(room)
    var cloth := MeshInstance3D.new()
    var plane := PlaneMesh.new()
    plane.size = Vector2(1.8, 1.8 * 941.0 / 1672.0)
    cloth.mesh = plane
    var fabric := ShaderMaterial.new()
    fabric.shader = load("res://Art/WarRoom/cloth.gdshader")
    fabric.set_shader_parameter("plate_texture", load("res://Art/navy-mat-topaz-4x.webp"))
    fabric.set_shader_parameter("candle_motion", 0.0)
    fabric.set_shader_parameter("environment_size", Vector2(1800, 1013.0383))
    fabric.set_shader_parameter("left_lamp", Vector2(.06944, .82575))
    fabric.set_shader_parameter("right_lamp", Vector2(.91667, .14463))
    cloth.material_override = fabric
    add_child(cloth)
    var frame := load("res://Art/LacquerFrame/lacquer-frame-a.glb").instantiate() as Node3D
    var wood := ShaderMaterial.new()
    wood.shader = load("res://Art/LacquerFrame/lacquer-frame.gdshader")
    wood.set_shader_parameter("source_art", load("res://Art/LacquerFrame/approved-frame-a.png"))
    wood.set_shader_parameter("native_candlelight", true)
    wood.set_shader_parameter("left_lamp", Vector2(.06944, .82575))
    wood.set_shader_parameter("right_lamp", Vector2(.91667, .14463))
    for mesh in frame.find_children("*", "MeshInstance3D"):
        mesh.material_override = wood
    add_child(frame)
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
        ProjectSettings.globalize_path("res://../../assets/table/war-room/approved-fortress-hall-b3.png")))
    reference.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    reference.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
    reference.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    reference.mouse_filter = Control.MOUSE_FILTER_IGNORE
    reference.visible = false
    overlay.add_child(reference)
    status = Label.new()
    status.text = "FORTRESS HALL · B3     1 Composition  ·  2 Overview  ·  3 Joinery  ·  R Reference  ·  Wheel / drag: inspect  ·  Home Reset"
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
    print("WAR_ROOM_REVIEW: isolated Blender architecture; unchanged mat/frame. No gameplay commands. Separate Meshy fixtures; animation deferred.")
    if not capture_path.is_empty():
        set_process_input(false)
        for _i in range(30): await get_tree().process_frame
        if OS.get_cmdline_user_args().has("--room-profile"):
            var started := Time.get_ticks_usec()
            for _i in range(60):
                RenderingServer.force_draw(false)
                RenderingServer.force_sync()
                await get_tree().process_frame
            var seconds := float(Time.get_ticks_usec() - started) / 1000000.0
            print("ROOM_DRAW_PROFILE: 60 forced draw/sync frames / ", seconds, " seconds = ", 60.0 / seconds, " fps; draw calls=", Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
        reference.hide()
        RenderingServer.force_draw(false)
        RenderingServer.force_sync()
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
        var plane_width := camera.near * 1672.0 / 882.0 * zoom
        camera.keep_aspect = Camera3D.KEEP_HEIGHT
        camera.set_frustum(plane_width / aspect, Vector2(26.0/1672.0,86.5/1672.0) * plane_width, camera.near, 30)
        camera.position = pan + Vector3(0, 1.54 * .64, 1.54 * sqrt(1.0 - .64 * .64))
        camera.look_at(pan)
    elif view_name == "overview":
        camera.set_perspective(44 * zoom, .02, 30)
        camera.position = pan + Vector3(0, 3.5, 4.5)
        camera.look_at(pan + Vector3(0, .06, -.55))
    elif view_name == "table-lantern":
        camera.set_perspective(37 * zoom, .002, 30)
        camera.position = pan + Vector3(-.775,.044+.24*.64,.296+.24*sqrt(1.0-.64*.64))
        camera.look_at(pan + Vector3(-.775,.044,.296))
    elif view_name == "room-lantern":
        camera.set_perspective(37 * zoom, .002, 30)
        var placements: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Art/WarRoom/layout-b3.json"))
        var fixture: Dictionary = placements.lamps.filter(func(item): return item.name == "RoomLantern-1_0")[0]
        var base: Array = fixture.position
        var half_height: float = fixture.height * .5
        var target := pan + Vector3(base[0], base[2] + half_height, -base[1] - half_height * .833)
        camera.position = target + Vector3(0,.36*.64,.36*sqrt(1.0-.64*.64))
        camera.look_at(target)
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
