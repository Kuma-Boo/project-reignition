shader_type spatial;

uniform float flowSpeed : hint_range(0f, 2f);
uniform sampler2D albedo : hint_albedo;
uniform sampler2D mask : hint_albedo;

void fragment()
{
	float scrollAmount = -TIME * flowSpeed;
	
	ALBEDO = texture(albedo, vec2(UV.x + scrollAmount, UV.y)).rgb;
	ALPHA = texture(mask, vec2(UV.x + scrollAmount, UV.y)).r;
}