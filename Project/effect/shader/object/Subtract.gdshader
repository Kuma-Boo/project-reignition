/// Custom implementation of a subtract shader since
/// built-in blend_sub doesn't seem to work properly.
shader_type spatial;
render_mode unshaded, specular_disabled;

uniform float transparency : hint_range(0, 1, .1) = .5; 
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture;

void fragment()
{
	vec4 col = texture(SCREEN_TEXTURE, SCREEN_UV) - COLOR * transparency;
	col = clamp(col, vec4(0.0), vec4(1.0));
	ALBEDO = col.rgb;
}