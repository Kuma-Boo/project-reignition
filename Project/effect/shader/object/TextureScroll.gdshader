shader_type spatial;
render_mode diffuse_burley, specular_disabled, depth_prepass_alpha, depth_draw_opaque;

global uniform float time;

uniform float scroll_speed = 0.0;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec2 uv = UV + vec2(0, time * scroll_speed);
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
	SPECULAR = specular;
}

