shader_type spatial;
render_mode blend_add, unshaded;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform float scroll_speed : hint_range(0, 1) = .28;

void fragment()
{
	vec2 main_uv = UV - vec2(1, 0.5) * time * scroll_speed;
	vec4 col = texture(albedo, main_uv) * COLOR;
	ALBEDO = col.rgb;
}
