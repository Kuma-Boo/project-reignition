shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

global uniform float time;
uniform float scroll_speed : hint_range(0.0, 5.0);
uniform sampler2D albedo : source_color;

void fragment()
{
	vec4 col = texture(albedo, UV - vec2(time * scroll_speed, 0));
	ALBEDO = col.rgb * COLOR.rgb;
	ALPHA = col.a * COLOR.a;
}