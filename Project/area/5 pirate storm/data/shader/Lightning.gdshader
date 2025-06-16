shader_type spatial;
render_mode specular_disabled, blend_add;

global uniform float secondary_environment_fx_intensity;

uniform sampler2D base : source_color;
uniform sampler2D albedo : source_color;
uniform float glow_intensity : hint_range(0.0, 10.0, 0.1) = 2.0;

void fragment() {
	vec4 col = texture(albedo, UV2) + textureLod(albedo, UV2, 2.0) * 0.2;
	vec4 base_col = texture(base, UV);
	col *= COLOR;
	col.a *= base_col.a;
	ALBEDO = col.rgb;
	ALPHA = col.a * secondary_environment_fx_intensity;
	
	EMISSION = col.rgb * glow_intensity;
}
