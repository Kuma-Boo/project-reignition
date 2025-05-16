shader_type spatial;
render_mode diffuse_burley, specular_disabled;

global uniform float time;

uniform float timing_offset;
uniform vec2 uv_offset;
uniform vec2 scroll_speed;
uniform vec2 scroll_magnitude;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec2 scroll_offset = vec2(time * scroll_speed.x, sin(time * scroll_speed.y + timing_offset));
	scroll_offset.y *= scroll_magnitude.y;
	vec2 uv = UV + uv_offset + scroll_offset;
	uv.y = clamp(uv.y, 0, 1);
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
