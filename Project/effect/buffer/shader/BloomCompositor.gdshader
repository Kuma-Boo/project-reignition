shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D bloom_texture : source_color, hint_default_transparent;
uniform bool multiply;

/*
void vertex()
{
    POSITION = vec4(VERTEX, 1.0);
}
*/

void fragment()
{
	vec4 col = texture(SCREEN_TEXTURE, SCREEN_UV);
	vec4 bloom_col = texture(bloom_texture, SCREEN_UV);
	
	if(multiply)
		col.rgb = col.rgb * bloom_col.rgb;
	else
		col.rgb = col.rgb + bloom_col.rgb;
	
	COLOR.rgb = col.rgb;
}