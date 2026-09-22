shader_type canvas_item;
render_mode unshaded;
uniform float atmosphere_time = 0.0;
uniform float motion_amount = 1.0;
// Layered animated matte: only sky pixels move. The sun, mountain silhouette,
// playing surface and card layers are independently anchored.
float ridge(float x) {
    if(x < .18) return mix(.202,.275,x/.18);
    if(x < .28) return mix(.275,.252,(x-.18)/.10);
    if(x < .47) return mix(.252,.357,(x-.28)/.19);
    if(x < .71) return mix(.357,.331,(x-.47)/.24);
    if(x < .83) return mix(.331,.304,(x-.71)/.12);
    return mix(.304,.273,(x-.83)/.17);
}
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    vec3 a=fract(vec3(i.x,i.y,i.x+i.y)*vec3(.1031,.11369,.13787));
    float n00=fract(sin(dot(i,vec2(127.1,311.7)))*43758.5453);
    float n10=fract(sin(dot(i+vec2(1,0),vec2(127.1,311.7)))*43758.5453);
    float n01=fract(sin(dot(i+vec2(0,1),vec2(127.1,311.7)))*43758.5453);
    float n11=fract(sin(dot(i+vec2(1,1),vec2(127.1,311.7)))*43758.5453);
    return mix(mix(n00,n10,f.x),mix(n01,n11,f.x),f.y);
}
void fragment() {
    vec2 uv=UV;
    float t=atmosphere_time*motion_amount;
    float sky=1.0-smoothstep(ridge(uv.x)-.035,ridge(uv.x)-.008,uv.y);
    float sun=1.0-smoothstep(.90,1.24,length((uv-vec2(.569,.267))/vec2(.060,.107)));
    float edge=smoothstep(0.0,.04,uv.x)*(1.0-smoothstep(.96,1.0,uv.x));
    // Broad translation and slow local density evolution have different periods,
    // avoiding synchronized rippling. Displacement stays below 1% of the image.
    float layer=smoothstep(.10,.26,uv.y);
    vec2 travel=vec2(
      .0050*(sin(t*.018+uv.y*2.7)-sin(uv.y*2.7))+
      .0018*(sin(t*.031+uv.x*2.0)-sin(uv.x*2.0)),
      .0006*(noise(uv*9.0+vec2(t*.007,0))-noise(uv*9.0)));
    travel*=mix(1.0,.35,layer)*sky*(1.0-sun)*edge;
    vec4 c=texture(TEXTURE,uv+travel);
    // Haze variation is confined to the distant valley, never the foreground.
    float haze=exp(-pow((uv.y-.386)/.032,2.0))*.008;
    float breath=sin(t*.11+uv.x*8.0)-sin(uv.x*8.0);
    c.rgb+=vec3(.22,.10,.06)*breath*haze;
    COLOR=c;
}
