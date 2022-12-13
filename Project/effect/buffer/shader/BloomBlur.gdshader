shader_type canvas_item;

uniform vec2 blur_direction;
const float blur_strength = 12.0;
const int blur_iterations = 8;

vec4 blur(sampler2D screen_tex, vec2 screen_uv, vec2 pixel_size)
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
	vec4 blurred_col = blur(SCREEN_TEXTURE, SCREEN_UV, SCREEN_PIXEL_SIZE);
	COLOR = blurred_col;
}