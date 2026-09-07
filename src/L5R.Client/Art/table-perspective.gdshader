shader_type canvas_item;
render_mode unshaded;
uniform vec2 display_size;
uniform vec2 canvas_size;
uniform float pitch = .64;
uniform float depth = .00065;
void fragment() {
    vec2 screen = (UV-.5)*display_size;
    float y = screen.y/(pitch+depth*screen.y);
    vec2 plane = vec2(screen.x*(1.0-depth*y),y);
    vec2 sample_uv = .5+plane/canvas_size;
    COLOR = texture(TEXTURE,sample_uv);
    if (any(lessThan(sample_uv,vec2(0.0))) || any(greaterThan(sample_uv,vec2(1.0)))) COLOR = vec4(0.0);
}
