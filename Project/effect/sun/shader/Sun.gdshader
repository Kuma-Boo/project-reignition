shader_type spatial;
render_mode unshaded, blend_add;

global uniform float sun_occlusion; //position of the sun, on screen
global uniform float sun_movement;
uniform float movement_affect : hint_range(0.0, 1.0, 0.1) = 0.0;
uniform float occlusion_affect : hint_range(0.0, 1.0, 0.1) = 1.0;
uniform sampler2D albedo : source_color;

void fragment()
{
	vec4 col = texture(albedo, UV);
	float fac = clamp(sun_occlusion * occlusion_affect + movement_affect * sun_movement, 0.0, 1.0);
	ALBEDO = mix(col.rgb, vec4(0.0).rgb, fac);
}
