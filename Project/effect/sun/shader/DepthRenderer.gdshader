shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never;

void vertex() {
    POSITION = vec4(VERTEX, 1.0);
}

void fragment() {
	float depth = texture(DEPTH_TEXTURE, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
	vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	view.xyz /= view.w;
	float linear_depth = -view.z / 4000.0;
	ALBEDO = vec3(linear_depth);
}