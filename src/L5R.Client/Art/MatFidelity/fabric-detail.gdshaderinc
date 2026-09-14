// Fine threads are independent of the low-resolution decorative print. Suppress
// each frequency before it becomes subpixel so panning cannot create moire.
float thread_wave(float p) {
    float footprint = fwidth(p);
    return cos(p * 6.28318530718) * (1.0 - smoothstep(0.20, 0.48, footprint));
}
vec3 fabric_detail(vec3 printed, vec2 mat_uv, float strength) {
    // A 1.8 m mat with ~0.9 mm yarn spacing. UVs cover the complete outer table.
    vec2 threads = mat_uv * vec2(2000.0, 1125.598);
    float warp = thread_wave(threads.x);
    float weft = thread_wave(threads.y);
    float weave = warp * weft * 0.32 + warp * 0.34 + weft * 0.34;
    // Gold remains the approved printed artwork; avoid embossing it with cloth.
    float gold = smoothstep(0.012, 0.05, printed.r - printed.b);
    return printed * (1.0 + weave * strength * (1.0 - gold * 0.8));
}
