shader_type canvas_item;
render_mode unshaded;
uniform float atmosphere_time=0.0;
uniform float wind_strength=1.0;
uniform bool intro_view=false;
uniform float softness=0.0;
// Independently advected painted sky bands; all horizon/sun/mat masks are in the fixed frame.
float horizon(float x){
 float a=mix(.032,.077,smoothstep(.02,.26,x));
 a=mix(a,.13,smoothstep(.26,.48,x));a=mix(a,.15,smoothstep(.48,.62,x));
 a=mix(a,.08,smoothstep(.62,.76,x));a=mix(a,.038,smoothstep(.80,1.0,x));return a;
}
vec4 flow(sampler2D artwork,vec2 uv,vec2 velocity,float speed){
 float p=fract(atmosphere_time*speed);float q=fract(p+.5);
 vec4 a=texture(artwork,clamp(uv-velocity*(p-.5),vec2(.001),vec2(.999)));
 vec4 b=texture(artwork,clamp(uv-velocity*(q-.5),vec2(.001),vec2(.999)));
 return mix(a,b,abs(p*2.0-1.0));
}
void fragment(){
 vec2 uv=UV;vec4 base=texture(TEXTURE,uv);
 vec2 sun=intro_view?vec2(.57,.26):vec2(.60,.075);
 float radius=intro_view?.069:.056;
 float protected_sun=1.0-smoothstep(radius,radius+.018,length((uv-sun)*vec2(1.0,.563)));
 float skyline=intro_view?horizon(uv.x)+.17:horizon(uv.x);
 float sky=(1.0-smoothstep(skyline-.018,skyline,uv.y))*(1.0-protected_sun);
 vec4 upper=flow(TEXTURE,uv,vec2(.035,.002),.023);
 vec4 lower=flow(TEXTURE,uv,vec2(.019,-.001),.013);
 float layer=smoothstep(0.0,intro_view?.22:.065,uv.y);
 vec4 clouds=mix(upper,lower,layer);
 vec4 c=mix(base,clouds,sky*.90);
 // Coherent waves in the distant painted field; the foreground uses actual segmented meshes.
 float top=intro_view?.45:.275;float bottom=intro_view?.58:.365;
 float field=smoothstep(top,top+.035,uv.y)*(1.0-smoothstep(bottom-.022,bottom,uv.y));
 float wave=sin(uv.x*17.0-atmosphere_time*1.3)*.65+sin(uv.x*33.0-atmosphere_time*1.9)*.35;
 vec2 grass_uv=uv+vec2(wave*.0022,sin(uv.x*21.0-atmosphere_time)*.0006)*wind_strength;
 c=mix(c,texture(TEXTURE,grass_uv),field);
 if(softness>0.0){vec2 d=TEXTURE_PIXEL_SIZE*3.0*softness;c=(c*4.0+texture(TEXTURE,uv+d)+texture(TEXTURE,uv-d)+texture(TEXTURE,uv+vec2(d.x,-d.y))+texture(TEXTURE,uv+vec2(-d.x,d.y)))/8.0;}
 COLOR=c;
}
