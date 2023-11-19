using UnityEngine.Rendering;
using UnityEngine;

namespace Bliss
{
    /// <summary>
    /// a simple screen space draw call
    /// </summary>
    class Blitter
    {
        Mesh quad = new Mesh();

        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f)
        };

        int[] triangles = new int[6]
        {
            0, 2, 1,
            2, 3, 1
        };

        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        public Blitter()
        {
            quad.vertices = vertices;
            quad.triangles = triangles;
            quad.uv = uv;
        }
        public void Blit(CommandBuffer cmd, Material mat, int pass = 0)
        {
            cmd.DrawMesh(quad, Matrix4x4.identity, mat, 0, pass);
        }
    };
}