shader_type spatial;
render_mode blend_add;

uniform sampler2D lock_texture : source_color, hint_default_black;
uniform float base_glow : hint_range(0.0, 5.0, 0.1) = 5.0;
uniform float letter_glow_intensity : hint_range(0.0, 5.0, 0.1) = 3.0;

void fragment() {
	vec3 lock_col = texture(lock_texture, UV).rgb * letter_glow_intensity;
	ALBEDO = COLOR.rgb;
	EMISSION = COLOR.rgb * base_glow + lock_col;
	ALPHA = COLOR.a;
}
