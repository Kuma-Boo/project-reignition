shader_type spatial;
render_mode specular_disabled, depth_draw_opaque, shadows_disabled;

uniform float direction : hint_range(-1, 1);
uniform float flowSpeed : hint_range(0, 2);
uniform float positionDelta : hint_range(0, 2);
uniform float positionSpeed;
uniform sampler2D albedo : source_color;
uniform sampler2D mask : source_color;

void vertex()
{
	VERTEX.y += sin(TIME * positionSpeed) * positionDelta * direction;
}

void fragment()
{
	float scrollAmount = TIME * flowSpeed * direction;
	
	ALBEDO = texture(albedo, vec2(UV.x + scrollAmount, UV.y)).rgb;
	ALPHA = texture(mask, vec2(UV.x + scrollAmount, clamp(UV.y, .1, .9))).a;
}