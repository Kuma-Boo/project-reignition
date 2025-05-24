shader_type spatial;
render_mode depth_draw_never, unshaded, fog_disabled;

global uniform float time;

uniform sampler2D base : source_color, hint_default_black;
uniform sampler2D detail : source_color, hint_default_black;
uniform float base_brightness : hint_range(0.0, 1.0, 0.1) = 0.2;
uniform float detail_brightness : hint_range(0.0, 1.0, 0.1) = 0.2;
uniform float scroll_speed : hint_range(0.0, 0.1, 0.01) = .02;

void fragment()
{
	vec2 main_uv = UV + vec2(time * scroll_speed, 0);
	vec3 col = texture(base, main_uv).rgb * base_brightness;

	vec2 secondary_uv = UV2 + vec2(time * scroll_speed, 0);
	vec4 detail_col = texture(detail, secondary_uv);
	col += detail_col.rgb * detail_brightness;

	ALBEDO = col.rgb * COLOR.rgb;
	ALPHA = COLOR.a;
}
