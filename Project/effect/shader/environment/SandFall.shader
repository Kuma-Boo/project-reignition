shader_type spatial;
render_mode specular_disabled, depth_draw_alpha_prepass, shadows_disabled;

uniform float direction : hint_range(-1, 1);
uniform float flowSpeed : hint_range(0, 2);
uniform float positionDelta : hint_range(0, 2);
uniform float positionSpeed;
uniform sampler2D albedo : hint_albedo;
uniform sampler2D mask : hint_albedo;

void vertex()
{
	VERTEX.y += sin(TIME * positionSpeed) * positionDelta * direction;
}

void fragment()
{
	float scrollAmount = TIME * flowSpeed * direction;
	
	ALBEDO = texture(albedo, vec2(UV.x + scrollAmount, UV.y)).rgb;
	ALPHA = texture(mask, vec2(UV.x + scrollAmount, min(UV.y, 1))).a;
}