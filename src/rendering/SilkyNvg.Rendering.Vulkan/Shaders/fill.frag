#version 400
#extension GL_ARB_separate_shader_objects  : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 pass_vertex;
layout (location = 1) in vec2 pass_tcoord;

layout (location = 0) out vec4 out_Colour;

layout (std140, binding = 1) uniform frag {
	mat3 scissorMat;
	mat3 paintMat;
	vec4 innerCol;
	vec4 outerCol;
	vec2 scissorExt;
	vec2 scissorScale;
	vec2 extent;
	float radius;
	float feather;
	float strokeMult;
	float strokeThr;
	int texType;
	int type;
};

layout (binding = 2) uniform sampler2D tex;

float sdroundrect(vec2 pt, vec2 ext, float rad) {
	vec2 ext2 = ext - vec2(rad, rad);
	vec2 d = abs(pt) - ext2;
	return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - rad;
}

float scissorMask(vec2 p) {
	vec2 sc = (abs((scissorMat * vec3(p, 1.0)).xy) - scissorExt);
	sc = vec2(0.5, 0.5) - sc * scissorScale;
	return clamp(sc.x, 0.0, 1.0) * clamp(sc.y, 0.0, 1.0);
}

void main(void) {
	float scissor = scissorMask(pass_vertex);
	float strokeAlpha = 1.0;
	if (strokeAlpha < strokeThr) {
		discard;
	}
	if (type == 0) { // fill
		vec2 pt = (paintMat * vec3(pass_vertex, 1.0)).xy;
		float d = clamp((sdroundrect(pt, extent, radius) + feather * 0.5) / feather, 0.0, 1.0);
		vec4 colour = mix(innerCol, outerCol, d);

		colour *= strokeAlpha * scissor;
		out_Colour = colour;
	} else if (type == 1) { // image
		vec2 pt = (paintMat * vec3(pass_vertex, 1.0)).xy / extent;
		vec4 colour = texture(tex, pt);
		if (texType == 1) {
			colour = vec4(colour.xyz * colour.w, colour.w);
		} else if (texType == 2) {
			colour = vec4(colour.x);
		}
		colour *= innerCol;
		colour *= strokeAlpha * scissor;
		out_Colour = colour;
	} else if (type == 2) { // stencil
		out_Colour = vec4(1.0, 1.0, 1.0, 1.0);
	} else if (type == 3) { // text
		vec4 colour = texture(tex, pass_tcoord);
		if (texType == 1) {
			colour = vec4(colour.xyz * colour.w, colour.w);
		} else if (texType == 2) {
			colour = vec4(colour.x);
		}
		colour *= scissor;
		out_Colour = colour * innerCol;
	}
}