shader_type canvas_item;

uniform float blur_amount : hint_range(0.0, 5.0) = 4.0;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void fragment()
{
	vec4 color = texture(SCREEN_TEXTURE, SCREEN_UV, blur_amount);
	COLOR = color;
}