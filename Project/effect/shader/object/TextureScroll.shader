shader_type spatial;
render_mode cull_back;

uniform float scroll_speed : hint_range(0.0, 5.0);
uniform sampler2D albedo : hint_albedo;
uniform float metallic : hint_range(0.0, 1.0);
uniform float specular : hint_range(0.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0);

void fragment()
{
	ALBEDO = texture(albedo, UV + vec2(0, TIME * scroll_speed)).rgb;
	METALLIC = metallic;
	SPECULAR = specular;
	ROUGHNESS = roughness;
}

