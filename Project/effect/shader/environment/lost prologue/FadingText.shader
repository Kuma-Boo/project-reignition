shader_type spatial;
render_mode specular_disabled, depth_draw_alpha_prepass;

uniform sampler2D albedo : hint_albedo;
uniform sampler2D text : hint_albedo;
uniform float roughness : hint_range(0, 1);

void fragment() {
	vec4 albedo_tex = texture(albedo, UV);
	vec4 text_tex = texture(text, UV);
	
	float textFade = clamp(smoothstep(6.0, 12.0, -VERTEX.z), 0.0, 1.0);
	ALBEDO = mix(albedo_tex.rgb, text_tex.rgb, textFade) * COLOR.rgb;
	ROUGHNESS = roughness;
	ALPHA = albedo_tex.a;
	ALPHA_SCISSOR = .1;
}
