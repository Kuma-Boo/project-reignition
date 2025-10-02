shader_type spatial;
render_mode unshaded, depth_draw_opaque, ambient_light_disabled, fog_disabled;

uniform sampler2D albedo : source_color, hint_default_black;
uniform float intensity = 1.5;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	ALBEDO = col.rgb * intensity;
	ALPHA = col.a;
}

