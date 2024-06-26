shader_type spatial;
render_mode specular_disabled;

uniform sampler2D albedo : source_color;
uniform float emission_strength = 2.0;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	EMISSION += col.rgb * emission_strength;
}