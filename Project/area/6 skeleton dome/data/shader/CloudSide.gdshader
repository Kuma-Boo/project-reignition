shader_type spatial;
render_mode unshaded, blend_add;

global uniform float time;

uniform sampler2D albedo : source_color, hint_default_black;
uniform float scroll_speed = .02;

void fragment()
{
	vec4 col = texture(albedo, UV2 - vec2(time * scroll_speed, 0)) * COLOR;
	
	ALBEDO = col.rgb;
	ALPHA = col.a;
}

