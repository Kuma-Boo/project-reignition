shader_type spatial;
render_mode unshaded, depth_draw_opaque;

uniform sampler2D albedo : hint_albedo;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}

