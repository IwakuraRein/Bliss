using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    public struct GrassChunk
    {
        public float x;
        public float z;
        public float size;
        public float height;
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

        private void Update()
        {
            // oriente along camera forward direction
            //transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);
            //transform.forward = Vector3.Normalize(new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z));
            GenerateChunks();
        }
        void GenerateChunks()
        {
            // find the convex hull of camera's view frustum
            //cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), maxViewDistance, Camera.MonoOrStereoscopicEye.Mono, corners);
            Vector3[] corners = new Vector3[5]
            {
                transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0, 0, maxViewDistance))),
                transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(1, 0, maxViewDistance))),
                transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0, 1, maxViewDistance))),
                transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(1, 1, maxViewDistance))),
                new Vector3(cam.transform.position.x, 0, cam.transform.position.z),
            };

            float maxX = float.MinValue, minX = float.MaxValue, maxZ = float.MinValue, minZ = float.MaxValue;
            foreach (var corner in corners)
            {
                maxX = Mathf.Max(maxX, corner.x);
                minX = Mathf.Min(minX, corner.x);
                maxZ = Mathf.Max(maxZ, corner.z);
                minZ = Mathf.Min(minZ, corner.z);
            }
            minX = chunkSize * (int)((minX - chunkSize * 0.5f) / chunkSize);
            maxX = chunkSize * (int)((maxX + chunkSize * 0.5f) / chunkSize);
            minZ = chunkSize * (int)((minZ - chunkSize * 0.5f) / chunkSize);
            maxZ = chunkSize * (int)((maxZ + chunkSize * 0.5f) / chunkSize);

            // TODO: use jobsystem or compute shader
            // 10,000,000 blades???
            //blades = new GrassBlade[((int)((maxX - minX) / grassInterval)+1) * ((int)((maxZ - minZ) / grassInterval) + 1)];

            chunks = new List<GrassChunk>();
            float interval = chunkSize;
            for (float z = minZ; z <= maxZ; z += interval)
            {
                for (float x = minX; x <= maxX; x += interval)
                {
                    var chunk = new GrassChunk();
                    chunk.x = x; chunk.z = z; chunk.size = interval; chunk.height = grassHeight;
                    if (IsChunkVisible(chunk)) chunks.Add(chunk);
                }
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
                    Gizmos.DrawCube(new Vector3(chunk.x + chunk.size * 0.5f, chunk.height * 0.5f, chunk.z + chunk.size * 0.5f), new Vector3(chunk.size, chunk.height, chunk.size));
                }
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
                new Vector3(chunk.x - chunk.size * 0.5f, 0f, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, 0f, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, 0f, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, 0f, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, chunk.height, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, chunk.height, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, chunk.height, chunk.z - chunk.size * 0.5f),
                new Vector3(chunk.x - chunk.size * 0.5f, chunk.height, chunk.z - chunk.size * 0.5f),
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