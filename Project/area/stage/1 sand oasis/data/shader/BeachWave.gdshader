shader_type spatial;
render_mode specular_disabled, blend_add;

global uniform float time;
uniform sampler2D main_tex : source_color, filter_linear;
uniform sampler2D secondary_tex : source_color, filter_linear;
uniform float frequency;
uniform float strength;

void fragment()
{
	float alpha = pow(sin(time * frequency), 2.0);
	float uv_offset = sin(mod((time * frequency) / PI, 1.0) * 2.6) * strength - strength;//pow(cos(TIME * frequency), 2.0) * strength - strength;
	float secondary_alpha = pow(sin((time - .4) * frequency), 2.0);
	vec2 uv = UV;
	uv.y = clamp(uv.y + uv_offset, 0.01, 1); //Repeat x only
	
	vec4 col = texture(main_tex, uv) * alpha;
	uv.y = (UV.y - uv_offset) + .54;
	vec4 secondary_col = texture(secondary_tex, uv) * secondary_alpha;
	col += secondary_col * secondary_col.r * (1.0 - col.a) * .5;
	col = clamp(col, 0, 1);
	
	col *= COLOR;
	ALBEDO = col.rgb;
	ALPHA = col.a * .6;
}
