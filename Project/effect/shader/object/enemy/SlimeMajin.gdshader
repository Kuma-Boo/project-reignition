shader_type spatial;
render_mode diffuse_burley;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform sampler2D reflection : hint_default_transparent, source_color;
uniform float reflection_strength : hint_range(0, 1) = .2;
uniform float roughness : hint_range(0, 1) = .8;
const float RIM_BIAS = .4;
const float RIM_MULTIPLIER = .6;

void fragment()
{
	vec3 r = reflect(VIEW, NORMAL);
	float m = 2.0 * sqrt(pow(r.x, 2) + pow(r.y, 2) + pow(r.z, 2));
	vec2 reflection_uv = r.xy / m + .5;
	vec4 reflection_col = texture(reflection, reflection_uv) * reflection_strength;
	
	vec4 col = texture(albedo, UV);

	col = (col + reflection_col) * COLOR;
	float view = dot(NORMAL, VIEW);

	// Calculate rim lighting
	vec4 spec_col = texture(specular, UV);

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
