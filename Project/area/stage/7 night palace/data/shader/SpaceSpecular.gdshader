shader_type spatial;
render_mode unshaded, blend_add, cull_disabled;

uniform float bias = 0.0;

void fragment()
{
	float dotProd = 1.0 - dot(VIEW, NORMAL) + bias;
	ALBEDO = vec3(clamp(dotProd, 0, 1) + .02);
}