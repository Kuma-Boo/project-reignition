shader_type spatial;
render_mode blend_add, unshaded, world_vertex_coords;

uniform vec4 albedo : source_color;
uniform sampler2D texture_albedo : source_color;
uniform float distance_fade_min;
uniform float distance_fade_max;
uniform float clipping_offset;

const float CAMERA_DISTANCE_LIMIT = 0.1;

void vertex() {
	vec3 cam_dir = CAMERA_POSITION_WORLD - VERTEX;
	float cam_dst = length(cam_dir);
	VERTEX += min(clipping_offset, cam_dst - CAMERA_DISTANCE_LIMIT) * normalize(cam_dir);
}

void fragment() {
	vec2 base_uv = UV;
	vec4 albedo_tex = texture(texture_albedo,base_uv);
	albedo_tex *= COLOR;
	ALBEDO = albedo.rgb * albedo_tex.rgb;
	ALPHA *= albedo.a * albedo_tex.a;
	ALPHA *= 1.0 - clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
}
