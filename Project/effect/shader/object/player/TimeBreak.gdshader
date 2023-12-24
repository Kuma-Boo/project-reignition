shader_type canvas_item;
render_mode blend_add;

global uniform float time;

uniform sampler2D lut;
uniform sampler2D screen_texture : hint_screen_texture;
uniform int iterations;
uniform float rotation_step : hint_range(0.0, 0.1, 0.01) = .1;
uniform float rotation_speed : hint_range(0.0, 5.0, 0.1) = 2.0;
uniform float opacity : hint_range(0.0, 1.0, 0.1) = .1;

vec2 rotate(vec2 uv, float angle)
{
	mat2 rotation = mat2(vec2(sin(angle), -cos(angle)), vec2(cos(angle), sin(angle)));
	
	uv -= vec2(0.5);
	uv = uv * rotation;
	uv += vec2(0.5);
	return uv;
}


void fragment()
{
	float layer_factor = (opacity * .5) / float(iterations);
	vec2 screen_uv = FRAGCOORD.xy / (1.0 / SCREEN_PIXEL_SIZE).xy;
	vec3 col = texture(screen_texture, screen_uv).rgb;
	vec2 uv = UV * vec2(1.0, .6) + vec2(0.0, 0.15);
	for (int i = 0; i < iterations; i++)
	{
		float rotation_amount = float(i) * rotation_step + time * rotation_speed;
		COLOR.a += (1.0 - texture(lut, rotate(uv, rotation_amount)).a) * layer_factor;
		COLOR.a += (1.0 - texture(lut, rotate(uv, -rotation_amount)).a) * layer_factor;
	}
}
