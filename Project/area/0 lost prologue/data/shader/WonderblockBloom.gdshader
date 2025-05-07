shader_type spatial;
render_mode unshaded, blend_mul;

instance uniform vec3 block_size;
const float distance_fade_min = 80.0;
const float distance_fade_max = 0.0;
const float grow_bias = 100.0;
const float offset_bias = 0.1;
const float blur_distance = 0.1;

varying vec3 vertex_position;
varying vec3 reference_position;

void vertex()
{
	vec3 vert = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float distance_fac = clamp(smoothstep(distance_fade_max, distance_fade_min, -vert.z), 0.0, 1.0);
	VERTEX -= normalize(NORMAL) * (distance_fac * grow_bias + offset_bias);
	
	vertex_position = VERTEX;
	reference_position = VERTEX - normalize(NORMAL) * blur_distance;
}

const float bias = -2.0;

void fragment()
{
	vec3 world_camera = (INV_VIEW_MATRIX * vec4(vec3(0.0), 1.0)).xyz;
	vec4 a = INV_VIEW_MATRIX * vec4(VERTEX, 1.0);
	
	float dst = 1.0 - clamp(distance(a.xyz, world_camera) + bias, 0.0, 1.0);
	
	/*
	float distance_fac = 1.0 - clamp(smoothstep(distance_fade_max, distance_fade_min, -VERTEX.z), 0.0, 1.0);
	vec3 dst = (abs(vertex_position) - block_size) / -blur_distance;
	float fade = length(dst);
	fade = clamp(fade, 0.0, 1.0);
	*/
	
	ALBEDO = vec3(dst);//COLOR.rgb;// + vec3(1.0 - (distance_fac * fade)); //Use vertex color
}
