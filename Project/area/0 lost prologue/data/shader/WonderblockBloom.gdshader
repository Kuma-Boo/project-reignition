shader_type spatial;
render_mode unshaded;

const float distance_fade_min = 2.0;
const float distance_fade_max = 80.0;
const float grow_scale = 2.0;
const float offset_bias = 3.0;
const float fade_offset = -8.0;

void vertex()
{
	float fade_distance = length((VIEW_MATRIX * MODEL_MATRIX[3])) + offset_bias;
	float distance_fac = clamp(smoothstep(distance_fade_min, distance_fade_max, fade_distance), 0.0, 1.0);
	VERTEX -= normalize(NORMAL) * (distance_fac * grow_scale);
}

void fragment()
{
	float fade_distance = length((VIEW_MATRIX * MODEL_MATRIX[3])) + fade_offset;
	float fade = 1.0 - clamp(smoothstep(distance_fade_min, distance_fade_max, fade_distance), 0.0, 1.0);
	ALBEDO = vec3(0.0);
	ALPHA *= fade;
}
