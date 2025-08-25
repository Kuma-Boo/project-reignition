shader_type spatial;
render_mode unshaded, cull_disabled, fog_disabled;

uniform sampler2D albedo : source_color;
uniform float skewAmount = .5;
uniform vec2 uv_scale = vec2(1.0, 1.0);

varying float lifetime;

void vertex()
{
	lifetime = INSTANCE_CUSTOM.y;
}

void fragment()
{
	vec2 uv = UV * uv_scale;
	uv.x += (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).y * skewAmount;
	uv.y = clamp(uv.y - lifetime, 0.0, 1.0);
	vec4 col = texture(albedo, uv);
	col *= COLOR;
	
	ALBEDO = col.rgb;
	ALPHA = col.a;
}