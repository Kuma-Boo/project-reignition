shader_type canvas_item;

uniform float flash_amount : hint_range(0, 1);
void fragment()
{
	COLOR.rgb += COLOR.rgb * flash_amount;
}
