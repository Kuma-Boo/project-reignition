shader_type spatial;
render_mode specular_disabled;

uniform sampler2D albedo : source_color;

uniform float emission_fade_start : hint_range(0.0, 4096.0, 0.01);

uniform sampler2D texture_emission : source_color, hint_default_black;
uniform vec4 emission : source_color;
uniform float emission_energy : hint_range(0.0, 100.0, 0.01);

void fragment() {
	vec2 base_uv = UV;

	vec4 col = texture(albedo, base_uv) * COLOR;
	ALBEDO = col.rgb;

	// Emission: Enabled
	vec3 emission_tex = texture(texture_emission, base_uv).rgb;
	// Emission Operator: Add
	EMISSION = (emission.rgb + emission_tex) * emission_energy;
	EMISSION *= 1.0 - clamp(smoothstep(0, emission_fade_start, length(VERTEX)), 0.0, 1.0);
}
