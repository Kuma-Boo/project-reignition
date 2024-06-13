shader_type spatial;
render_mode diffuse_burley, unshaded, fog_disabled, blend_add;

global uniform float time;

uniform sampler2D albedo : source_color, hint_default_black;
uniform sampler2D detail : source_color, hint_default_black;
uniform sampler2D clip_mask : source_color, hint_default_black;
uniform float scroll_speed : hint_range(0.0, 0.1, 0.01);

void fragment()
{
	float scroll = time * scroll_speed;
	vec3 col = texture(albedo, UV + vec2(scroll, 0)).rgb;
	vec4 detail_col = texture(detail, UV2 + vec2(scroll, 0));
	col = mix(col, detail_col.rgb, detail_col.a);
	
	vec2 clip_uv = UV + scroll;
	
	ALBEDO = col.rgb;
	ALPHA = texture(clip_mask, clip_uv).a * COLOR.a;
}
