shader_type spatial;
render_mode blend_add, cull_disabled, unshaded;

global uniform vec3 player_position;
uniform sampler2D albedo : source_color;
uniform float brightness;

const float INTENSITY = 2.0;
const float GLOW_SIZE = 1.0;
const float GLOW_INTENSITY = 0.4;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	
	vec4 vert = INV_VIEW_MATRIX * vec4(VERTEX, 1.0);
	float glow_factor = (1.0 - distance(player_position, vert.xyz) / GLOW_SIZE);
	glow_factor = clamp(glow_factor, 0.0, 1.0);
	
	col.rgb += vec3(GLOW_INTENSITY) * glow_factor;
	ALBEDO = col.rgb * (INTENSITY + brightness);
}