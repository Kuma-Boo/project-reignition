shader_type spatial;
render_mode unshaded, cull_disabled, blend_add;

global uniform float time;

uniform float scroll_speed : hint_range(0.0, 1.0, 0.1) = 0.0;
uniform sampler2D albedo : source_color;
uniform float intensity = 1;

void fragment()
{
	vec4 col = texture(albedo, UV + time * vec2(scroll_speed, 0.0)) * COLOR;
	ALBEDO = col.rgb * intensity;
}

