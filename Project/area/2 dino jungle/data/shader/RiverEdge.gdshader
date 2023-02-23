///NOTE: The original shader uses 3 UV maps, but Godot only supports 2.
shader_type spatial;
render_mode unshaded, depth_prepass_alpha, depth_draw_opaque, specular_disabled, shadows_disabled;

global uniform float time;
uniform sampler2D albedo : source_color;
uniform sampler2D mask : source_color;
uniform float scroll_speed;
uniform float jiggle_speed;
uniform float jiggle_amount;

void fragment()
{
	vec2 scroll_amount = vec2(scroll_speed * time, sin(time * (jiggle_speed + 20.0)) * jiggle_amount);
	vec2 mask_uv = UV - scroll_amount;
	mask_uv.y = clamp(mask_uv.y, .1, .9);
	float mask_col = texture(mask, mask_uv).a;
	
	vec2 uv = UV2 - scroll_amount;
	vec4 col = texture(albedo, uv) * COLOR;
	col.a *= mask_col;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
