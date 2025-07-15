shader_type spatial;
render_mode blend_add, specular_disabled;

uniform sampler2D albedo : source_color;

uniform float distance_fade_min : hint_range(0.0, 4096.0, 0.01);
uniform float distance_fade_max : hint_range(0.0, 4096.0, 0.01);

uniform float emission_intensity = 2.0;
uniform vec2 uv_offset;

uniform int particles_anim_h_frames : hint_range(1, 128);
uniform int particles_anim_v_frames : hint_range(1, 128);
uniform bool particles_anim_loop;

void vertex() {
	// Billboard
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(normalize(cross(MODEL_MATRIX[1].xyz, INV_VIEW_MATRIX[2].xyz)), 0.0), normalize(MODEL_MATRIX[1]), MODEL_MATRIX[2], MODEL_MATRIX[3]);
	
	// Particle Scaling
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	
	float h_frames = float(particles_anim_h_frames);
	float v_frames = float(particles_anim_v_frames);
	float particle_total_frames = float(particles_anim_h_frames * particles_anim_v_frames);
	float particle_frame = floor(INSTANCE_CUSTOM.z * float(particle_total_frames));
	if (!particles_anim_loop) {
		particle_frame = clamp(particle_frame, 0.0, particle_total_frames - 1.0);
	} else {
		particle_frame = mod(particle_frame, particle_total_frames);
	}
	
	UV += uv_offset;
	UV /= vec2(h_frames, v_frames);
	UV += vec2(mod(particle_frame, h_frames) / h_frames, floor((particle_frame + 0.5) / h_frames) / v_frames);
}

void fragment() {
	vec4 col = texture(albedo, UV);
	col += textureLod(albedo, UV, 2);

	// Vertex Color Use as Albedo: Enabled
	col *= COLOR;

	ALBEDO = col.rgb;
	ALPHA *= col.a;
	// Distance Fade: Pixel Alpha
	EMISSION = col.rgb * emission_intensity * col.a;
	ALPHA *= clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
}
