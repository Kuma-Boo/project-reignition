shader_type spatial;
render_mode unshaded, specular_disabled;

uniform sampler2D albedo : source_color;

void fragment()
{
	ALBEDO = texture(albedo, UV).rgb;
}
