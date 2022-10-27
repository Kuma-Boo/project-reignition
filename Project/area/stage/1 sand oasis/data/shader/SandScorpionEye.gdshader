shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float rim_bias : hint_range(0, 1) = .5;
uniform float rim_multiplier : hint_range(0, 1) = 0;
uniform float roughness : hint_range(0, 1) = .8;
uniform float damage_flash : hint_range(0.0, 1.0) = 0;
uniform float defeat_tint : hint_range(0.0, 1.0) = 0;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR * defeat_tint;
	ALBEDO = mix(col.rgb, vec3(1, 0, 0), damage_flash * .8);
	
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	float spec = texture(specular, UV).r;
	float rim = spec * (rim_bias - view) * rim_multiplier;
	rim = clamp(rim, 0, 1);
	
	//Calculate specularity
	spec *= view;
	ROUGHNESS = roughness;
	SPECULAR = spec;
	EMISSION = vec3(rim);
}