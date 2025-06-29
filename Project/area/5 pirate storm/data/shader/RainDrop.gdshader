shader_type spatial;
render_mode specular_disabled, fog_disabled, unshaded, cull_disabled;

global uniform float environment_fx_intensity;

uniform sampler2D albedo : source_color;
uniform vec4 tint : source_color;

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR * tint;
	ALBEDO = col.rgb;
	ALPHA *= col.a * environment_fx_intensity;
}
