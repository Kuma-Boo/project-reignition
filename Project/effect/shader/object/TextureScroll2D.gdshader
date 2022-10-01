shader_type spatial;
render_mode unshaded, cull_back;

global uniform float time;

uniform sampler2D albedo : source_color;
uniform vec2 scale = vec2(1);
uniform vec2 scrollSpeed = vec2(0);
uniform vec2 offset = vec2(0);

void fragment()
{
	vec2 uv = UV * scale;
	uv += offset + scrollSpeed * time;

	vec4 col = texture(albedo, uv);
	ALBEDO = col.rgb;
	ALBEDO *= COLOR.rgb; //Multiply the vertex color
	ALPHA = col.a * COLOR.a; //Set Alpha
}