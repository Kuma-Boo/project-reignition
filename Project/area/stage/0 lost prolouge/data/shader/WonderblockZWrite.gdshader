shader_type spatial;
render_mode unshaded, depth_draw_always, blend_mul, specular_disabled;

void fragment()
{
	ALBEDO = .6 - COLOR.rgb;
}
