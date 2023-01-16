shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D albedo : hint_default_transparent, source_color;
uniform sampler2D detail : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float roughness : hint_range(0, 1) = .8;
const float RIM_BIAS = .4;
const float RIM_MULTIPLIER = .8;
const float SPECULAR_MULTIPLIER = 2.5;

void fragment()
{
	vec2 uv = UV2;
	vec2 uv2 = UV;
	
	vec4 col = texture(albedo, uv);
	vec4 detailCol = texture(detail, uv2);
	
	col = mix(col, detailCol, 1.0 - col.a) * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	vec4 spec_col = texture(specular, uv);
	float spec = spec_col.r * spec_col.a;
	float rim = spec * (RIM_BIAS - view) * RIM_MULTIPLIER;
	rim = clamp(rim, 0, 1);
	
	//Calculate specularity
	spec *= view;
	ROUGHNESS = roughness;
	SPECULAR = clamp(spec * SPECULAR_MULTIPLIER, 0, 1);
	ALBEDO = col.rgb;
	EMISSION = vec3(rim);
}
