shader_type spatial;
render_mode blend_add;

uniform sampler2D albedo : source_color;
uniform float main_speed : hint_range(0, 1) = .28;
uniform float alpha : hint_range(0, 1) = .6;
uniform float secondary_speed = 4.0;
uniform float sway_amount = .4;

void fragment()
{
	vec2 main_uv = UV + vec2(1, 0.5) * TIME * main_speed;
	vec2 second_uv = UV + vec2(TIME * -main_speed, 0.5 * sin(TIME * secondary_speed) * sway_amount);
	vec4 col = texture(albedo, main_uv);
	col += texture(albedo, second_uv);
	col = clamp(col, 0, 1);
	col *= COLOR;
	
	
	ALBEDO = col.rgb;
	ALPHA = col.a * alpha;
}
