shader_type canvas_item;
render_mode unshaded;

uniform vec2 section_size = vec2(500.0, 250.0);
uniform float reveal : hint_range(0.0, 1.0) = 1.0;
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
varying vec2 local_point;
varying vec4 stroke_color;

float hash(vec2 point) {
    return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 point) {
    vec2 cell = floor(point), fraction = fract(point);
    fraction = fraction * fraction * (3.0 - 2.0 * fraction);
    return mix(mix(hash(cell), hash(cell + vec2(1.0, 0.0)), fraction.x),
        mix(hash(cell + vec2(0.0, 1.0)), hash(cell + vec2(1.0)), fraction.x), fraction.y);
}

void vertex() {
    local_point = VERTEX;
    stroke_color = COLOR;
}

void fragment() {
    vec4 paint = texture(TEXTURE, UV);
    vec2 dimensions = max(section_size, vec2(1.0));
    vec2 point = clamp(local_point, vec2(0.0), dimensions);
    vec4 distance_to_edge = abs(vec4(local_point.y, local_point.x - dimensions.x,
        local_point.y - dimensions.y, local_point.x));
    float path = point.x;
    float nearest = distance_to_edge.x;
    if (distance_to_edge.y < nearest) { nearest = distance_to_edge.y; path = dimensions.x + point.y; }
    if (distance_to_edge.z < nearest) { nearest = distance_to_edge.z; path = dimensions.x + dimensions.y + dimensions.x - point.x; }
    if (distance_to_edge.w < nearest) { path = 2.0 * dimensions.x + dimensions.y + dimensions.y - point.y; }
    float travel = path / (2.0 * (dimensions.x + dimensions.y));
    float grain = noise(local_point * .29) * .58 + noise(local_point * .83 + vec2(6.0)) * .42;

    // Pigment seeps from opposite corners in several uneven fronts. Each
    // local fiber darkens gradually; there is no hard wipe or marching rectangle.
    // Explicit reveal time stops completely once the pigment has settled.
    float corner_distance = min(min(travel, 1.0-travel), abs(travel-.5));
    float broad = noise(local_point * .024 + vec2(7.0, 13.0));
    float arrival = corner_distance * 2.65 + broad * .22 + grain * .065;
    float front = mix(-.2, 1.28, reveal);
    float soak = front - arrival;
    float appearing = smoothstep(-.11, .19, soak);
    float wet = (1.0-smoothstep(.04,.30,abs(soak))) * (1.0-step(.999,reveal));
    float evaporation = 1.0 - smoothstep(grain - .2, grain + .23, dissolve * 1.5 - .25);
    if (dissolve < .001) evaporation = 1.0;

    // Champagne pigment inherits the original raster's grain and highlights,
    // while retaining enough saturation to read in the lantern-lit navy cloth.
    float luminance = dot(paint.rgb, vec3(.2126, .7152, .0722));
    vec3 pigment = mix(paint.rgb, vec3(luminance), .35);
    pigment = mix(pigment, vec3(.94, .85, .66), wet * .14);
    COLOR = vec4(pigment * stroke_color.rgb,
        paint.a * stroke_color.a * opacity * appearing * evaporation * (.9 + grain * .1 + wet * .13));
}
