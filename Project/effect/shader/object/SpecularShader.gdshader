shader_type spatial;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float specular_multiplier : hint_range(0, 5) = 1;
uniform float roughness : hint_range(0, 1) = .8;

void fragment()
{
	vec2 uv = UV;
	
	vec4 col = texture(albedo, uv) * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	vec4 spec_col = texture(specular, uv);
	float spec = spec_col.r * spec_col.a;
	
	//Calculate specularity
	spec *= view;
	ROUGHNESS = roughness;
	SPECULAR = clamp(spec * specular_multiplier, 0, 1);
	ALBEDO = col.rgb;
}