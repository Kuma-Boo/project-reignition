shader_type spatial;
render_mode unshaded, cull_back, blend_add;
//Unlit shader that supports scrolling, rotation and scaling.

uniform sampler2D albedo : source_color;
uniform vec2 scale = vec2(1);
uniform vec2 scrollSpeed = vec2(0);
uniform vec2 offset = vec2(0);
uniform float rotationSpeed = 0;
uniform float rotation : hint_range(-360, 360) = 0;

uniform vec2 uvRotationPivot = vec2(0.5);

mat2 get2dRotationMatrix(float angleRadians)
{
    float s = sin(angleRadians);
    float c = cos(angleRadians);
    return mat2(vec2(c, s), vec2(-s, c));
}

void fragment()
{
	vec2 uv = UV * scale;
	//uv -= floor(uv); // make it into [0.0, 1.0) x [0.0, 1.0) range
	uv -= uvRotationPivot; // move origin to the rotation pivot
	uv *= get2dRotationMatrix(radians(rotation + rotationSpeed * TIME)); // rotate
	uv += uvRotationPivot; // move origin back
	uv += offset + scrollSpeed * TIME;

	vec4 col = texture(albedo, uv);
	ALBEDO = col.rgb;
	ALBEDO *= COLOR.rgb; //Multiply the vertex color
	ALPHA = col.a * COLOR.a; //Set Alpha
}