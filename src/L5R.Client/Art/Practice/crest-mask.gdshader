shader_type canvas_item;
uniform vec4 region;
void fragment(){
    vec4 color=texture(TEXTURE,UV);
    vec2 local_uv=(UV-region.xy)/region.zw;
    color.a*=1.0-smoothstep(0.485,0.5,length(local_uv-vec2(0.5)));
    COLOR=color;
}
