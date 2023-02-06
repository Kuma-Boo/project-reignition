shader_type spatial;
render_mode unshaded, depth_draw_never, depth_test_disabled;

global uniform float render_distance;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;

void fragment()
{
	float depth = texture(DEPTH_TEXTURE, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
	vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	float linear_depth = -(view.xyz / view.w).z;
	float normalized_depth = linear_depth / render_distance;
	
	ALBEDO = vec3(normalized_depth, linear_depth, depth);
	
	//Become transparent when nothing is found
	if(normalized_depth + (render_distance * (.99 / render_distance)) >= 1.0)
		ALPHA = 0.0;
	else
		ALPHA = 1.0;
}