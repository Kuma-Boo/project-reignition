shader_type spatial;
render_mode unshaded, depth_draw_always, specular_disabled;

void fragment()
{
	ALBEDO = COLOR.rgb;
}
