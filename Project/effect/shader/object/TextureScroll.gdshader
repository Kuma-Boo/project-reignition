shader_type spatial;
render_mode cull_back;

global uniform float time;
uniform float scroll_speed : hint_range(0.0, 5.0);
uniform sampler2D albedo : source_color;

void fragment()
{
	ALBEDO = texture(albedo, UV + vec2(0, time * scroll_speed)).rgb;
}

