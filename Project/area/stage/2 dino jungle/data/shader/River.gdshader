shader_type spatial;
render_mode unshaded, specular_disabled, depth_prepass_alpha, depth_draw_opaque, shadows_disabled;

global uniform float time;
uniform sampler2D albedo : source_color;
uniform sampler2D displacement : source_color;
uniform float displacement_scale = 1.0;
uniform float displacement_speed;
uniform float scroll_speed;

void fragment()
{
	vec2 displacement_uv = UV2 * displacement_scale + vec2(0, displacement_speed) * time;
	vec4 uv_offset = texture(displacement, displacement_uv);
	vec2 uv = UV + uv_offset.ra + vec2(sin(time * 4.0) * .2, scroll_speed * time);
	vec4 col = texture(albedo, uv) * COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
