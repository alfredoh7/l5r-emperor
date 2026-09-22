shader_type canvas_item;
render_mode unshaded;
uniform sampler2D destination : filter_linear_mipmap;
uniform sampler2D ink_fiber : filter_linear_mipmap, repeat_enable;
uniform float progress=0.0;
uniform float push=0.0;
uniform float softness=0.0;
uniform float fade=1.0;
uniform vec2 pixel=vec2(.00052,.00093);
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 a=floor(p),b=fract(p);b=b*b*(3.0-2.0*b);return mix(mix(hash(a),hash(a+vec2(1,0)),b.x),mix(hash(a+vec2(0,1)),hash(a+vec2(1)),b.x),b.y);}
void fragment(){
 vec2 uv=(UV-vec2(.5,.47))/(1.0+push*.065)+vec2(.5,.47);
 vec2 d=pixel*softness*3.0;
 vec4 room=texture(TEXTURE,uv)*.28;
 room+=(texture(TEXTURE,uv+d)+texture(TEXTURE,uv-d)+texture(TEXTURE,uv+vec2(d.x,-d.y))+texture(TEXTURE,uv+vec2(-d.x,d.y)))*.12;
 room+=(texture(TEXTURE,uv+d*2.0)+texture(TEXTURE,uv-d*2.0))*.12;
 room.rgb*=1.0-softness*.09;
 vec2 q=UV-vec2(.5,.5);
 // Sweep the actual generated dry-brush alpha along each diagonal edge.
 // Keep the paper fibers sharp; the prior noisy feather read as a digital wipe.
 float grain=noise(UV*vec2(80,170))*.007;
 float ridge=abs(q.x+q.y*.38);
 float radius=mix(-.14,.94,progress);
 float reveal=1.0-smoothstep(radius-.001,radius+.001,ridge+grain*.3);
 float along=UV.y*.94+.03;
 vec2 brush_uv=vec2(along,.85-.70*along+(ridge-radius)*4.2);
 float inside=step(0.0,brush_uv.y)*step(brush_uv.y,1.0);
 float rim=texture(ink_fiber,brush_uv).a*inside;
 rim*=smoothstep(.015,.1,progress)*(1.0-smoothstep(.84,1.0,progress));
 vec4 result=mix(room,texture(destination,UV),reveal);
 result.rgb=mix(result.rgb,vec3(.011,.009,.013),rim*.94);
 COLOR=vec4(result.rgb,fade);
}
