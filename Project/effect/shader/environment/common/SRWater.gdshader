shader_type spatial;
render_mode shadows_disabled;

uniform sampler2D base : hint_normal;
uniform sampler2D detail : hint_normal;
uniform sampler2D clip : hint_default_black;
uniform vec4 base_color : source_color;

void fragment()
{
	vec4 col = base_color * COLOR;
	vec4 normal = texture(base, UV) + texture(detail, UV);
	normal = clamp(normal, vec4(0), vec4(1));
	//normal *= texture(clip, UV).a;
	ALBEDO = col.rgb;
	///NORMAL = normal.rgb;
	
	ROUGHNESS = 0.0;
	ALPHA = col.a;
}
