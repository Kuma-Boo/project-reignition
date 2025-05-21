shader_type spatial;
render_mode specular_disabled, unshaded, fog_disabled, cull_disabled;

global uniform float environment_fx_intensity;

uniform sampler2D albedo : source_color, filter_linear_mipmap;


void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	ALPHA *= col.a * environment_fx_intensity;
}
