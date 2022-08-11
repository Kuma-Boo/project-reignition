shader_type spatial;
render_mode cull_back;

uniform float scroll_speed : hint_range(0.0, 5.0);
uniform sampler2D albedo : hint_albedo;

void fragment()
{
	ALBEDO = texture(albedo, UV + vec2(0, TIME * scroll_speed)).rgb;
}

