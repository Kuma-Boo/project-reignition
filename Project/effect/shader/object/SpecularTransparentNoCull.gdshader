shader_type spatial;
render_mode diffuse_burley, cull_disabled, depth_prepass_alpha, depth_draw_opaque;

uniform sampler2D albedo : hint_default_black;
uniform sampler2D specular : hint_default_black;
uniform float roughness : hint_range(0, 1) = .8;

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	SPECULAR = texture(specular, UV).r;
	ROUGHNESS = roughness;
	ALBEDO = col.rgb;
	ALPHA *= col.a;
}
