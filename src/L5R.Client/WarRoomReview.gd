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
var painted: Array[ShaderMaterial] = []
var light_time := 0.0
var animate := true
var film_path := ""
var film_frames := 192

func _ready() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--room-capture="): capture_path = arg.trim_prefix("--room-capture=")
        if arg.begins_with("--room-view="): view_name = arg.trim_prefix("--room-view=")
        if arg.begins_with("--room-film="): film_path = arg.trim_prefix("--room-film=")
        if arg.begins_with("--room-frames="): film_frames = int(arg.trim_prefix("--room-frames="))
        if arg == "--room-motion-off": animate = false
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
    painted.append(fabric)
    add_child(cloth)
    var frame := load("res://Art/LacquerFrame/lacquer-frame-a.glb").instantiate() as Node3D
    var wood := ShaderMaterial.new()
    wood.shader = load("res://Art/LacquerFrame/lacquer-frame.gdshader")
    wood.set_shader_parameter("source_art", load("res://Art/LacquerFrame/approved-frame-a.png"))
    wood.set_shader_parameter("native_candlelight", false)
    wood.set_shader_parameter("left_lamp", Vector2(.06944, .82575))
    wood.set_shader_parameter("right_lamp", Vector2(.91667, .14463))
    for mesh in frame.find_children("*", "MeshInstance3D"):
        mesh.material_override = wood
    add_child(frame)
    painted.append(wood)
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
    status.text = "FORTRESS HALL · B3     1 Composition · 2 Overview · 3 Joinery · 4 Floor · 5 Stone · 6 Lantern · 7 Brazier · 8 Dais · 9 Chest · 0 Shoji · M Motion · R Reference · Wheel / drag · Home Reset"
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
    print("WAR_ROOM_REVIEW: isolated Blender architecture; unchanged mat/frame. No gameplay commands. Separate Meshy fixtures; shared presentation-clock firelight.")
    if not capture_path.is_empty() or not film_path.is_empty():
        set_process(false)
        set_process_input(false)
        for _i in range(30): await get_tree().process_frame
        if OS.get_cmdline_user_args().has("--room-check-motion"):
            set_light(0.0, true)
            var moving_a := rendered_image().get_data()
            set_light(4.0, true)
            var moving_b := rendered_image().get_data()
            set_light(0.0, false)
            var frozen_a := rendered_image().get_data()
            set_light(8.0, false)
            var frozen_b := rendered_image().get_data()
            set_light(2.0, true)
            var resumed := rendered_image().get_data()
            if moving_a == moving_b or frozen_a != frozen_b or resumed == frozen_b:
                push_error("Room motion check failed: visible motion, exact freeze or resume.")
                get_tree().quit(1)
                return
            print("ROOM_MOTION_PASS: phases 0/4 differ; disabled phases 0/8 byte-identical; motion resumes at phase 2.")
        if not film_path.is_empty():
            DirAccess.make_dir_recursive_absolute(film_path)
            for frame_index in range(film_frames):
                set_light(float(frame_index) / 24.0, animate)
                var result := rendered_image().save_png(film_path.path_join("frame-%04d.png" % frame_index))
                if result != OK:
                    push_error("Room motion frame could not be saved.")
                    get_tree().quit(1)
                    return
                await get_tree().process_frame
            print("ROOM_FILM_PASS: ", film_frames, " native frames at 24 fps; view=", view_name)
        set_light(0.0, animate)
        if OS.get_cmdline_user_args().has("--room-profile"):
            var started := Time.get_ticks_usec()
            for _i in range(60):
                RenderingServer.force_draw(false)
                RenderingServer.force_sync()
                await get_tree().process_frame
            var seconds := float(Time.get_ticks_usec() - started) / 1000000.0
            print("ROOM_DRAW_PROFILE: 60 forced draw/sync frames / ", seconds, " seconds = ", 60.0 / seconds, " fps; draw calls=", Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
        reference.hide()
        var result := OK
        if not capture_path.is_empty(): result = rendered_image().save_png(capture_path)
        print("WAR_ROOM_CAPTURE: ", view_name, " / ", result)
        get_tree().quit(0 if result == OK else 1)

func rendered_image() -> Image:
    RenderingServer.force_draw(false)
    RenderingServer.force_sync()
    return get_viewport().get_texture().get_image()

func set_light(time: float, moving: bool) -> void:
    room.call("PresentLight", time, moving)
    for material in painted:
        material.set_shader_parameter("candle_phase", time)
        material.set_shader_parameter("candle_motion", 1.0 if moving else 0.0)

func _process(delta: float) -> void:
    light_time += delta
    set_light(light_time, animate)

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
    elif view_name == "floor":
        camera.set_perspective(42 * zoom, .002, 30)
        var target := pan + Vector3(-1.30, -.16, -.45)
        camera.position = target + Vector3(0, .80*.64, .80*sqrt(1.0-.64*.64))
        camera.look_at(target)
    elif view_name in ["stone", "dais", "chest", "shoji"]:
        camera.set_perspective(42 * zoom, .002, 30)
        var targets := {"stone": Vector3(-1.5,-.10,-.95), "dais": Vector3(.075,-.095,-1.45), "chest": Vector3(-1.80,-.105,-1.235), "shoji": Vector3(2.075,.19,-.65)}
        var distances := {"stone": .55, "dais": 1.6, "chest": .62, "shoji": 1.25}
        var target: Vector3 = pan + targets[view_name]
        var distance: float = distances[view_name]
        camera.position = target + Vector3(0,distance*.64,distance*sqrt(1.0-.64*.64))
        if view_name == "chest": camera.position = target + Vector3(0,.32,.38)
        if view_name == "shoji": camera.position = target + Vector3(-1.28,.26,.50)
        camera.look_at(target)
        # Architecture has the same vertical compensation as the game camera.
        # Use its actual up vector for oblique inspection, avoiding a false tilt.
        if view_name in ["chest", "shoji"]: camera.look_at(target, Vector3(0,1,-.833).normalized())
    elif view_name == "room-lantern":
        camera.set_perspective(37 * zoom, .002, 30)
        var placements: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Art/WarRoom/layout-b3.json"))
        var fixture: Dictionary = placements.lamps.filter(func(item): return item.name == "RoomLantern-1_0")[0]
        var base: Array = fixture.position
        var half_height: float = fixture.height * .5
        var target := pan + Vector3(base[0], base[2] + half_height, -base[1] - half_height * .833)
        camera.position = target + Vector3(0,.36*.64,.36*sqrt(1.0-.64*.64))
        camera.look_at(target)
    elif view_name == "brazier":
        camera.set_perspective(42 * zoom, .002, 30)
        var target := pan + Vector3(-1.73, -.03, -.79)
        camera.position = target + Vector3(0, .50*.64, .50*sqrt(1.0-.64*.64))
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
            KEY_4: view_name = "floor"; zoom = 1.0; pan = Vector3.ZERO
            KEY_5: view_name = "stone"; zoom = 1.0; pan = Vector3.ZERO
            KEY_6: view_name = "room-lantern"; zoom = 1.0; pan = Vector3.ZERO
            KEY_7: view_name = "brazier"; zoom = 1.0; pan = Vector3.ZERO
            KEY_8: view_name = "dais"; zoom = 1.0; pan = Vector3.ZERO
            KEY_9: view_name = "chest"; zoom = 1.0; pan = Vector3.ZERO
            KEY_0: view_name = "shoji"; zoom = 1.0; pan = Vector3.ZERO
            KEY_M: animate = not animate
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
