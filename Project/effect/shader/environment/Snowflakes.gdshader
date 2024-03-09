shader_type spatial;
render_mode unshaded, blend_add;

global uniform float time;

uniform float intensity = 2.0;
uniform float scroll_speed = 0.0;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec2 uv = UV + vec2(time * scroll_speed, 0);
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb * intensity;
}

