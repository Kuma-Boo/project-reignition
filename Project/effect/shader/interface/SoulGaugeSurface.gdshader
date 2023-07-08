shader_type canvas_item;

global uniform float time;

uniform sampler2D albedo : source_color, repeat_enable;
uniform int particles_anim_h_frames : hint_range(0, 10, 1) = 5;
uniform int particles_anim_v_frames : hint_range(0, 10, 1) = 4;
uniform int animation_framerate : hint_range(0, 60, 1) = 60;
uniform float intensity = 1.0;

void vertex()
{
	float h_frames = float(particles_anim_h_frames);
	float v_frames = float(particles_anim_v_frames);
	float particle_total_frames = float(particles_anim_h_frames * particles_anim_v_frames);
	float particle_frame = floor(TIME * float(animation_framerate));
	particle_frame = mod(particle_frame, particle_total_frames);
	UV /= vec2(h_frames, v_frames);
	UV += vec2(mod(particle_frame, h_frames) / h_frames, floor((particle_frame + 0.5) / h_frames) / v_frames);
}

void fragment()
{
	COLOR = texture(albedo, UV);
	COLOR += texture(albedo, -UV);
	COLOR.a *= intensity;
}
