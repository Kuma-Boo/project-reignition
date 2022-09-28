shader_type spatial;
render_mode blend_add, cull_disabled;

uniform sampler2D albedo : source_color;
uniform float intensity = 2;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb * intensity;
	//EMISSION = col.rgb;
}