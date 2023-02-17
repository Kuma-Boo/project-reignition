shader_type spatial;
render_mode specular_disabled, shadows_disabled;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform sampler2D displacement : source_color;
const vec2 displacement_scale = vec2(1.6, 1.6);
const float scroll_speed = .4;

void fragment()
{
	vec2 uv = UV + texture(displacement, UV * displacement_scale + vec2(0, time * scroll_speed)).ra;
	vec2 uv2 = UV2 + texture(displacement, UV2 * displacement_scale - vec2(time * scroll_speed, 0)).ra;
	vec4 col = texture(albedo, uv);
	
	//Bring out red and green channels
	col.r *= 2.0;
	col.g *= 1.5;
	
	ALBEDO = col.rgb;
	EMISSION = col.rgb;
}
