#ifndef TERRAIN_HLSL
#define TERRAIN_HLSL


/// <summary>
/// reference: https://www.shadertoy.com/view/Mds3DX
/// </summary>

#define NORMAL_SAMPLE_DELTA 0.01
#define OCTAVES 4
#define MAX_MARCH_STEPS 150
// doesn't matter much unless it is too small
#define TILE_WIDTH 1024.0
#define HEIGHT_MAGNITUDE 0.6

float hash(float n) {
	//return frac(sin(n) * 43758.5453123);

	// reference: https://www.shadertoy.com/view/4djSRW
	n = frac(n * .1031);
	n *= n + 33.33;
	n *= n + n;
	return frac(n);
}

// Value noise generator. Returns
// three values on [-1, +1]
float3 noised(float2 x) {
	float2 p = floor(x);
	float2 f = frac(x);

	float n = p.x + p.y * TILE_WIDTH;

	// Grab noise values at four corners of a square
	float a = hash(n + 0.0);
	float b = hash(n + 1.0);
	float c = hash(n + TILE_WIDTH);
	float d = hash(n + TILE_WIDTH + 1.0);

	// use smoothstep-filtered lerp for one component and compute the derivatives for the others
	// See https://iquilezles.org/articles/morenoise

	// The (negative) smoothstep weight
	float2 u = f * f * (3.0 - 2.0 * f);
	return float3(a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y,
		30.0 * f * f * (f * (f - 2.0) + 1.0) * (float2(b - a, c - a) + (a - b - c + d) * u.yx));
}

float GetHeight(float2 p)
{
	p *= 0.5;

	const float2x2 M2 = float2x2(1.6, -1.2, 1.2, 1.6);
	float height = 0.0;
	float2 d = 0.0;

	// Magnitude at this octave
	float magnitude = HEIGHT_MAGNITUDE;

	// Add multiple octaves of noise, chosen from points that spiral outward
	// to avoid hitting the tiling period of the noise function.
	for (int i = 0; i < OCTAVES; ++i) {
		float3 n = noised(p);
		d += n.yz;

		// The 1 + |d|^2 denominator creates the mountainous lumpiness.
		// Without it, this is a standard value noise function.
		height += magnitude * n.x / (1.0 + dot(d, d));
		p = mul(M2, p);
		magnitude *= 0.5;
	}

	// iq's original had 0.5 here, but that doesn't fit the range
	return (height * 0.5 + 0.5);
}

float3 GetNormal(float2 p)
{
	float3 n = float3(
		GetHeight(float2(p.x - NORMAL_SAMPLE_DELTA, p.y)) - GetHeight(float2(p.x + NORMAL_SAMPLE_DELTA, p.y)),
		2.0 * NORMAL_SAMPLE_DELTA,
		GetHeight(float2(p.x, p.y - NORMAL_SAMPLE_DELTA)) - GetHeight(float2(p.x, p.y + NORMAL_SAMPLE_DELTA)));
	return normalize(n);
}

bool RayMarchTerrain(inout float3 rayPos, float3 rayDir, float step = 0.5, float maxDistance = 1000.0)
{
	float time = 0.0;
	float lastHeight = 0.0;
	float lastY = 0.0;
	float deltaT = 0.01;
	float3 pos;
	float height;
	bool hitFound = false;
	for (int index = 0; index < 150; ++index)
	{
		pos = rayPos + rayDir * time;
		height = GetHeight(pos.xz);
		if (height > pos.y)
		{
			hitFound = true;
			break;
		}

		deltaT = max(0.01 * float(time), (pos.y - height) * step);
		time += deltaT;

		if (time > maxDistance)
			break;

		lastHeight = height;
		lastY = pos.y;
	}

	if (hitFound) {
		time = time - deltaT + deltaT * (lastHeight - lastY) / (pos.y - lastY - height + lastHeight);
		rayPos += rayDir * time;
	}

	return hitFound;
}
#endif