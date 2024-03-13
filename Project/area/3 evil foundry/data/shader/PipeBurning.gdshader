shader_type spatial;
render_mode specular_disabled;

global uniform float time;
uniform sampler2D albedo : source_color;

const float GLOW_SPEED = 5.0;
const float GLOW_STRENGTH = 4.0;
const float GLOW_BIAS = 3.0;

void fragment()
{
	vec4 col = texture(albedo, UV) * COLOR;
	float t = (sin(time * GLOW_SPEED) + 1.0) * .5;
	ALBEDO = col.rgb;
	EMISSION = col.rgb * (t * GLOW_STRENGTH + GLOW_BIAS);
}
