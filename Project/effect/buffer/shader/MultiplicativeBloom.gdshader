shader_type canvas_item;
render_mode blend_mul;

#define pow2(x) (x * x)
#define iResolution 1.0 / SCREEN_PIXEL_SIZE

uniform sampler2D bloom_texture : hint_default_white;
uniform sampler2D object_texture : hint_default_white;

uniform float strength : hint_range(0.0, 20.0, 0.1) = 1.0;
const int samples = 16;
const float sigma = float(samples) * 0.25;

float gaussian(vec2 i) {
    return 1.0 / (2.0 * PI * pow2(sigma)) * exp(-((pow2(i.x) + pow2(i.y)) / (2.0 * pow2(sigma))));
}

vec3 sample_texture(vec2 uv) {
	vec4 col = texture(bloom_texture, uv);
	return col.rgb;
}

vec3 blur(vec2 uv, vec2 scale) {
    vec3 col = vec3(0.0);
    float accum = 0.0;
    float weight;
    vec2 offset;
    
    for (int x = -samples / 2; x < samples / 2; ++x) {
        for (int y = -samples / 2; y < samples / 2; ++y) {
            offset = vec2(float(x), float(y));
            weight = gaussian(offset);
            col += sample_texture(uv + scale * offset) * weight;
            accum += weight;
        }
    }
    
    return col / accum;
}

void fragment() {
	vec2 ps = vec2(1.0) / iResolution.xy * .000001 * strength;
	
	vec3 col = blur(UV, ps);
	col = clamp(col, vec3(0.0), vec3(1.0));
	col += texture(object_texture, UV).a;
	COLOR.rgb = col;
}