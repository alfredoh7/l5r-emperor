shader_type canvas_item;
uniform float softness = 1.0;
varying vec4 tint;
void vertex() { tint=COLOR; }
void fragment() {
    vec2 d=TEXTURE_PIXEL_SIZE*3.0*softness;
    vec4 c=texture(TEXTURE,UV)*4.0;
    c+=texture(TEXTURE,UV+d)+texture(TEXTURE,UV-d);
    c+=texture(TEXTURE,UV+vec2(d.x,-d.y))+texture(TEXTURE,UV+vec2(-d.x,d.y));
    COLOR=tint*c/8.0;
}
