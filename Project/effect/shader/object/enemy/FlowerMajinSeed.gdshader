shader_type spatial;
render_mode specular_disabled;

uniform sampler2D albedo : source_color;
uniform sampler2D specular : source_color;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	vec3 upDirection = (VIEW_MATRIX * vec4(0, 1, 0, 0)).rgb;
	
	vec4 spec_col = texture(specular, UV);
	float spec = spec_col.r * .4;
	spec *= dot(NORMAL, upDirection);
	col.g = clamp(col.g + spec, 0, 1);
	
	ALBEDO = col.rgb;
}