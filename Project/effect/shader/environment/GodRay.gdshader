shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, fog_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform vec2 scroll_vector = vec2(0, 1);
uniform float scroll_speed = 0.1;

void fragment()
{
	vec4 col = texture(albedo, UV + scroll_vector * time * scroll_speed);
	col += texture(albedo, UV + scroll_vector * time * -scroll_speed);
	ALBEDO = col.rgb * COLOR.rgb;
	ALPHA *= clamp(smoothstep(0, 5, length(VERTEX)), 0.0, 1.0);
}
