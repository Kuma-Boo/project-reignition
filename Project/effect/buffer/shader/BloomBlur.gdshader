shader_type canvas_item;

uniform vec2 blur_direction;
const float blur_strength = 12.0;
const int blur_iterations = 8;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;

vec4 blur(vec2 screen_uv, vec2 pixel_size)
{
	vec4 col = vec4(0, 0, 0, 0);
	
	for (int i = 0; i < blur_iterations; i++)
	{
		vec2 uv = screen_uv + blur_direction * pixel_size * blur_strength * float(i);
		col += texture(screen_tex, uv);
		
		uv = screen_uv - blur_direction * pixel_size * blur_strength * float(i);
		col += texture(screen_tex, uv);
	}
	
	col /= float(blur_iterations * 2);
	
	return col;
}

void fragment()
{
	vec4 blurred_col = blur(SCREEN_UV, SCREEN_PIXEL_SIZE);
	COLOR = blurred_col;
}