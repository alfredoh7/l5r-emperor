shader_type spatial;
render_mode unshaded,cull_disabled,world_vertex_coords,fog_disabled,depth_prepass_alpha;
#include "res://Art/CrimsonDuel/duel-wind.gdshaderinc"
uniform float wind_time=0.0;
uniform float wind_strength=1.0;
uniform bool use_art=false;
uniform sampler2D foliage_art : filter_linear_mipmap_anisotropic,repeat_disable;
varying float h;
varying float shade;
varying float distance_fade;
vec3 painted(vec3 c){
 vec3 target=pow((c+vec3(.055))/1.055,vec3(2.4));
 if(!OUTPUT_IS_SRGB)return mix(c/12.92,target,step(vec3(.04045),c));
 vec3 x=max(c,vec3(.03));
 for(int i=0;i<6;i++){vec3 value=x*(x*(x*.305306011+.682171111)+.012522878);vec3 derivative=x*(x*.915918033+1.364342222)+.012522878;x-=(value-target)/derivative;}
 return x;
}
void vertex(){
 vec3 root=MODEL_MATRIX[3].xyz;float scale=length(MODEL_MATRIX[1].xyz);
 h=1.0-UV.y;
 float phase=INSTANCE_CUSTOM.r*6.283;
 float gust=duel_gust(root.xz,wind_time);
 float swell=sin(root.x*.74+root.z*.24-wind_time*1.05);
 float stiffness=mix(.70,1.15,INSTANCE_CUSTOM.g);
 float angle=(.13+wind_strength*(.36*gust+.10*swell+.035*sin(wind_time*1.8+phase)))*stiffness*h;
 float flutter=sin(wind_time*4.8+phase+root.x)*.016*wind_strength*h*h*h;
 VERTEX.x+=scale*(sin(angle)*h+flutter);
 VERTEX.z+=scale*sin(angle*.67)*h*.26;
 VERTEX.y-=scale*h*(1.0-cos(angle));
 shade=mix(.88,1.10,INSTANCE_CUSTOM.b);
 distance_fade=mix(.92,.68,INSTANCE_CUSTOM.a);
}
void fragment(){
 vec4 art=texture(foliage_art,UV);
 vec3 c=use_art?art.rgb*vec3(1.04,.96,.81):mix(vec3(.25,.10,.03),vec3(.86,.57,.26),pow(h,.75));
 c*=shade;
 // Golden rim varies with blade depth/height, without an expensive dynamic shadow pass.
 c+=vec3(.06,.025,.004)*smoothstep(.65,1.0,h);
 ALBEDO=painted(clamp(c,vec3(.008),vec3(1.0)));
 ALPHA=(use_art?art.a:.72)*distance_fade*smoothstep(.03,.22,h);
}
