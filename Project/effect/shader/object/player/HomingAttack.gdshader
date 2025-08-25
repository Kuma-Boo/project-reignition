shader_type spatial;
render_mode cull_disabled, blend_add, unshaded, fog_disabled;

global uniform float time;

uniform float intensity = 1.0;
uniform float hue_shift : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform vec2 scroll_speed;
uniform float opacity = 1.0;
uniform float softness : hint_range(0.0, 5.0, 0.1) = 1.0;
uniform sampler2D albedo : hint_default_white, repeat_enable;
uniform sampler2D lut : repeat_enable;

const mat3 RGB_to_YIQ = mat3(
		vec3(0.2989, 0.5959, 0.2115),
		vec3(0.5870, -0.2774, -0.5229),
		vec3(0.1140, -0.3216, 0.3114));

mat3 get_hue_shift()
{
	float cos_shift = cos(hue_shift * TAU);
	float sin_shift = sin(hue_shift * TAU);

	return mat3(
	vec3(1.0, 0, 0),
	vec3(0, cos_shift, -sin_shift),
	vec3(0, sin_shift, cos_shift));
}

void fragment()
{
	vec2 uv_offset = scroll_speed * time;
	vec2 uv1 = (UV + uv_offset);
	vec2 uv2 = (UV + (uv_offset * vec2(1, -1)));
	vec3 col = (texture(albedo, uv1) * texture(albedo, uv2) * COLOR).rgb;

	vec2 lut_uv = vec2(col.r, 0);
	float power_amount = texture(lut, lut_uv).r * intensity;

	col = RGB_to_YIQ * col;
	col = get_hue_shift() * col;
	col = inverse(RGB_to_YIQ) * col;

	ALBEDO = col * power_amount;

	float distance_fade = clamp(smoothstep(.1, .5, length(VERTEX)), 0.0, 1.0);
	ALPHA *= opacity * distance_fade * COLOR.a;
	if(softness > .01)
		ALPHA *= pow(dot(NORMAL, VIEW), softness);

	ALPHA = clamp(ALPHA, 0.0, 1.0);
}