shader_type spatial;
render_mode diffuse_burley, cull_disabled;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float specular_multiplier : hint_range(0, 5) = 1;
uniform float rim_bias : hint_range(0, 1) = .5;
uniform float rim_multiplier : hint_range(0, 1) = 0;
uniform float roughness : hint_range(0, 1) = .8;

void fragment()
{
	vec2 uv = UV;
	vec4 col = texture(albedo, uv);
	
	col = col * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	vec4 spec_col = texture(specular, uv);
	float spec = spec_col.r * spec_col.a;
	float rim = spec * (rim_bias - view) * rim_multiplier;
	rim = clamp(rim, 0, 1);
	
	//Calculate specularity
	spec *= view;
	ROUGHNESS = roughness;
	SPECULAR = clamp(spec * specular_multiplier, 0, 1);
	ALBEDO = col.rgb;
	EMISSION = vec3(rim);
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = .5;
}