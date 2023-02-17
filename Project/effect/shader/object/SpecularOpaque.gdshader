shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float specular_multiplier : hint_range(0, 5) = 1;
uniform float roughness : hint_range(0, 1) = .8;

void fragment()
{
	vec2 uv = UV;
	
	vec4 col = texture(albedo, uv) * COLOR;
	
	//Calculate specularity
	vec4 spec_col = texture(specular, uv);
	float spec = spec_col.r * spec_col.a;
	float view = dot(NORMAL, VIEW);
	spec *= view;
	spec = clamp(spec * specular_multiplier, 0, 1);
	
	ROUGHNESS = roughness;
	SPECULAR = spec;
	ALBEDO = col.rgb;
}