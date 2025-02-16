shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float distortion_amount : hint_range(0.0, 1.0, 0.1) = 0.5;
uniform sampler2D albedo : hint_default_black;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

void fragment() {
	vec4 col = texture(albedo, UV);
	vec2 uv = SCREEN_UV + col.r * col.a * distortion_amount;
	ALBEDO = texture(screen_texture, uv).rgb;
	SPECULAR = 0.0;
	ROUGHNESS = 1.0;
}