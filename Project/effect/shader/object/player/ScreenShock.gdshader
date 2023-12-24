shader_type canvas_item;

uniform sampler2D factor_texture : hint_default_black;
uniform sampler2D screen_texture : hint_screen_texture;
uniform float wave_count : hint_range(1.0, 20.0, 1.0) = 20.0;
uniform float speed = 20.0;
uniform float height : hint_range(0.0, 0.1, 0.001) = 0.003;

void fragment() {
	vec2 cPos = -1.0 + 2.0 * UV / (1.0 / TEXTURE_PIXEL_SIZE);
	float cLength = length(cPos);
	vec2 uv = FRAGCOORD.xy / (1.0 / SCREEN_PIXEL_SIZE).xy;
	uv += (cPos/cLength) * cos(cLength * wave_count - TIME * speed) * height * texture(factor_texture, UV).r;
    vec3 col = texture(screen_texture, uv).xyz;
	COLOR = vec4(col, COLOR.a);
}