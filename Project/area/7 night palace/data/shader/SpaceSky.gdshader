shader_type spatial;
render_mode unshaded, depth_draw_opaque, ambient_light_disabled;

uniform sampler2D albedo : source_color;
const float INTENSITY = 3.0;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb * INTENSITY;
	ALPHA = col.a;
}

