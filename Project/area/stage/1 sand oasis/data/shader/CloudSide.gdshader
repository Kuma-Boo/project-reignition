shader_type spatial;
render_mode diffuse_burley, depth_draw_never, unshaded;

global uniform float time;

uniform sampler2D albedo : source_color, hint_default_black;
uniform sampler2D detail : source_color, hint_default_black;
uniform sampler2D clip_mask : source_color, hint_default_black;
uniform sampler2D tint : source_color, hint_default_black;
//Couldn't recreate the bloom/glow :(
//uniform sampler2D glow_shape : source_color, hint_default_black;
//uniform sampler2D glow_strength : source_color, hint_default_black;
uniform float scroll_speed : hint_range(0.0, 0.1, 0.01);

void fragment()
{
	float scroll = time * scroll_speed;
	
	vec2 main_uv = UV - vec2(scroll, 0);
	vec3 col = texture(albedo, main_uv).rgb;

	vec4 detail_col = texture(detail, main_uv);
	col = mix(col, detail_col.rgb, detail_col.a);
	
	vec4 tint_col = texture(tint, UV2);
	col += tint_col.rgb * tint_col.a * .5;
	
	ALBEDO = col.rgb;
	ALPHA = texture(clip_mask, main_uv).a * COLOR.a;
}
