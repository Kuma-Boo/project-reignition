shader_type spatial;
render_mode blend_add, cull_disabled;

uniform sampler2D albedo : hint_albedo;
uniform float intensity = 2;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb * intensity;
	//EMISSION = col.rgb;
}