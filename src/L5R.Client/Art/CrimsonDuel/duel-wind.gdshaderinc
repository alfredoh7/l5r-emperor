// Shared world-space gust for the painted field and instanced vegetation.
float duel_hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float duel_noise(vec2 p){
 vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);
 return mix(mix(duel_hash(i),duel_hash(i+vec2(1,0)),f.x),mix(duel_hash(i+vec2(0,1)),duel_hash(i+vec2(1)),f.x),f.y);
}
float duel_gust(vec2 world,float clock){
 return .62*duel_noise(world*vec2(.30,.16)-vec2(clock*.24,clock*.08))
       +.38*duel_noise(world*vec2(.62,.31)-vec2(clock*.39,clock*.12));
}
