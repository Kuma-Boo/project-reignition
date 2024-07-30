shader_type spatial;
render_mode diffuse_burley, depth_draw_never, blend_add;

uniform sampler2D albedo : source_color, hint_default_black;
uniform sampler2D specular : hint_default_black;
uniform float rim_bias : hint_range(0, 5) = .5;
uniform vec4 rim_color : source_color;
uniform float emission : hint_range(0, 10) = 1.0;
uniform float opacity : hint_range(0, 1) = 0;
uniform float albedo_leak : hint_range(0, 1) = 0.2;
uniform float growth : hint_range(0, .01) = 0.005;

void vertex()
{
	VERTEX += NORMAL * growth;
}

void fragment()
{
	// Calculate rim lighting
	float view = dot(NORMAL, VIEW);
	vec4 body_col = texture(albedo, UV) * rim_color;
	vec4 spec_col = texture(specular, UV);
	float spec = spec_col.r * spec_col.a;
	float rim = spec * (rim_bias - view);
	rim = clamp(rim, 0, 1);
	
	// Calculate specularity
	spec *= view;
	ALBEDO = rim * rim_color.rgb + body_col.rgb * albedo_leak;
	ALPHA = opacity;
	EMISSION = clamp(vec3(rim) * rim * rim_color.rgb, vec3(0.0), vec3(1.0)) * emission * opacity;
}