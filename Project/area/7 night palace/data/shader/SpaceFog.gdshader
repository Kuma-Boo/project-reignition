shader_type spatial;
render_mode unshaded, blend_add, ambient_light_disabled, fog_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform float intensity = 1.5;
uniform float scroll_speed = .02;

void fragment()
{
	vec2 offset = vec2(time * scroll_speed);
	offset.y = sin(offset.y);
	vec4 col = texture(albedo, UV + offset) * intensity * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
