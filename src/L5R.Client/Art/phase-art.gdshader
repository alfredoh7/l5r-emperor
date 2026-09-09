shader_type canvas_item;
render_mode unshaded;
uniform vec2 plate_size = vec2(1260.0,109.38);
uniform vec4 phase_weights = vec4(0.0);
uniform vec2 phase_tail = vec2(0.0);
uniform float glow_energy = 1.0;
void fragment() {
    vec2 source=vec2(0.0,188.0)+UV*vec2(1774.0,154.0);
    float centers[6]=float[6](190.0,471.0,748.0,1031.0,1319.0,1596.0);
    vec3 base=texture(TEXTURE,source/vec2(1774.0,887.0)).rgb;
    // Blank only the generated lettering; the six frame/rail ornaments are source pixels.
    for(int i=0;i<6;i++) {
        if(abs(source.x-centers[i])<119.0&&source.y>222.0&&source.y<268.0)
            base=texture(TEXTURE,vec2(748.0+source.x-centers[i],220.0+(source.y-222.0)*.10)/vec2(1774.0,887.0)).rgb;
    }
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
