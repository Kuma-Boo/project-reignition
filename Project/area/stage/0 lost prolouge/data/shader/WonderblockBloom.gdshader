shader_type spatial;
render_mode unshaded, depth_draw_never, blend_mix, cull_back;

global uniform float bloom_amount;

uniform vec3 direction;
const float distance_fade_min = 80.0;
const float distance_fade_max = -5.0;
const float grow_bias = .1;

void vertex()
{
	vec3 vert = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float distance_fac = clamp(smoothstep(distance_fade_max, distance_fade_min, -vert.z), 0.05, 1.0);
	vec4 offset = INV_VIEW_MATRIX * vec4(normalize(direction) * bloom_amount * distance_fac, 0.0);
	VERTEX -= (normalize(NORMAL) * (bloom_amount * distance_fac - grow_bias) + offset.xyz);
}

void fragment()
{
	ALBEDO = COLOR.rgb; //Use vertex color
	ALPHA = COLOR.a;
	ALPHA *= clamp(smoothstep(distance_fade_min, distance_fade_max, -VERTEX.z), 0.0, 1.0);
}