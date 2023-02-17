shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D roughness : hint_default_white;
uniform float specular_multiplier : hint_range(0, 1) = .5;
uniform float roughness_multiplier : hint_range(0, 1) = .8;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	vec4 spec_col = texture(roughness, UV);
	float rough = spec_col.r * spec_col.a * roughness_multiplier;
	float spec = 1.0 - (spec_col.r * spec_col.a);
	spec *= view;
	spec *= specular_multiplier;
	
	//Calculate specularity
	ROUGHNESS = rough;
	SPECULAR = spec;
	ALBEDO = col.rgb;
}