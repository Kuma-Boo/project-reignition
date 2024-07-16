shader_type spatial;
render_mode unshaded, specular_disabled, depth_prepass_alpha, depth_draw_opaque, shadows_disabled, cull_disabled;

global uniform float time;

uniform float flowSpeed : hint_range(0, 2);
uniform float displacementFlowSpeed : hint_range(0, 2);
uniform float displacementScale : hint_range(0, 2) = 1.0;
uniform sampler2D albedo : source_color;
uniform sampler2D displacement : hint_default_black;
uniform sampler2D mask : source_color;

void fragment()
{
	float scrollAmount = time * -flowSpeed;
	float mask_col = texture(mask, vec2(UV.x + scrollAmount, clamp(UV.y, .1, .9))).a;
	
	vec2 uv = UV2 + texture(displacement, UV2 * displacementScale + vec2(time * -displacementFlowSpeed)).ra;
	vec4 col = texture(albedo, vec2(uv.x, uv.y + scrollAmount)) * COLOR;
	col.a *= mask_col;
	ALBEDO = col.rgb;
	ALPHA = col.a;
}