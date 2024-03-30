shader_type spatial;
render_mode unshaded;

uniform sampler2D menu_texture : source_color, hint_default_transparent, repeat_disable;


void fragment()
{
	vec4 col = texture(menu_texture, UV);
	ALBEDO = col.rgb;
	ALPHA *= col.a;
}
