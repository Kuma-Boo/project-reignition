shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque, depth_prepass_alpha;

global uniform float time;

uniform float flowSpeed : hint_range(0, 5, .1);
uniform float position_delta : hint_range(0, 2, .1);
uniform float position_offset;
uniform sampler2D albedo : source_color;
uniform sampler2D mask : source_color;

const float POSITION_SPEED = 5.0;

void vertex()
{
	VERTEX.y -= sin((time + position_offset) * POSITION_SPEED) * position_delta;
}

void fragment()
{
	float scrollAmount = time * flowSpeed;
	
	vec4 col = texture(albedo, vec2(UV.x + scrollAmount, UV.y)) * COLOR;
	col.a *= texture(mask, vec2(UV.x + scrollAmount, clamp(UV.y, .1, .9))).r;

	ALBEDO = col.rgb;
	ALPHA = col.a;
}