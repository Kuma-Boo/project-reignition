shader_type spatial;
render_mode depth_prepass_alpha, depth_draw_opaque;

uniform sampler2D albedo : hint_default_black, source_color;
uniform sampler2D specular : hint_default_black;
uniform float roughness : hint_range(0, 1) = .8;

void fragment()
{
	vec2 uv = UV;
	
	vec4 col = texture(albedo, uv) * COLOR;
	float view = dot(NORMAL, VIEW);
	
	//Calculate specular
	vec4 spec_col = texture(specular, uv);
	float spec = spec_col.r * spec_col.a;
	spec *= view;
	SPECULAR = spec;
	ROUGHNESS = roughness;
	
	ALBEDO = col.rgb;
	ALPHA = col.a;
}