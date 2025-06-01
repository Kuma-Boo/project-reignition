shader_type spatial;
render_mode specular_disabled, unshaded, blend_add, fog_disabled;

global uniform float time;

uniform float scroll_speed = 0.0;
uniform float scroll_magnitude = 0.2;
uniform sampler2D albedo : source_color, repeat_disable;

void vertex()
{
	VERTEX.y += sin(time * scroll_speed) * scroll_magnitude;
}

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
