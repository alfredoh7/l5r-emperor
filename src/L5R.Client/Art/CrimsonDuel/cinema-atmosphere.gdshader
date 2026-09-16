shader_type canvas_item;
render_mode unshaded;
uniform float atmosphere_time=0.0;
void fragment(){
    vec2 uv=UV;
    // Keep the sun, mountain silhouette, cloth, filigree and all live cards fixed.
    float cloud_mask=1.0-smoothstep(.06,.14,length((uv-vec2(.60,.075))*vec2(1.0,1.78)));
    float sky=(1.0-smoothstep(.06,.17,uv.y))*(1.0-cloud_mask);
    float field=smoothstep(.23,.31,uv.y)*(1.0-smoothstep(.35,.365,uv.y));
    float wave=sin(uv.x*18.0+atmosphere_time*.8)*.6+sin(uv.x*39.0-atmosphere_time*1.35)*.4;
    uv.x+=sky*sin(atmosphere_time*.045)*.003;
    uv.x+=field*wave*sin(atmosphere_time*.65)*.0018;
    uv.y+=field*sin(uv.x*32.0+atmosphere_time)*sin(atmosphere_time*.4)*.0005;
    COLOR=texture(TEXTURE,uv);
}
