shader_type spatial;
render_mode cull_disabled, blend_add;

global uniform float time;

uniform float scroll_speed : hint_range(0.0, 1.0, 0.1) = 0.0;
uniform sampler2D albedo : source_color;
const float GLOW_STRENGTH = 8.0;

void fragment()
{
	vec2 uv = UV * vec2(0.5, 1.0) + time * vec2(scroll_speed, 0.0);
	vec4 col = texture(albedo, uv) * COLOR;
	vec4 emission_col = textureLod(albedo, uv, 2.0) * COLOR;
	SPECULAR = 0.0;
	ALBEDO = col.rgb * GLOW_STRENGTH;
	EMISSION = emission_col.rgb * GLOW_STRENGTH;
}

