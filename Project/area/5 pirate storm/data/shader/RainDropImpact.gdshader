shader_type spatial;
render_mode specular_disabled, unshaded;

global uniform float environment_fx_intensity;

uniform sampler2D texture_albedo : source_color, filter_linear_mipmap;

uniform int particles_anim_h_frames : hint_range(1, 128);
uniform int particles_anim_v_frames : hint_range(1, 128);

void vertex() {
	float h_frames = float(particles_anim_h_frames);
	float v_frames = float(particles_anim_v_frames);
	float particle_total_frames = float(particles_anim_h_frames * particles_anim_v_frames);
	float particle_frame = floor(INSTANCE_CUSTOM.z * float(particle_total_frames));
	particle_frame = clamp(particle_frame, 0.0, particle_total_frames - 1.0);
	UV /= vec2(h_frames, v_frames);
	UV += vec2(mod(particle_frame, h_frames) / h_frames, floor((particle_frame + 0.5) / h_frames) / v_frames);
}

void fragment() {
	vec4 albedo_tex = texture(texture_albedo, UV) * COLOR;
	ALBEDO = albedo_tex.rgb;
	ALPHA *= albedo_tex.a * environment_fx_intensity;
}
