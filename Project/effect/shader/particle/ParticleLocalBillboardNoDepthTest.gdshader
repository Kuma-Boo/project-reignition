shader_type spatial;
render_mode blend_add, unshaded, depth_test_disabled;

uniform sampler2D albedo : source_color;

uniform bool particles_anim_loop;
uniform int particles_anim_h_frames;
uniform int particles_anim_v_frames;


void vertex() {
	// Billboard
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(normalize(cross(MODEL_MATRIX[1].xyz, INV_VIEW_MATRIX[2].xyz)), 0.0), normalize(MODEL_MATRIX[1]), MODEL_MATRIX[2], MODEL_MATRIX[3]);
	
	// Particle Scaling
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	
	// Texture animation
	float h_frames = float(particles_anim_h_frames);
	float v_frames = float(particles_anim_v_frames);
	float particle_total_frames = float(particles_anim_h_frames * particles_anim_v_frames);
	float particle_frame = floor(INSTANCE_CUSTOM.z * float(particle_total_frames));
	particle_frame = mod(particle_frame, particle_total_frames);
	if (!particles_anim_loop) {
		particle_frame = clamp(particle_frame, 0.0, particle_total_frames - 1.0);
	} else {
		particle_frame = mod(particle_frame, particle_total_frames);
	}
	UV /= vec2(h_frames, v_frames);
	UV += vec2(mod(particle_frame, h_frames) / h_frames, floor((particle_frame + 0.5) / h_frames) / v_frames);
}


void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
