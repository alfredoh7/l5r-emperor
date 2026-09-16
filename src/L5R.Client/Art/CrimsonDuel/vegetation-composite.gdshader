shader_type canvas_item;
render_mode unshaded;
void fragment(){
 // Grass sits beyond the mat's far hem. It never crosses cloth, ornament or card hit areas.
 vec4 c=texture(TEXTURE,UV);
 c.a*=1.0-smoothstep(.360,.368,UV.y);
 COLOR=c;
}
