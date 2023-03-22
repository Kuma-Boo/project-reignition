shader_type spatial;
render_mode blend_add, shadows_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform float main_speed : hint_range(0, 1) = .28;
uniform float secondary_speed = 4.0;
uniform float sway_amount = .4;

void fragment()
{
	vec2 main_uv = UV + vec2(1, 0.5) * time * main_speed;
	vec2 second_uv = UV + vec2(time * -main_speed, 0.5 * sin(time * secondary_speed) * sway_amount);
	vec4 col = texture(albedo, main_uv);
	col += texture(albedo, second_uv);
	col = clamp(col, 0, 1) * COLOR;
	
	ALBEDO = col.rgb;
	SPECULAR = col.a;
	ALPHA = col.a;
}
