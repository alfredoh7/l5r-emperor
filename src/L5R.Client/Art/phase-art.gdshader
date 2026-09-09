shader_type canvas_item;
render_mode unshaded;
uniform sampler2D blank_lacquer : source_color, filter_linear;
uniform vec2 plate_size = vec2(1260.0,109.38);
uniform vec4 phase_weights = vec4(0.0);
uniform vec2 phase_tail = vec2(0.0);
uniform float glow_energy = 1.0;
void fragment() {
    vec2 source=vec2(0.0,188.0)+UV*vec2(1774.0,154.0);
    float centers[6]=float[6](190.0,471.0,748.0,1031.0,1319.0,1596.0);
    vec3 base=texture(TEXTURE,source/vec2(1774.0,887.0)).rgb;
    // One continuous 2D lacquer surface across the label band. Never stretch a
    // thin scanline into separate rectangles behind each heading.
    float band=smoothstep(211.0,221.0,source.y)*(1.0-smoothstep(270.0,280.0,source.y));
    band*=smoothstep(35.0,58.0,source.x)*(1.0-smoothstep(1714.0,1735.0,source.x));
    vec3 lacquer=texture(blank_lacquer,vec2(560.0+UV.x*400.0,280.0+(source.y-211.0))/vec2(2066.0,761.0)).rgb;
    base=mix(base,lacquer,band);
    vec2 activeDelta=source-vec2(1031.0,294.0);
    if(abs(activeDelta.x)<127.0&&abs(activeDelta.y)<33.0)
        base=texture(TEXTURE,(vec2(748.0,294.0)+activeDelta)/vec2(1774.0,887.0)).rgb;
    for(int i=0;i<6;i++) {
        vec2 d=source-vec2(centers[i],294.0);
        float rail=exp(-pow(d.y/7.0,2.0))*(1.0-smoothstep(104.0,129.0,abs(d.x)));
        float coin=1.0-smoothstep(19.0,31.0,length(d));
        float weight=i<4?phase_weights[i]:phase_tail[i-4];
        vec3 lit=texture(TEXTURE,(vec2(1031.0,294.0)+d)/vec2(1774.0,887.0)).rgb;
        base=mix(base,lit*glow_energy,max(rail,coin)*weight);
    }
    float alpha=smoothstep(0.0,.012,UV.x)*smoothstep(0.0,.012,1.0-UV.x)*smoothstep(0.0,.04,UV.y)*smoothstep(0.0,.08,1.0-UV.y);
    COLOR=vec4(base,alpha);
}
