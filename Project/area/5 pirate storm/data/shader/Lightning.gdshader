shader_type spatial;
render_mode specular_disabled, unshaded;

uniform sampler2D base : source_color;
uniform sampler2D albedo : source_color;

void fragment() {
	vec4 col = texture(albedo, UV2) * COLOR;
	vec4 base_col = texture(base, UV);
	col.a *= base_col.a;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
