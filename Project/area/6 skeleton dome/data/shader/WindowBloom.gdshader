shader_type spatial;
render_mode blend_add;

uniform sampler2D albedo : source_color;
uniform float emission_strength;
uniform float distance_fade_min : hint_range(0.0, 4096.0, 0.01);
uniform float distance_fade_max : hint_range(0.0, 4096.0, 0.01);

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	EMISSION = col.rgb * emission_strength;
	ALPHA *= clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
}
