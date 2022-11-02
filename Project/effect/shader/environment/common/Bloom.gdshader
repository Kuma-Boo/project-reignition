/*
Adds the secondary viewport's bloom on top of the original image.
*/
shader_type spatial;
render_mode blend_add;

uniform vec4 color : source_color;
uniform float intensity = 1.0;
uniform float base_size : hint_range(0.0, 5.0) = 1.0;
const float fade_distance = 10.0;
varying float dst; //How far this is from the camera

void vertex()
{
	//Calculate distance
	vec4 view_space_vertex = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	dst = clamp(smoothstep(fade_distance, 0.0, -view_space_vertex.z), 0.0, 1.0);
	
	VERTEX += normalize(NORMAL) * base_size * (1.0 - dst);
}

void fragment()
{
	vec4 col = color * COLOR;
	
	EMISSION = col.rgb * intensity;
	ALPHA = col.a * dst;
}