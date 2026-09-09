shader_type canvas_item;
render_mode unshaded;
uniform float age=8.0;
uniform bool still=false;
uniform float emphasis=1.0;
varying vec2 cloth;
varying vec4 stroke_color;
void vertex(){cloth=VERTEX;stroke_color=COLOR;}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
void fragment(){
    // Fragment COLOR already contains texture modulation in Godot. Preserve
    // vertex tint separately so the source is not multiplied by itself.
    vec4 ink=texture(TEXTURE,UV)*stroke_color;
    float fibers=noise(cloth*vec2(.58,.20));
    float pools=noise(cloth*.028)*.65+noise(cloth*.09)*.35;
    // Several irregular absorption fronts bloom into fibers. No perimeter wipe,
    // marching stroke, scaling or dependence on a gold-ink reveal clock.
    float wet=smoothstep(pools*.95+fibers*.26-.08,pools*.95+fibers*.26+.38,age*.76);
    float red=1.0-smoothstep(.22,.63,ink.g/max(ink.r,.001));
    float breathe=still? .5 : .5+.5*sin(age*1.65-.8);
    float settled=smoothstep(.65,2.1,age);
    float light=mix(.25,1.08+ .32*breathe,settled)*emphasis;
    vec3 stain=mix(vec3(.19,.018,.027),ink.rgb*vec3(1.50,.85,.78),settled);
    ink.rgb=mix(ink.rgb,stain*light,red);
    // User-selected 25% extra blood coverage, applied only to red pigment.
    ink.a=clamp(ink.a*mix(1.0,1.25,red),0.0,1.0)*wet;
    // A soft halo comes from the actual red pigment, never a geometric box.
    // It emerges after absorption and breathes with the settled stain.
    float halo=0.0;
    for(int i=0;i<12;i++){
        float angle=float(i)*.5235988;
        vec2 sample_uv=UV+vec2(cos(angle),sin(angle))*TEXTURE_PIXEL_SIZE*7.0;
        vec4 nearby=texture(TEXTURE,clamp(sample_uv,vec2(0),vec2(1)));
        float pigment=1.0-smoothstep(.22,.63,nearby.g/max(nearby.r,.001));
        halo+=nearby.a*pigment*nearby.r/12.0;
    }
    float glow=halo*wet*settled*(.45+.25*breathe)*emphasis;
    float alpha=ink.a+glow*(1.0-ink.a);
    COLOR=vec4((ink.rgb*ink.a+vec3(.78,.018,.028)*glow*(1.0-ink.a))/max(alpha,.001),alpha);
}
