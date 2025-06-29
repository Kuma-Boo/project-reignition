shader_type spatial;
render_mode blend_add, specular_disabled;

global uniform float time;

uniform vec4 background_color : source_color;
uniform sampler2D albedo : source_color;
uniform sampler2D distortion : hint_default_black;
uniform sampler2D screen_texture : hint_screen_texture;

uniform float scroll_speed;
uniform float distortion_strength : hint_range(0.0, 1.0) = .5;
uniform float distortion_scale : hint_range(0.0, 5.0) = .5;

void fragment()
{
	// Get the screen texture
	vec4 col = background_color + texture(screen_texture, SCREEN_UV) * COLOR;

	// Distort base UV
	vec2 uv = UV + vec2(1, 0) * scroll_speed * time;
	vec2 distorted_uv = UV - vec2(1, 0) * scroll_speed * time;
	uv += texture(distortion, distorted_uv * distortion_scale).rg * (.5 - distortion_strength);
	col += texture(albedo, uv);

	ALBEDO = col.rgb;
	EMISSION = texture(albedo, uv).rgb * 3.0;
}
