shader_type spatial;
render_mode diffuse_burley, depth_draw_never, unshaded, fog_disabled;

global uniform float time;

uniform sampler2D albedo : source_color, hint_default_black;
uniform float scroll_speed : hint_range(0.0, 0.1, 0.01) = .02;

void fragment()
{
	vec2 main_uv = UV + vec2(time * scroll_speed, 0);
	main_uv.y = clamp(main_uv.y, 0.0, 1.0);
	vec4 col = texture(albedo, main_uv);

	vec2 detail_uv = UV2 + vec2(time * scroll_speed, 0);
	detail_uv.y = clamp(detail_uv.y, 0.0, 1.0);
	vec4 detail_col = texture(albedo, detail_uv) * COLOR;
	col += detail_col * detail_col.a;
	
	ALBEDO = col.rgb;
	ALPHA = clamp(col.a, 0.0, 1.0);
}
