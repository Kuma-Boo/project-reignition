shader_type spatial;
render_mode specular_disabled, depth_draw_alpha_prepass, shadows_disabled, cull_disabled;

uniform float flowSpeed : hint_range(0, 2);
uniform float displacementFlowSpeed : hint_range(0, 2);
uniform float displacementScale : hint_range(0, 2) = 1.0;
uniform sampler2D albedo : hint_albedo;
uniform sampler2D displacement : hint_black_albedo;
uniform sampler2D mask : hint_albedo;

void fragment()
{
	float scrollAmount = TIME * -flowSpeed;
	
	vec2 uv = UV2 + texture(displacement, UV2 * displacementScale + vec2(TIME * -displacementFlowSpeed)).ra;
	vec4 col = texture(albedo, vec2(uv.x, uv.y + scrollAmount));
	ALBEDO = col.rgb * COLOR.rgb;
	ALPHA = COLOR.a * col.a * texture(mask, vec2(UV.x + scrollAmount, clamp(UV.y, .1, .9))).a;
}