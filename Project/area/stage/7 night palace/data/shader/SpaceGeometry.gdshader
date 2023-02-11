shader_type spatial;
render_mode blend_add, cull_disabled;

global uniform vec3 player_position;
uniform sampler2D albedo : source_color;

const float INTENSITY = 1.2;
const float GLOW_SIZE = 1.0;
const float GLOW_INTENSITY = .1;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	
	vec4 vert = INV_VIEW_MATRIX * vec4(VERTEX, 1.0);
	float fac = (1.0 - distance(player_position, vert.xyz) / GLOW_SIZE);
	fac = clamp(fac, 0.0, 1.0);
	col.rgb += vec3(GLOW_INTENSITY) * fac;
	SPECULAR += fac;
	ALBEDO = col.rgb * INTENSITY;
	EMISSION = col.rgb * INTENSITY;
}