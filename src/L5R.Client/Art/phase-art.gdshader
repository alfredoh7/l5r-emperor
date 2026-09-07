shader_type canvas_item;
render_mode unshaded;
uniform sampler2D lit_reference : filter_linear_mipmap, repeat_disable;
uniform vec2 plate_size = vec2(1260.0,101.24);
uniform vec4 phase_weights = vec4(1.0,0.0,0.0,0.0);
uniform float glow_energy = 1.0;
void fragment() {
    // Direct bitmap presentation: no procedural outlines, rails or medallions.
    vec2 plate_uv = (vec2(0.0,260.0)+UV*vec2(2066.0,166.0))/vec2(2066.0,761.0);
    // The supplied crop cuts off its left corner. Reuse the plate's opposite end-cap,
    // as a textured nine-slice would, so the live bar has a complete matching frame.
    if (UV.x < .026) plate_uv.x = 1.0-UV.x;
    vec4 base = texture(TEXTURE,plate_uv);
    vec4 centers = vec4(230.0,778.0,1293.0,1791.0)/2066.0;
    float scale = plate_size.x/1042.0;
    for (int i=0;i<4;i++) {
        vec2 center = vec2(centers[i]*plate_size.x,124.0/166.0*plate_size.y);
        vec2 local = (UV*plate_size-center)/scale+vec2(119.0,21.0);
        if (local.x>=0.0 && local.x<=240.0 && local.y>=0.0 && local.y<=48.0) {
            vec2 delta = local-vec2(119.0,21.0);
            float rail = exp(-pow(delta.y/7.0,2.0)*1.5)*smoothstep(0.0,16.0,local.x)*smoothstep(0.0,16.0,240.0-local.x);
            float medallion = 1.0-smoothstep(16.0,24.0,length(delta));
            float feather = max(rail,medallion);
            vec3 lit = texture(lit_reference,(vec2(274.0,59.0)+local)/vec2(1042.0,122.0)).rgb;
            base.rgb = mix(base.rgb,lit*glow_energy,feather*phase_weights[i]);
        }
    }
    // The screenshot crop has a dark shadow below the metal; blend its edge into the game.
    base.a = smoothstep(0.0,.025,UV.x)*smoothstep(0.0,.025,1.0-UV.x)*smoothstep(0.0,.035,UV.y)*smoothstep(0.0,.07,1.0-UV.y);
    COLOR = base;
}
