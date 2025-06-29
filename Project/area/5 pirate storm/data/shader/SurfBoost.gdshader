shader_type spatial;
render_mode blend_add, specular_disabled, unshaded, fog_disabled;

uniform sampler2D albedo : source_color;

void vertex() {
	mat4 mat_world = mat4(
		vec4(cos(INSTANCE_CUSTOM.x), -sin(INSTANCE_CUSTOM.x), 0.0, 0.0),
		vec4(sin(INSTANCE_CUSTOM.x), cos(INSTANCE_CUSTOM.x), 0.0, 0.0),
		vec4(0.0, 0.0, 1.0, 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));

	MODELVIEW_MATRIX *= mat_world;
}

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	ALPHA *= col.a;
}
