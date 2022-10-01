shader_type spatial;
render_mode depth_draw_opaque;

uniform sampler2D albedo : source_color;
uniform sampler2D detail : source_color;

void fragment() {
	vec4 albedo_tex = texture(albedo, UV);
	vec4 text_tex = texture(detail, UV);
	
	float textFade = clamp(smoothstep(6.0, 12.0, -VERTEX.z), 0.0, 1.0);
	ALBEDO = mix(albedo_tex.rgb, text_tex.rgb, textFade) * COLOR.rgb;
	ROUGHNESS = 1.0;
	ALPHA = albedo_tex.a;
	ALPHA_SCISSOR_THRESHOLD = .1;
}
