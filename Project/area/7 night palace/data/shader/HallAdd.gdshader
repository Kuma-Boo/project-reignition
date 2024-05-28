shader_type spatial;
render_mode blend_add;

uniform float emission_strength = 5.0;

void fragment()
{
	ALBEDO = COLOR.rgb;
	EMISSION = vec3(emission_strength * ALPHA * COLOR.r);
}
