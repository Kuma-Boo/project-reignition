shader_type spatial;
render_mode blend_add, unshaded, cull_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
const float scroll_speed = 0.1;

void fragment()
{
	vec4 col = texture(albedo, UV + vec2(0, time * scroll_speed));
	col += texture(albedo, UV + vec2(0, time * -scroll_speed));
	ALBEDO = col.rgb * COLOR.rgb;
}

