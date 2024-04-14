shader_type spatial;
render_mode cull_disabled, blend_add;

global uniform float time;

uniform float scroll_speed : hint_range(0.0, 1.0, 0.1) = 0.0;
uniform sampler2D albedo : source_color;
uniform float glow_strength = 8.0;

uniform float bloom_amount;
const float DISTANCE_MIN = 50.0;
const float DISTANCE_MAX = -5.0;

varying float distance_fac;


void vertex()
{
	vec3 vert = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	distance_fac = clamp(smoothstep(DISTANCE_MAX, DISTANCE_MIN, -vert.z), 0.05, 1.0);
	VERTEX += NORMAL * bloom_amount * distance_fac;
}


void fragment()
{
	vec2 uv = UV * vec2(0.5, 1.0) + time * vec2(scroll_speed, 0.0);
	vec4 col = texture(albedo, uv) * COLOR;
	vec4 emission_col = textureLod(albedo, uv, 2.0) * COLOR;
	SPECULAR = 0.0;
	ALBEDO = col.rgb * glow_strength;
	EMISSION = emission_col.rgb * glow_strength;
	ALPHA *= col.a * (1.0 - distance_fac);
}

