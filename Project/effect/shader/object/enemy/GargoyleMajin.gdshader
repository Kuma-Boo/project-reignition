shader_type spatial;
render_mode diffuse_burley, depth_draw_always;

instance uniform float defeat_blend : hint_range(0.0, 1.0) = 0.0;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform sampler2D detail : hint_default_transparent, source_color;
uniform sampler2D detail_specular : hint_default_black;
uniform float roughness : hint_range(0, 1) = .8;
const float RIM_BIAS = .4;
const float RIM_MULTIPLIER = .6;

void fragment()
{
	vec4 col = mix(texture(albedo, UV), texture(specular, UV), defeat_blend);
	vec4 detailCol = texture(detail, UV2);
	
	col = mix(col, detailCol, detailCol.a) * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate rim lighting
	vec4 spec_col = texture(specular, UV);
	spec_col = mix(spec_col, texture(detail_specular, UV2), detailCol.a);
	
	float spec = spec_col.r * spec_col.a;
	float rim = spec * (RIM_BIAS - view) * RIM_MULTIPLIER;
	rim = clamp(rim, 0, 1);
	
	//Calculate specularity
	spec *= view;
	ROUGHNESS = roughness;
	SPECULAR = spec;
	ALBEDO = col.rgb;
	EMISSION = clamp((col.rgb + vec3(rim)) * rim * .5, vec3(0.0), vec3(1.0));
}
