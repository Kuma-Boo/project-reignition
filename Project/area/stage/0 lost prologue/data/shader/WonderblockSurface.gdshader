shader_type spatial;
render_mode unshaded, blend_add, specular_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform sampler2D distortion : hint_default_black;
uniform float scroll_speed;
uniform float distortion_strength : hint_range(0.0, 1.0) = .5;
uniform float distortion_scale : hint_range(0.0, 5.0) = .5;

void fragment()
{
	vec2 uv = UV + vec2(1, 0) * scroll_speed * time;
	vec2 distortion_uv = UV - vec2(1, 0) * scroll_speed * time;
	uv += texture(distortion, distortion_uv * distortion_scale).rg * (.5 - distortion_strength);
	vec4 col = texture(albedo, uv);
	ALBEDO = clamp(col.rgb * 20.0, 0.0, 1.0);
}
