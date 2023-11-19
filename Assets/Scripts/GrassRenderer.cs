using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;
using static UnityEditor.LightingExplorerTableColumn;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using static UnityEngine.UI.GridLayoutGroup;

namespace Bliss
{
    public struct GrassBlade
    {
        public float width;
        public Vector3 v0;
        public Vector3 v1;
        public Vector3 v2;
    }
    public class GrassChunk
    {
        public Vector2 pos;
        public float size;
        public float height;

        public GrassChunk(float x, float z, float size, float height)
        {
            this.pos = new Vector2(x, z);
            this.size = size;
            this.height = height;
        }
    }
    public class GrassRenderer : MonoBehaviour
    {
        [SerializeField]
        Camera cam;
        [SerializeField]
        float maxViewDistance = 100f;
        [SerializeField]
        float grassHeight = 0.3f;
        [SerializeField]
        float grassWidth = 0.02f;
        [SerializeField]
        float grassInterval = 0.04f;
        [SerializeField]
        float chunkSize = 2f;
        [SerializeField]
        float chunkHalveDist = 6f;

        List<GrassChunk> chunks;
        Vector2[] frustumTriangle = new Vector2[3];
        Vector2[] frustumTriangleLocal = new Vector2[3];

        private void Update()
        {
            // oriente along camera forward direction
            transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);
            transform.forward = Vector3.Normalize(new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z));

            Profiler.BeginSample("Generate Visible Grass Chunks");
            GenerateChunks();
            Profiler.EndSample();
        }
        void GenerateChunks()
        {
            chunks = new List<GrassChunk>();

            #region get the triangle of frustum's projection
            //cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), maxViewDistance, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
            var frustumCorners = new Vector3[4];
            frustumCorners[0] = transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0, 0, maxViewDistance)));
            frustumCorners[1] = transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(1, 0, maxViewDistance)));
            frustumCorners[2] = transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0, 1, maxViewDistance)));
            frustumCorners[3] = transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(1, 1, maxViewDistance)));
            float maxX = float.MinValue, minX = float.MaxValue, maxZ = float.MinValue, minZ = float.MaxValue;
            foreach (var corner in frustumCorners)
            {
                maxX = Mathf.Max(maxX, corner.x);
                minX = Mathf.Min(minX, corner.x);
                maxZ = Mathf.Max(maxZ, corner.z);
                minZ = Mathf.Min(minZ, corner.z);
            }

            frustumTriangle[0] = new Vector2(cam.transform.position.x, cam.transform.position.z);
            var foo = transform.TransformPoint(minX, 0f, maxZ);
            frustumTriangle[1] = new Vector2(foo.x, foo.z);
            foo = transform.TransformPoint(maxX, 0f, maxZ);
            frustumTriangle[2] = new Vector2(foo.x, foo.z);

            frustumTriangleLocal[0] = transform.InverseTransformPoint(cam.transform.position);
            frustumTriangleLocal[1] = new Vector2(minX, maxZ);
            frustumTriangleLocal[2] = new Vector2(maxX, maxZ);
            #endregion

            #region raterize the triangle
            var chunkMap = new Dictionary<Vector2Int, GrassChunk>();

            int GridIndex(float x) => Mathf.FloorToInt(x / chunkSize);
            void Swap<T>(ref T x, ref T y) { var tmp = x; x = y; y = tmp; }
            const float EPS = 0.001f;

            void fillBottomFlatTriangle(Vector2 v1, Vector2 v2, Vector2 v3)
            {
                float invslope1 = (v2.x - v1.x) / (v2.y - v1.y);
                float invslope2 = (v3.x - v1.x) / (v3.y - v1.y);

                float curx1 = v1.x;
                float curx2 = v1.x;

                for (int scanlineY = GridIndex(v1.y); scanlineY <= GridIndex(v2.y); scanlineY++)
                {
                    int xx1 = GridIndex(curx1);
                    int xx2 = GridIndex(curx2);
                    for (int x = Mathf.Min(xx1, xx2)-1; x <= Mathf.Max(xx1, xx2)+1; x++)
                    {
                        var coord = new Vector2Int(x, scanlineY);
                        if (!chunkMap.ContainsKey(coord))
                        {
                            var chunk = new GrassChunk((float)coord.x * chunkSize, (float)coord.y * chunkSize, chunkSize, grassHeight);
                            if (IsChunkVisible(chunk)) chunkMap.Add(coord, chunk);
                        }
                    }
                    curx1 += invslope1 * chunkSize;
                    curx2 += invslope2 * chunkSize;
                }
            }
            void fillTopFlatTriangle(Vector2 v1, Vector2 v2, Vector2 v3)
            {
                float invslope1 = (v3.x - v1.x) / (v3.y - v1.y);
                float invslope2 = (v3.x - v2.x) / (v3.y - v2.y);

                float curx1 = v3.x;
                float curx2 = v3.x;

                for (int scanlineY = GridIndex(v3.y); scanlineY > GridIndex(v1.y); scanlineY--)
                {
                    int xx1 = GridIndex(curx1);
                    int xx2 = GridIndex(curx2);
                    for (int x = Mathf.Min(xx1, xx2)-1; x <= Mathf.Max(xx1, xx2)+1; x++)
                    {
                        var coord = new Vector2Int(x, scanlineY);
                        if (!chunkMap.ContainsKey(coord))
                        {
                            var chunk = new GrassChunk((float)coord.x * chunkSize, (float)coord.y * chunkSize, chunkSize, grassHeight);
                            if (IsChunkVisible(chunk)) chunkMap.Add(coord, chunk);
                        }
                    }
                    curx1 -= invslope1 * chunkSize;
                    curx2 -= invslope2 * chunkSize;
                }
            }

            if (frustumTriangle[0].y > frustumTriangle[1].y)
                Swap(ref frustumTriangle[0], ref frustumTriangle[1]);
            if (frustumTriangle[0].y > frustumTriangle[2].y)
                Swap(ref frustumTriangle[0], ref frustumTriangle[2]);
            if (frustumTriangle[1].y > frustumTriangle[2].y)
                Swap(ref frustumTriangle[1], ref frustumTriangle[2]);

            if (frustumTriangle[2].y - frustumTriangle[1].y < EPS)
            {
                fillBottomFlatTriangle(frustumTriangle[0], frustumTriangle[1], frustumTriangle[2]);
            }
            /* check for trivial case of top-flat triangle */
            else if (frustumTriangle[1].y - frustumTriangle[0].y < EPS)
            {
                fillTopFlatTriangle(frustumTriangle[0], frustumTriangle[1], frustumTriangle[2]);
            }
            else
            {
                /* general case - split the triangle in a topflat and bottom-flat one */
                Vector2 v4 = new Vector2(
                    frustumTriangle[0].x + ((frustumTriangle[1].y - frustumTriangle[0].y) / (frustumTriangle[2].y - frustumTriangle[0].y)) * (frustumTriangle[2].x - frustumTriangle[0].x), frustumTriangle[1].y);
                fillBottomFlatTriangle(frustumTriangle[0], frustumTriangle[1], v4);
                fillTopFlatTriangle(frustumTriangle[1], v4, frustumTriangle[2]);
            }
            #endregion

            #region combine the chunks that are far away

            #endregion

            foreach (var chunk in chunkMap.Values)
            {
                chunks.Add(chunk);
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (chunks != null)
            {
                //Gizmos.matrix = transform.localToWorldMatrix;
                Color visibleColor = new Color(0, 1, 0, 0.5f);
                Color invisibleColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                Gizmos.color = visibleColor;
                foreach (var chunk in chunks)
                {
                    Gizmos.DrawCube(new Vector3(chunk.pos.x + chunk.size * 0.5f, chunk.height * 0.5f, chunk.pos.y + chunk.size * 0.5f), new Vector3(chunk.size, chunk.height, chunk.size));
                }
            }
            if (frustumTriangle != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 vert0 = new Vector3(frustumTriangle[0].x, 0, frustumTriangle[0].y);
                Vector3 vert1 = new Vector3(frustumTriangle[1].x, 0, frustumTriangle[1].y);
                Vector3 vert2 = new Vector3(frustumTriangle[2].x, 0, frustumTriangle[2].y);
                Gizmos.DrawSphere(vert0, 1f);
                Gizmos.DrawSphere(vert1, 1f);
                Gizmos.DrawSphere(vert2, 1f);
                Gizmos.DrawLine(vert0, vert1);
                Gizmos.DrawLine(vert0, vert2);
                Gizmos.DrawLine(vert1, vert2);
            }
        }
        //GrassBlade GenerateBlade(float x, float z)
        //{
        //    // TODO: add wind force
        //    var blade = new GrassBlade();
        //    blade.v0 = new Vector3(x, 0, z);
        //    blade.v1 = new Vector3(x, grassHeight, z);
        //    blade.v1 = new Vector3(x, grassHeight, z);
        //    return blade;
        //}
        //bool IsBladeInPolygon(GrassBlade blade, Vector3[] polygon)
        //{
        //    if (IsPointInPolygon(blade.v0, polygon)) return true;
        //    if (IsPointInPolygon(blade.v1, polygon)) return true;
        //    if (IsPointInPolygon(blade.v1, polygon)) return true;
        //    return false;
        //}
        //static bool IsPointInPolygon(Vector3 point, Vector3[] polygon)
        //{
        //    int polygonLength = polygon.Length, i = 0;
        //    bool inside = false;
        //    // x, y for tested point.
        //    float pointX = point.x, pointY = point.z;
        //    // start / end point for the current polygon segment.
        //    float startX, startY, endX, endY;
        //    Vector3 endPoint = polygon[polygonLength - 1];
        //    endX = endPoint.x;
        //    endY = endPoint.z;
        //    while (i < polygonLength)
        //    {
        //        startX = endX; startY = endY;
        //        endPoint = polygon[i++];
        //        endX = endPoint.x; endY = endPoint.z;
        //        //
        //        inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
        //                  && /* if so, test if it is under the segment */
        //                  ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
        //    }
        //    return inside;
        //}
        bool IsChunkVisible(GrassChunk chunk)
        {
            Vector3[] verts = new Vector3[8]
            {
                new Vector3(chunk.pos.x, 0f, chunk.pos.y),
                new Vector3(chunk.pos.x + chunk.size, 0f, chunk.pos.y),
                new Vector3(chunk.pos.x + chunk.size, 0f, chunk.pos.y + chunk.size),
                new Vector3(chunk.pos.x, 0f, chunk.pos.y + chunk.size),
                new Vector3(chunk.pos.x, chunk.height, chunk.pos.y),
                new Vector3(chunk.pos.x + chunk.size, chunk.height, chunk.pos.y),
                new Vector3(chunk.pos.x + chunk.size, chunk.height, chunk.pos.y + chunk.size),
                new Vector3(chunk.pos.x, chunk.height, chunk.pos.y + chunk.size),
            };
            foreach (var vert in verts)
            {
                if (IsPointVisible(vert)) return true;
            }
            return false;
        }
        bool IsPointVisible(Vector3 pos)
        {
            Vector3 pos2 = cam.WorldToViewportPoint(pos);
            return pos2.x > 0 && pos2.x < 1 && pos2.y > 0 && pos2.y < 1 && pos2.x > 0 && pos2.z > 0;
        }
    }
}