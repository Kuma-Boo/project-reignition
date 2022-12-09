shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

global uniform float time;

uniform float scroll_speed : hint_range(0.0, 1.0, 0.1) = 0.0;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec4 col = texture(albedo, UV + vec2(time * scroll_speed, 0)) * COLOR;
	ALBEDO = col.rgb;
}

