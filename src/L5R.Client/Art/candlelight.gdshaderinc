// Shared by the lantern, fabric and cards. No random frame noise or periodic
// on/off pulse: a slow breath, independent soft flicker and smooth turbulence.
float candle_energy(float phase, float motion) {
    float t = phase;
    float cell = floor(t * 5.3);
    float blend = smoothstep(0.0, 1.0, fract(t * 5.3));
    float a = fract(sin(cell * 127.1 + 4.7) * 43758.5453);
    float b = fract(sin((cell + 1.0) * 127.1 + 4.7) * 43758.5453);
    float fire = .13 * sin(t * 1.65) + .075 * sin(t * 3.8 + 1.2)
        + .035 * sin(t * 7.1 + .4) + .055 * (mix(a, b, blend) - .5);
    return 1.0 + fire * motion;
}
