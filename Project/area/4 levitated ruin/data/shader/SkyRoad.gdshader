shader_type spatial;
render_mode blend_add, unshaded, specular_disabled, cull_disabled;

instance uniform float path_ratio : hint_range(0.0, 1.0) = 1.0;
instance uniform float path_length = 1000.0;

uniform float glow_intensity = 5.0;

const float distance_fade_min = 0.1;
const float distance_fade_max = 2.0;
const float end_smoothness = 0.1;

void fragment()
{
	float distance_fade = clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
	float distant_transparency = clamp(smoothstep(100.0, 50.0, length(VERTEX)), 0.0, 1.0);
	distant_transparency = mix(abs(mod(UV.y, 0.2) - 0.1), 1.0, distant_transparency);
	
	ALBEDO = COLOR.rgb * glow_intensity * distant_transparency * COLOR.a;
	
	float scroll_amount = (path_ratio * path_length) - UV.x;
	float smoothing = mix(end_smoothness, 2.0, path_ratio);
	ALPHA *= clamp(scroll_amount * smoothing, 0.0, 1.0) * distance_fade;
}
