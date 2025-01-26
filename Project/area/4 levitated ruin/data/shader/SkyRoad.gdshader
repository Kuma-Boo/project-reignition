shader_type spatial;
render_mode blend_add, unshaded, specular_disabled, cull_disabled;

instance uniform float path_ratio : hint_range(0.0, 1.0) = 1.0;
instance uniform float path_length = 1000.0;

uniform float glow_intensity = 5.0;
uniform sampler2D center_gradient : hint_default_white;

const float distance_fade_min = 0.1;
const float distance_fade_max = 1.0;
const float start_smoothing = 10.0;
const float end_smoothing = 5.0;

void fragment()
{
	float distance_fade = clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
	float distant_transparency = clamp(smoothstep(100.0, 50.0, length(VERTEX)), 0.0, 1.0);
	distant_transparency = mix(abs(mod(UV.y, 0.2) - 0.1), 1.0, distant_transparency);
	
	float detail = texture(center_gradient, vec2(UV.y, 0)).r;
	ALBEDO = COLOR.rgb * glow_intensity * distant_transparency * detail * COLOR.a;
	
	float scroll_amount = (path_ratio * path_length) - UV.x;
	scroll_amount = clamp(scroll_amount / end_smoothing, 0.0, glow_intensity);
	float start_blend = clamp(UV.x / start_smoothing, 0.0, 1.0);
	ALPHA *= scroll_amount * start_blend * distance_fade;
}
