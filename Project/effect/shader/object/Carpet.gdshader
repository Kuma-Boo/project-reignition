shader_type spatial;
render_mode specular_disabled;

uniform sampler2D albedo : source_color;

global uniform vec3 player_position;
global uniform float time;

uniform float player_height_influence_limit = .3;
uniform float player_distance_influence_limit = .3;
uniform float displacement_strength = .05;
uniform float displacement_speed = 5.0;

instance uniform float bounce_displacement_strength : hint_range(0.0, 1.0, 0.1) = 0;
instance uniform float bounce_phase;
instance uniform float bounce_blend : hint_range(0.0, 1.0, 0.1) = 0;

void vertex()
{
	vec4 global_position = MODEL_MATRIX * vec4(VERTEX, 1.0);
	float idle_amount = sin(time * displacement_speed) * displacement_strength;
	float player_height_influence = player_position.y - global_position.y;
	player_height_influence = clamp(abs(player_height_influence - player_height_influence_limit), -player_height_influence_limit, player_height_influence_limit);
	player_height_influence -= player_height_influence_limit;
	
	float player_distance_influence = clamp(length(player_position.xz - global_position.xz), -player_distance_influence_limit, player_distance_influence_limit);
	player_distance_influence /= player_distance_influence_limit;
	player_distance_influence = 1.0 - player_distance_influence;
	
	float bounce_amount = COLOR.r * sin(bounce_phase) * bounce_displacement_strength;
	VERTEX.y += sign(COLOR.r) * mix(idle_amount, bounce_amount, bounce_blend);
	VERTEX.y += player_height_influence * player_distance_influence;
}

void fragment()
{
	ALBEDO = texture(albedo, UV).rgb * COLOR.rgb;
	SPECULAR = 0.0;
}
