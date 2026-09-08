shader_type canvas_item;
render_mode unshaded;
uniform float clock = 0.0;
uniform float atmosphere = 0.65;
uniform float motion = 1.0;
float spot(vec2 uv, vec2 center, vec2 radius) { return exp(-2.0 * dot((uv-center)/radius,(uv-center)/radius)); }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) { vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f); return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y); }
void fragment() {
    vec2 uv=UV;
    float t=clock;
    // Only sky pixels move. Buildings, window frames, moon disk and lantern remain rigid.
    float sky=smoothstep(.641,.663,uv.x)*(1.0-smoothstep(.941,.959,uv.x))*(1.0-smoothstep(.225,.285,uv.y));
    float moon=spot(uv,vec2(.804,.126),vec2(.040,.067));
    float drift=sin(t*.065)*.005*motion*atmosphere*sky*(1.0-smoothstep(.10,.55,moon));
    vec4 base=texture(TEXTURE,uv+vec2(drift,drift*.16));
    float fire=(sin(t*1.8)+.43*sin(t*3.7+.9)+.22*sin(t*7.1))*.5*motion;
    float fire2=(sin(t*1.3+2.2)+.4*sin(t*4.1))*.5*motion;
    float lamp=spot(uv,vec2(.938,.573),vec2(.08,.23));
    float small=spot(uv,vec2(.502,.49),vec2(.029,.085));
    float spill=spot(uv,vec2(.877,.79),vec2(.19,.14));
    vec3 warm=vec3(1.0,.52,.17);
    base.rgb+=warm*atmosphere*(lamp*(.055+.032*fire)+small*(.052+.033*fire2)+spill*(.012+.009*fire));
    float moonHalo=spot(uv,vec2(.804,.126),vec2(.069,.113));
    base.rgb+=vec3(.40,.55,.82)*moonHalo*atmosphere*(.014+.006*sin(t*.23)*motion);
    // Slow wind through valley haze, bounded behind the study/window and below the sky.
    float outdoors=smoothstep(.642,.659,uv.x)*(1.0-smoothstep(.858,.879,uv.x));
    float fogBand=spot(uv,vec2(.774,.419),vec2(.153,.18))*outdoors;
    float fog=noise(vec2(uv.x*12.0-t*.022*motion,uv.y*38.0+t*.007*motion));
    base.rgb+=vec3(.30,.43,.57)*(fog-.45)*fogBand*.055*atmosphere;
    float houses=0.0;
    houses+=spot(uv,vec2(.679,.342),vec2(.011,.009))*(.7+.25*sin(t*1.3)*motion);
    houses+=spot(uv,vec2(.670,.417),vec2(.016,.013))*(.7+.20*sin(t*1.1+2.0)*motion);
    houses+=spot(uv,vec2(.834,.565),vec2(.011,.012))*(.7+.20*sin(t*.9+4.0)*motion);
    houses+=spot(uv,vec2(.687,.607),vec2(.022,.014))*(.7+.20*sin(t*1.6+1.0)*motion);
    houses+=spot(uv,vec2(.650,.62),vec2(.011,.016))*(.7+.20*sin(t*1.4+3.0)*motion);
    base.rgb+=warm*houses*.15*atmosphere;
    COLOR=base;
}
