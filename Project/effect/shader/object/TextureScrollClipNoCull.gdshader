shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_disabled;

global uniform float time;

uniform vec2 scroll_direction = vec2(0, 1);
uniform float scroll_speed = 0.0;
uniform float alpha_scissor_threshold : hint_range(0.0, 1.0, 0.1) = 0.5;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec2 uv = UV + scroll_direction * time * scroll_speed;
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_scissor_threshold;
}
