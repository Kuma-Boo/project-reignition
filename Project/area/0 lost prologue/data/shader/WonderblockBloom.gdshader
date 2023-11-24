shader_type spatial;
render_mode unshaded;

const float distance_fade_min = 80.0;
const float distance_fade_max = 5.0;
const float grow_bias = 2.0;
const float offset_bias = -.05;

void vertex()
{
	vec3 vert = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float distance_fac = clamp(smoothstep(distance_fade_max, distance_fade_min, -vert.z), 0.0, 1.0);
	VERTEX -= normalize(NORMAL) * (distance_fac * grow_bias + offset_bias);
}

void fragment()
{
	float distance_fac = 1.0 - clamp(smoothstep(distance_fade_max, distance_fade_min, -VERTEX.z), 0.0, 1.0);
	ALBEDO = vec3(.5);//mix(vec3(.5), COLOR.rgb, distance_fac); //Use vertex color
}
