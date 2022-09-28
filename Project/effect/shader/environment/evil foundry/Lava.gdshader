shader_type spatial;
render_mode unshaded;

uniform sampler2D albedo : source_color;
uniform sampler2D displacement : source_color;
const vec2 displacement_scale = vec2(1.6, 1.6);
const float scroll_speed = .4;

void fragment()
{
	vec2 uv = UV + texture(displacement, UV * displacement_scale + vec2(0, TIME * scroll_speed)).ra;
	vec2 uv2 = UV2 + texture(displacement, UV2 * displacement_scale + vec2(-TIME * scroll_speed, 0)).ra;
	vec4 col = texture(albedo, uv);
	col = clamp(col, 0, 1);
	ALBEDO = col.rgb;
}

//void fragment()
//{
//	float scrollAmount = TIME * -flowSpeed;
//
//	vec2 uv = UV2 + texture(displacement, UV2 * displacementScale + vec2(TIME * -displacementFlowSpeed)).ra;
//	vec4 col = texture(albedo, vec2(uv.x, uv.y + scrollAmount));
//	ALBEDO = col.rgb * COLOR.rgb;
//	ALPHA = COLOR.a * col.a * texture(mask, vec2(UV.x + scrollAmount, clamp(UV.y, .1, .9))).a;
//}
