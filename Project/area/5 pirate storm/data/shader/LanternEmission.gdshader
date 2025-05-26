shader_type spatial;
render_mode blend_add, specular_disabled;


uniform float glow_intensity = 10.0;

void fragment() {
	ALBEDO = COLOR.rgb;
	EMISSION = COLOR.rgb * glow_intensity;
}
