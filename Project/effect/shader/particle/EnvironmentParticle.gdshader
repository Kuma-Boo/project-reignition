shader_type spatial;
render_mode specular_disabled, fog_disabled, unshaded, cull_disabled;

global uniform float environment_fx_intensity;

uniform sampler2D albedo : source_color;
uniform vec4 tint : source_color;
uniform float distance_fade_min : hint_range(0.0, 4096.0, 0.01);
uniform float distance_fade_max : hint_range(0.0, 4096.0, 0.01);

void fragment() {
	vec4 col = texture(albedo, UV) * COLOR * tint;
	ALBEDO = col.rgb;
	ALPHA *= col.a * environment_fx_intensity;
	ALPHA *= clamp(smoothstep(distance_fade_min, distance_fade_max, length(VERTEX)), 0.0, 1.0);
}
