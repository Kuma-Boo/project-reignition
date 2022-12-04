shader_type spatial;
render_mode diffuse_burley, specular_disabled, depth_prepass_alpha, depth_draw_opaque;

global uniform float time;

uniform float scroll_speed : hint_range(-5.0, 5.0) = 0.0;
uniform sampler2D albedo : source_color;

void fragment()
{
	ALBEDO = texture(albedo, UV + vec2(0, time * scroll_speed)).rgb;
	ALPHA = COLOR.a;
}

