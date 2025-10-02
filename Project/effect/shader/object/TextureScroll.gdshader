shader_type spatial;
render_mode diffuse_burley, specular_disabled, depth_draw_opaque;

global uniform float time;

uniform vec2 scroll_direction = vec2(0, 1.0);

uniform float scroll_speed = 0.0;
uniform sampler2D albedo : source_color;
uniform float distance_fade_min : hint_range(0.0, 4096.0, 0.01);
uniform float distance_fade_max : hint_range(0.0, 4096.0, 0.01);

void fragment()
{
	vec2 uv = UV + scroll_direction * time * scroll_speed;
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = .99;
	SPECULAR = 0.0;

	{
		// Distance Fade: Pixel Dither
		float fade_distance = length(VERTEX);

		// Use interleaved gradient noise, which is fast but still looks good.
		const vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
		float fade = clamp(smoothstep(distance_fade_min, distance_fade_max, fade_distance), 0.0, 1.0);
		// Use a hard cap to prevent a few stray pixels from remaining when past the fade-out distance.
		if (fade < 0.001 || fade < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
			discard;
		}
	}
}
