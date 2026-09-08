shader_type canvas_item;
uniform bool white_cutout = false;
uniform float saturation = 0.53;
uniform vec2 decal_size = vec2(94.0);
varying vec2 decal_uv;
void vertex() { decal_uv = VERTEX; }
void fragment() {
    vec4 c = COLOR;
    float gray=dot(c.rgb,vec3(.299,.587,.114));
    // Preserve the supplied marks exactly; age their material at display time.
    c.rgb=mix(vec3(gray),c.rgb,saturation)*vec3(.85,.79,.69);
    float grain=fract(sin(dot(decal_uv,vec2(12.9898,78.233)))*43758.5453);
    c.rgb*=.94+.10*grain;
    if(white_cutout) {
        c.a*=1.0-smoothstep(.82,.96,min(COLOR.r,min(COLOR.g,COLOR.b)));
        c.a*=1.0-smoothstep(.487,.50,length(decal_uv/decal_size-vec2(.5)));
    }
    COLOR=c;
}
