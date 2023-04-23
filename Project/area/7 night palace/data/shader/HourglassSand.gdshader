shader_type spatial;
render_mode diffuse_burley, specular_disabled;

instance uniform float scroll : hint_range(0, 1);
uniform sampler2D albedo : source_color;

void fragment()
{
	vec4 col = texture(albedo, UV + vec2(0, scroll)) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = .5;
}
