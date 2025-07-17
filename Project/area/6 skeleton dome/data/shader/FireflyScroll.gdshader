shader_type spatial;
render_mode blend_add, specular_disabled;

global uniform float time;

uniform float distance_fade_min : hint_range(0.0, 4096.0, 0.01);
uniform float distance_fade_max : hint_range(0.0, 4096.0, 0.01);

uniform float scroll_speed = 0.0;
uniform sampler2D albedo : source_color;

uniform float emission_strength;

void fragment()
{
	vec2 uv = UV + vec2(time * scroll_speed, 0);
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
	ALPHA *= clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
	
	SPECULAR = 0.0;
	EMISSION = col.rgb * emission_strength;
}
