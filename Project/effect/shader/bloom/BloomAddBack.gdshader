shader_type spatial;
render_mode depth_draw_never, blend_add, cull_disabled;

global uniform float bloom_amount;
uniform vec2 direction;
const float distance_fade_min = 50.0;
const float distance_fade_max = -5.0;
uniform float grow_direction = 1.0;

void vertex()
{
	vec3 vert = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float distance_fac = clamp(smoothstep(distance_fade_max, distance_fade_min, -vert.z), 0.05, 1.0);
	vec4 offset = INV_VIEW_MATRIX * vec4(normalize(direction) * bloom_amount * distance_fac, 0.0, 0.0);
	VERTEX += (normalize(NORMAL) * bloom_amount * distance_fac + offset.xyz) * grow_direction;
}

void fragment()
{
	ALBEDO = COLOR.rgb; //Use vertex color
	EMISSION = COLOR.rgb;
	ALPHA = COLOR.a * .5;
	ALPHA *= clamp(smoothstep(distance_fade_min, distance_fade_max, -VERTEX.z), 0.0, 1.0);
}