shader_type spatial;
render_mode unshaded, blend_add;

uniform float uv_offset;
uniform sampler2D albedo : hint_default_black;

void fragment() {
	vec4 col = texture(albedo, UV + vec2(uv_offset, 0)) * COLOR;
	ALBEDO = col.rgb;
}
