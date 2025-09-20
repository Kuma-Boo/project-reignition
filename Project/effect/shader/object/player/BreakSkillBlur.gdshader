shader_type canvas_item;

uniform float blur_power : hint_range(0.0, 1.0) = 0.01;
uniform float max_blur_power : hint_range(0.0, 1.0) = 0.01;
uniform float blur_clarity : hint_range(1.0, 5.0, 1.0) = 1.0;
uniform int sampling_count : hint_range(1, 64) = 6;
uniform sampler2D screen_texture : hint_screen_texture;

void fragment()
{
	vec2 direction = SCREEN_UV - vec2(0.5);
	float power = pow(length(direction), blur_clarity) * blur_power;
	power = min(power, max_blur_power);
	
	vec3 c = vec3(0.0, 0.0, 0.0);
	float f = 1.0 / float(sampling_count);
	for(int i = 0; i < sampling_count; i++)
	{
		c += texture(screen_texture, SCREEN_UV - power * direction * float(i)).rgb * f;
	}
	COLOR.rgb = c;
}