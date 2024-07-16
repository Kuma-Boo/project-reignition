shader_type canvas_item;

global uniform float time;

uniform sampler2D lut;
uniform sampler2D screen_texture : hint_screen_texture;
uniform int iterations;
uniform float rotation_step : hint_range(0.0, 0.1, 0.01) = .1;
uniform float rotation_speed : hint_range(0.0, 5.0, 0.1) = 2.0;
uniform float glow_strength : hint_range(0.0, 2.0, 0.1) = 1.0;
uniform float opacity : hint_range(0.0, 1.0, 0.1) = .1;
uniform float brightness : hint_range(0.0, 2.0, 0.1) = 1.0;
uniform float contrast : hint_range(0.0, 2.0, 0.1) = 1.0;
uniform float saturation : hint_range(0.0, 2.0, 0.1) = 1.0;

vec2 rotate(vec2 uv, vec2 pivot, float angle)
{
	mat2 rotation = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
	uv -= pivot;
	uv = uv * rotation;
	uv += pivot;
	return uv;
}

void fragment()
{
	vec3 screen_col = texture(screen_texture, SCREEN_UV).rgb;
	vec3 break_col = screen_col.rgb;
	
	break_col = mix(vec3(0.0), break_col, brightness);
	break_col = mix(vec3(0.5), break_col, contrast);
	float average = (break_col.r + break_col.g + break_col.b) / 3.0f;
	break_col = mix(vec3(average), break_col, saturation);
	
	float layer_factor = (glow_strength  * (length(vec2(0.5) - UV) * 1.5)) / float(iterations);
	for (int i = 0; i < iterations; i++)
	{
		float rotation_amount = float(i) * rotation_step + time * rotation_speed;
		vec2 uv_right = rotate(UV, vec2(0.5), rotation_amount);
		vec2 uv_left = rotate(UV, vec2(0.5), -rotation_amount);
		break_col.rgb += texture(lut, uv_right).rgb * layer_factor;
		break_col.rgb += texture(lut, uv_left).rgb * layer_factor;
	}
	
	COLOR.rgb = mix(screen_col, break_col, opacity);
}
