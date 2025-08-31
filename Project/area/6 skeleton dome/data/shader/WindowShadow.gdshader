shader_type spatial;
render_mode blend_mul, diffuse_burley, specular_disabled;

uniform sampler2D albedo : source_color;

uniform vec4 emission_color : source_color;
uniform float emission_energy : hint_range(0.0, 100.0, 0.01);

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = vec3(0, 0, 0);
	EMISSION = vec3(1, 1, 1) + (col.rgb * emission_energy * emission_color.rgb);
}
