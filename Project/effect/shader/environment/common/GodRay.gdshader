shader_type spatial;
render_mode blend_add, unshaded, cull_disabled;

global uniform float time;

uniform float scroll_speed : hint_range(0.0, 5.0) = 0.0;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec4 col = texture(albedo, UV + vec2(0, time * scroll_speed));
	col += texture(albedo, UV + vec2(0, time * -scroll_speed));
	ALBEDO = col.rgb * COLOR.rgb;
}

