shader_type spatial;
render_mode cull_disabled, blend_add, unshaded;

global uniform float time;

uniform float intensity = 1.0;
uniform vec2 scroll_speed;
uniform float opacity = 1.0;
uniform float softness : hint_range(0.0, 5.0, 0.1) = 1.0;
uniform sampler2D albedo : hint_default_white, repeat_enable;
uniform sampler2D vertex_color : hint_default_white, repeat_enable;
uniform sampler2D lut : repeat_enable;


void fragment()
{
	vec2 uv_offset = scroll_speed * time;
	vec2 uv1 = (UV + uv_offset);
	vec2 uv2 = (UV + (uv_offset * vec2(1, -1)));
	vec4 col = texture(albedo, uv1) * texture(albedo, uv2);
	
	vec2 lut_uv = vec2(col.r, 0);
	vec3 power_amount = texture(lut, lut_uv).rgb * intensity;
	ALBEDO = pow(col.rgb, power_amount);
	
	float distance_fade = clamp(smoothstep(.1, .5, length(VERTEX)), 0.0, 1.0);
	ALPHA *= texture(vertex_color, UV2).r * opacity * distance_fade;
	if(softness > .01)
	{
		ALPHA *= pow(dot(NORMAL, VIEW), softness);
	}
	ALPHA = clamp(ALPHA, 0.0, 1.0);
}
