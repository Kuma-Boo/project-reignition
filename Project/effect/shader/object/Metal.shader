shader_type spatial;
render_mode specular_disabled, shadows_disabled;

uniform sampler2D albedo;

void fragment()
{
	vec3 r = reflect(VIEW, NORMAL);
	float m = 2.0 * sqrt(pow(r.x, 2) + pow(r.y, 2) + pow(r.z, 2));
	vec2 uv = r.xy / m + .5;
	ALBEDO = texture(albedo, uv).rgb;
}