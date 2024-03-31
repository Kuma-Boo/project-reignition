shader_type canvas_item;
render_mode blend_add, unshaded;

global uniform float time;
uniform sampler2D screen_texture : source_color, hint_screen_texture;

void fragment()
{
	vec4 col = texture(screen_texture, SCREEN_UV);
	float flicker_amount = (sin(time * 10.0) + 1.0) * .5;
	COLOR.a = col.r * flicker_amount;
}
