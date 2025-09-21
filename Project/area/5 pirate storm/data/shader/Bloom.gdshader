shader_type spatial;
render_mode blend_add, specular_disabled, cull_disabled;

uniform sampler2D albedo : source_color;
uniform float glow_intensity = 10.0;

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb * COLOR.a;
	EMISSION = col.rgb * glow_intensity * COLOR.a;
}
