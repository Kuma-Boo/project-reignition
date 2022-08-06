shader_type spatial;
render_mode cull_disabled, unshaded, depth_draw_alpha_prepass;

uniform float scroll_speed : hint_range(0.0, 5.0);
uniform sampler2D albedo : hint_albedo;

void fragment()
{
	vec4 col = texture(albedo, UV + vec2(TIME * scroll_speed, 0));
	ALBEDO = col.rgb;
	ALPHA = col.a * COLOR.a;
}