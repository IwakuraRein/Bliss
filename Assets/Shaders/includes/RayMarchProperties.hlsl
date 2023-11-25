#ifndef RAY_MARCH_PROPERTIES_HLSL
#define RAY_MARCH_PROPERTIES_HLSL

#define MAX_MARCH_TERRAIN_STEPS 128
#define MAX_MARCH_CLOUD_STEPS 64

float _rayMarchHash(float n) {
	// this generates different results in CPU
	//return frac(sin(n) * 43758.5453123);

	// reference: https://www.shadertoy.com/view/4djSRW
	// this generates different results in compute shaders. haven't figured out why.
	//n = frac(n * .1031);
	//n *= n + 33.33;
	//n *= n + n;
	//return frac(n);

	// reference: https://gist.github.com/keijiro/24f9d505fac238c9a2982c0d6911d8e3
	uint s = asuint(n);
	s ^= 2747636419u;
	s *= 2654435769u;
	s ^= s >> 16;
	s *= 2654435769u;
	s ^= s >> 16;
	s *= 2654435769u;
	return float(s) / 4294967295.0;
}

#endif