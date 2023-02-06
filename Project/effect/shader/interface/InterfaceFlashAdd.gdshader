shader_type canvas_item;
render_mode blend_add;

uniform float flash_amount : hint_range(0, 1);
void fragment()
{
	COLOR.rgb += COLOR.rgb * flash_amount;
}
