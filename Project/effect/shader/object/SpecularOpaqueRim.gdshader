shader_type spatial;
render_mode diffuse_burley, depth_prepass_alpha;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float specular_multiplier : hint_range(0, 1) = 1;
uniform float roughness : hint_range(0, 1) = .8;
uniform float rim_bias : hint_range(0, 1) = .5;
uniform vec3 rim_color : source_color;
uniform float rim_albedo_influence : hint_range(0, 1) = .5;


void fragment()
{
	vec2 uv = UV;
	
	vec4 col = texture(albedo, uv);
	
	col = col * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	vec4 spec_col = texture(specular, uv);
	float spec = spec_col.r * spec_col.a;
	float rim = spec * (rim_bias - view);
	rim = clamp(rim, 0, 1);
	
	//Calculate specularity
	spec *= view;
	ALPHA = 1.0;
	ROUGHNESS = roughness;
	SPECULAR = clamp(spec * specular_multiplier, 0, 1);
	ALBEDO = col.rgb;
	EMISSION = clamp(vec3(rim) * rim * rim_color * NORMAL.g * ((ALBEDO * rim_albedo_influence) + (1.0 - rim_albedo_influence)), vec3(0.0), vec3(1.0));
}