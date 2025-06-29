shader_type spatial;
render_mode unshaded, fog_disabled;

global uniform float time;
uniform sampler2D background_texture : source_color;
uniform sampler2D star_texture : source_color;
uniform float star_flicker_speed = .1;

void fragment()
{
	vec2 star_uv = UV + vec2(star_flicker_speed) * time;
	vec4 star_col = texture(star_texture, star_uv) * COLOR * vec4(2);
	vec4 base_col = texture(background_texture, UV2);
	vec4 col = mix(star_col, base_col, base_col.a);
	ALBEDO = col.rgb;
}
