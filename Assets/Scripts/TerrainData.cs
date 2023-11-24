using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Bliss
{
    /// <summary>
    /// Query terrain altitude in CPU side.
    /// Coherent with the Terrain.hlsl.
    /// </summary>
    public class TerrainData
    {
        public const int OCTAVES = 4;
        public const float NORMAL_SAMPLE_DELTA = 0.01f;
        public const float TILE_WIDTH = 1024f;
        public const float HEIGHT_MAGNITUDE = 0.6f;

        public static float MaxHeight
        {
            get
            {
                return HEIGHT_MAGNITUDE * 0.5f;
            }
        }
        public static float hash(float n)
        {
            // This yileds different values than shader
            //return frac(sin(n) * 43758.5453123f);

            // reference: https://www.shadertoy.com/view/4djSRW
            n = frac(n * .1031f);
            n *= n + 33.33f;
            n *= n + n;
            return frac(n);
        }
        public static float3 noised(float2 x)
        {
            float2 p = floor(x);
            float2 f = frac(x);

            float n = p.x + p.y * TILE_WIDTH;

            // Grab noise values at four corners of a square
            float a = hash(n);
            float b = hash(n + 1f);
            float c = hash(n + TILE_WIDTH);
            float d = hash(n + TILE_WIDTH + 1f);

            // use smoothstep-filtered lerp for one component and compute the derivatives for the others
            // See https://iquilezles.org/articles/morenoise

            // The (negative) smoothstep weight
            float2 u = f * f * (3f - 2f * f);
            return float3(a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y,
                30f * f * f * (f * (f - 2f) + 1f) * (float2(b - a, c - a) + (a - b - c + d) * u.yx));
        }
        public static float GetHeight(float2 p)
        {
            p *= 0.5f;

            float2x2 M2 = float2x2(1.6f, -1.2f, 1.2f, 1.6f);
            float height = 0f;
            float2 d = 0f;

            // Magnitude at this octave
            float magnitude = HEIGHT_MAGNITUDE;

            // Add multiple octaves of noise, chosen from points that spiral outward
            // to avoid hitting the tiling period of the noise function.
            for (int i = 0; i < OCTAVES; ++i)
            {
                float3 n = noised(p);
                d += n.yz;

                // The 1 + |d|^2 denominator creates the mountainous lumpiness.
                // Without it, this is a standard value noise function.
                height += magnitude * n.x / (1f + dot(d, d));
                p = mul(M2, p);
                magnitude *= 0.5f;
            }

            return height * 0.5f;
        }
        public float3 GetNormal(float2 p)
        {
            float3 n = float3(
                GetHeight(float2(p.x - NORMAL_SAMPLE_DELTA, p.y)) - GetHeight(float2(p.x + NORMAL_SAMPLE_DELTA, p.y)),
                2f * NORMAL_SAMPLE_DELTA,
                GetHeight(float2(p.x, p.y - NORMAL_SAMPLE_DELTA)) - GetHeight(float2(p.x, p.y + NORMAL_SAMPLE_DELTA)));
            return normalize(n);
        }
    }
}