shader_type spatial;
render_mode specular_disabled;

uniform sampler2D albedo : source_color;
uniform float ambient_light_strength : hint_range(0, 1, .1) = 1.0;

void fragment()
{
	vec3 r = reflect(VIEW, NORMAL);
	float m = 2.0 * sqrt(pow(r.x, 2) + pow(r.y, 2) + pow(r.z, 2));
	vec2 uv = r.xy / m + .5;
	vec4 col = texture(albedo, uv);
	ALBEDO = col.rgb;
	EMISSION = col.rgb * (1.0 - ambient_light_strength); //Reduce ambient light influence slightly
}