shader_type spatial;
render_mode unshaded, fog_disabled;

global uniform float time;
uniform sampler2D albedo : source_color;
uniform vec2 main_scroll_speed;
uniform vec2 secondary_scroll_speed;

void fragment()
{
	vec2 main_uv = UV + main_scroll_speed * time;
	vec2 secondary_uv = UV2 + secondary_scroll_speed * time;
	float alpha = texture(albedo, main_uv).a;
	alpha *= texture(albedo, secondary_uv).a;
	ALBEDO = COLOR.rgb;
	ALPHA = alpha * COLOR.a;
}
