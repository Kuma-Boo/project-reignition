shader_type spatial;
render_mode unshaded, blend_add, ambient_light_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
const float intensity = 1.5;
const float scroll_speed = .02;

void fragment()
{
	vec4 col = texture(albedo, UV + vec2(time * scroll_speed)) * intensity * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
