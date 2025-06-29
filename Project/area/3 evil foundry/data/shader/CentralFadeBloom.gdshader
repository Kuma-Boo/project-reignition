shader_type spatial;
render_mode blend_add, unshaded, fog_disabled;
uniform vec4 albedo : source_color;

uniform float rim_bias : hint_range(0, 1) = .5;

uniform float distance_fade_min;
uniform float distance_fade_max;


void fragment() {
	float view = dot(NORMAL, VIEW);
	float rim = (view - rim_bias);
	
	float distance_fade = clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
	
	ALBEDO = albedo.rgb * COLOR.rgb * rim * distance_fade;
	ALBEDO = vec3(clamp(ALBEDO.r, 0, 1), clamp(ALBEDO.g, 0, 1), clamp(ALBEDO.b, 0, 1));
}