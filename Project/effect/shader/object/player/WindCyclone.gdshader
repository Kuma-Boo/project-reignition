shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, fog_disabled;

uniform sampler2D albedo : source_color;
uniform vec2 uv_scale = vec2(1.0, 1.0);

varying float lifetime;

void vertex()
{
	lifetime = INSTANCE_CUSTOM.y;
}

void fragment()
{
	vec2 uv = UV * uv_scale;
	uv.y = clamp(uv.y - lifetime, 0.0, 1.0);
	vec4 col = texture(albedo, uv);
	col *= COLOR;

	ALBEDO = col.rgb;
	ALPHA = col.a;
}