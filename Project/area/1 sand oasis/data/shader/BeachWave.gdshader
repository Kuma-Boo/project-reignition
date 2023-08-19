shader_type spatial;
render_mode specular_disabled;

global uniform float time;
uniform sampler2D main_tex : source_color, hint_default_transparent;
uniform sampler2D secondary_tex : source_color, hint_default_transparent;
uniform float frequency;
uniform float strength;
uniform float offset;
uniform float secondary_offset;

void fragment()
{
	float alpha = abs(sin(time * frequency));
	vec2 uv = UV;
	uv.y = clamp(uv.y + offset + strength * alpha, 0.01, 1.0);
	vec4 col = texture(main_tex, uv) * alpha;
	
	float secondary_alpha = abs(sin((time - secondary_offset) * frequency));
	vec2 uv2 = UV;
	uv2.y = uv2.y + offset + strength * secondary_alpha;
	vec4 secondary_col = texture(secondary_tex, uv2) * secondary_alpha;
	
	col.rgb += secondary_col.r;
	col.a += secondary_col.r;
	
	col = clamp(col * COLOR, 0, 1);
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
