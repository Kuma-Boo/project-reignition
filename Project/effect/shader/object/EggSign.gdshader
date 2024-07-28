shader_type spatial;
render_mode specular_disabled;

uniform sampler2D albedo : source_color;
uniform sampler2D detail : source_color, repeat_disable;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	vec4 detail_tex = texture(detail, UV2);
	ALBEDO.rgb = mix(col.rgb, detail_tex.rgb, detail_tex.a);
	
	EMISSION = detail_tex.rgb * detail_tex.a;
}
