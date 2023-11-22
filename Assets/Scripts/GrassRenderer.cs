using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bliss
{
    public class GrassChunk : IComparable<GrassChunk>
    {
        //public Vector2Int grid;
        public float X
        {
            get { return pos3d.x; }
        }
        public float Y
        {
            get { return pos3d.z; }
        }
        public Vector2Int grid;
        public Vector2 pos2d;
        public Vector3 pos3d;
        public float size;
        public int index;
        public Vector3 cameraLocalCoord;

        public GrassChunk(Vector2Int grid, Camera cam, float size, int index)
        {
            this.grid = grid;
            this.pos2d = new Vector2(grid.x * size, grid.y * size);
            this.pos3d = new Vector3(grid.x * size, cam.transform.position.y, grid.y * size);
            this.size = size;
            this.index = index;
            this.cameraLocalCoord = cam.transform.InverseTransformPoint(this.pos3d);
            this.pos3d.y = 0;
        }
        public Bounds GetBounds(float height)
        {
            return new Bounds(new Vector3(X + size * 0.5f, height * 0.5f, Y + size * 0.5f), new Vector3(size, height, size));
        }

        public int CompareTo(GrassChunk other)
        {
            if (grid == other.grid) return 0;
            //if (this.cameraLocalCoord.z < 0)
            //{
            //    if (other.cameraLocalCoord.z >= 0) return 1;
            //    return 0;
            //}
            if (this.cameraLocalCoord.z < other.cameraLocalCoord.z) return -1;
            return 1;
        }
    }
    [Serializable]
    public struct GrassPassSettings
    {
        public float chunkSize;
        [Range(2, 500)]
        [Tooltip("A chunk will of ChunkGrassSize^2 grass blades.")]
        public int ChunkGrassSize;

        public Vector3 scaleOverride;
        public float windFieldSpeed;
        public float windFieldMagnitude;
        public float grassStiffness;
        public float deltaMultiplier;
        public int GrassNumPerChunk
        {
            get { return ChunkGrassSize * ChunkGrassSize; }
        }
    }

    public class GrassPass : ScriptableRenderPass
    {
        struct GrassRenderProperty
        {
            public Vector4 v0;
            public Vector4 v1andv2;
            public Vector4 right;
            public Vector4 color;
            public static int Size()
            {
                return
                    sizeof(float) * 4 * 4; 
            }
        };
        public float GrassHeight
        {
            get
            {
                return mesh.bounds.size.y * settings.scaleOverride.y;
            }
        }
        public float GrassWidth
        {
            get
            {
                return mesh.bounds.size.x * settings.scaleOverride.x;
            }
        }
        internal ProfilingSampler pofilingSampler = new ProfilingSampler("Grass");
        internal Material material;
        internal Mesh mesh;
        internal ComputeShader compute;
        internal bool enableInSceneViewPort = false;

        internal ComputeBuffer meshRenderPropertyBuffer; // store matrics for grass
        internal ComputeBuffer drawIndirectArgsBuffer; // store number, lod, grid origin position, etc
        internal GrassChunk[] chunks;
        internal GrassPassSettings settings;

        public int BufferSize
        {
            get => meshRenderPropertyBuffer != null? meshRenderPropertyBuffer.count : 0;
        }

        public GrassPass(Material material, Mesh mesh, ComputeShader compute, GrassPassSettings settings, RenderPassEvent injectionPoint)
        {
            this.material = material;
            this.mesh = mesh;
            this.compute = compute;
            this.settings = settings;
            renderPassEvent = injectionPoint;
            //InitializeBuffers(InitialSize);
        }
        public void InitializeBuffers(int chunkSize)
        {
            Dispose();
            int kernel = compute.FindKernel("CSMain");
            // Argument buffer used by DrawMeshInstancedIndirect.
            // It has 5 uint values

            GrassRenderProperty[] properties = new GrassRenderProperty[chunkSize * settings.GrassNumPerChunk];
            for (int i = 0; i < chunkSize * settings.GrassNumPerChunk; i++)
            {
                properties[i].v1andv2.x = 1f;
                properties[i].v1andv2.y = 0f;
                properties[i].v1andv2.z = 1f;
                properties[i].v1andv2.w = 0f;
                properties[i].color = Vector4.one;
            }

            drawIndirectArgsBuffer  = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

            meshRenderPropertyBuffer = new ComputeBuffer(chunkSize * settings.GrassNumPerChunk, GrassRenderProperty.Size());
            meshRenderPropertyBuffer.SetData(properties);
            compute.SetBuffer(kernel, "_Properties", meshRenderPropertyBuffer);
            material.SetBuffer("_Properties", meshRenderPropertyBuffer);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (meshRenderPropertyBuffer == null || meshRenderPropertyBuffer.count < chunks.Length * settings.GrassNumPerChunk)
            {
                Debug.LogWarning("Grass data exceed buffer size. Reallocating buffer...");
                InitializeBuffers(chunks.Length * settings.GrassNumPerChunk + 1);
            }


            var cameraData = renderingData.cameraData;
            if ((!enableInSceneViewPort || cameraData.camera.cameraType != CameraType.SceneView) &&
                (cameraData.camera.cameraType != CameraType.Game && cameraData.camera.cameraType != CameraType.Preview))
            {
                return;
            }

            if (material == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, pofilingSampler))
            {
                var scaleMat = Matrix4x4.Scale(settings.scaleOverride);
                //var rotMat = Matrix4x4.identity;

                int kernel = compute.FindKernel("CSMain");
                //compute.SetMatrix("_ScaleMat", scaleMat);
                //compute.SetMatrix("_RotMat", rotMat);

                compute.SetVector("_Gravity", Physics.gravity);
                compute.SetFloat("_DeltaMultiplier", settings.deltaMultiplier);
                compute.SetFloat("_DeltaTime", Time.deltaTime);
                compute.SetFloat("_Time", Time.time);
                compute.SetFloat("_WindFieldMovingSpeed", settings.windFieldSpeed);
                compute.SetFloat("_WindFieldMagnitude", settings.windFieldMagnitude);
                compute.SetFloat("_GrassStiffness", settings.grassStiffness);
                compute.SetFloat("_GrassHeight", GrassHeight);
                compute.SetFloat("_GrassWidth", GrassWidth);
                material.SetFloat("_GrassHeight", GrassHeight);
                material.SetFloat("_GrassWidth", GrassWidth);
                //material.SetVector("_ScaleOverride", settings.scaleOverride);

                if (chunks != null)
                {
                    foreach (var chunk in chunks)
                    {
                        //if (++chunkCount > maxChunkSize) break;
                        //var chunk = chunks[0];
                        compute.SetVector("_GridOrigin", chunk.pos2d);
                        compute.SetFloat("_GridSize", chunk.size / settings.ChunkGrassSize);
                        compute.SetInt("_ChunkWidth", settings.ChunkGrassSize);
                        compute.SetInt("_PropertiesStartIdx", chunk.index * settings.GrassNumPerChunk);
                        // We used to just be able to use `population` here, but it looks like a Unity update imposed a thread limit (65535) on my device.
                        // This is probably for the best, but we have to do some more calculation.  Divide population by numthreads.x in the compute shader.
                        compute.Dispatch(kernel, Mathf.CeilToInt(settings.GrassNumPerChunk / 64f), 1, 1);
                    }
                }

                uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                // Arguments for drawing mesh.
                // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
                args[0] = (uint)mesh.GetIndexCount(0);
                args[1] = (uint)(settings.GrassNumPerChunk * chunks.Length);
                args[2] = (uint)mesh.GetIndexStart(0);
                args[3] = (uint)mesh.GetBaseVertex(0);
                drawIndirectArgsBuffer.SetData(args);
                cmd.DrawMeshInstancedIndirect(mesh, 0, material, 0, drawIndirectArgsBuffer);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
        public void Dispose()
        {
            if (meshRenderPropertyBuffer != null)
            {
                meshRenderPropertyBuffer.Release();
            }
            meshRenderPropertyBuffer = null;

            if (drawIndirectArgsBuffer != null)
            {
                drawIndirectArgsBuffer.Release();
            }
            drawIndirectArgsBuffer = null;
        }
    }
    //[ExecuteInEditMode] // cause memeory leak
    public class GrassRenderer : MonoBehaviour
    {
        [SerializeField]
        bool enableInScene = false;
        [SerializeField]
        RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
        [SerializeField]
        Camera cam;
        [SerializeField]
        float maxViewDistance = 100f;
        [SerializeField]
        int LOD0Num = 6;
        [SerializeField]
        int LOD1Num = 20;
        [SerializeField]
        int LOD2Num = 50;
        [SerializeField]
        ComputeShader compute;
        [SerializeField]
        Material material; // vert and frag
        [SerializeField]
        Mesh mesh;
        [SerializeField]
        GrassPassSettings settings;

        Plane[] frustumPlanes;
        Vector2[] frustumTriangle = new Vector2[3];
        Vector2[] frustumTriangleLocal = new Vector2[3];

        GrassPass grassPass0;
        Dictionary<Vector2Int, int> chunkBufferMap = new Dictionary<Vector2Int, int>();

        public int DrawNum
        {
            get
            {
                if (grassPass0 != null && grassPass0.chunks != null) return grassPass0.chunks.Length * settings.GrassNumPerChunk;
                return 0;
            }
        }

        void OnEnable()
        {
            grassPass0 = new GrassPass(material, mesh, compute, settings, injectionPoint);
            GenerateChunks();
            grassPass0.InitializeBuffers(LOD0Num);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            grassPass0.Dispose();
            grassPass0 = null;
        }

        private void Update()
        {
            // oriente along camera forward direction
            transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);
            transform.forward = Vector3.Normalize(new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z));

            Profiler.BeginSample("Generate Visible Grass Chunks");
            GenerateChunks();
            Profiler.EndSample();

        }
        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            grassPass0.enableInSceneViewPort = enableInScene;
            grassPass0.settings = settings;
            cam.GetUniversalAdditionalCameraData()
                .scriptableRenderer.EnqueuePass(grassPass0);
        }

        void GenerateChunks()
        {

            #region get the triangle of frustum's projection
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Matrix4x4.Perspective(cam.fieldOfView, cam.aspect, cam.nearClipPlane, maxViewDistance) * cam.worldToCameraMatrix);

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
            //var addedChunks = new HashSet<Vector2Int>();
            var chunks = new SortedSet<GrassChunk>();

            void AddChunk(Vector2Int coord)
            {
                //if (!addedChunks.Contains(coord))
                //{
                    var chunk = new GrassChunk(coord, cam, settings.chunkSize, -1);
                    if (IsChunkVisible(chunk))
                    {
                        chunks.Add(chunk);
                        //addedChunks.Add(coord);
                    }
                //}
            }

            void Swap<T>(ref T x, ref T y) { var tmp = x; x = y; y = tmp; }
            const float EPS = 0.001f;

            void fillBottomFlatTriangle(Vector2 v1, Vector2 v2, Vector2 v3)
            {
                float invslope1 = (v2.x - v1.x) / (v2.y - v1.y);
                float invslope2 = (v3.x - v1.x) / (v3.y - v1.y);

                float curx1 = v1.x;
                float curx2 = v1.x;

                for (int scanlineY = GridIndex(v1.y) - 1; scanlineY <= GridIndex(v2.y); scanlineY++)
                {
                    int xx1 = GridIndex(curx1);
                    int xx2 = GridIndex(curx2);
                    for (int x = Mathf.Min(xx1, xx2); x <= Mathf.Max(xx1, xx2); x++)
                    {
                        var coord = new Vector2Int(x, scanlineY);
                        AddChunk(coord);
                    }
                    curx1 += invslope1 * settings.chunkSize;
                    curx2 += invslope2 * settings.chunkSize;
                }
            }
            void fillTopFlatTriangle(Vector2 v1, Vector2 v2, Vector2 v3)
            {
                float invslope1 = (v3.x - v1.x) / (v3.y - v1.y);
                float invslope2 = (v3.x - v2.x) / (v3.y - v2.y);

                float curx1 = v3.x;
                float curx2 = v3.x;

                for (int scanlineY = GridIndex(v3.y) + 1; scanlineY > GridIndex(v1.y); scanlineY--)
                {
                    int xx1 = GridIndex(curx1);
                    int xx2 = GridIndex(curx2);
                    for (int x = Mathf.Min(xx1, xx2); x <= Mathf.Max(xx1, xx2); x++)
                    {
                        var coord = new Vector2Int(x, scanlineY);
                        AddChunk(coord);
                    }
                    curx1 -= invslope1 * settings.chunkSize;
                    curx2 -= invslope2 * settings.chunkSize;
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

            #region write chunks. if the same chunk exists, map it to the same buffer position.
            int chunkNum = Mathf.Min(LOD0Num, chunks.Count);
            grassPass0.chunks = new GrassChunk[chunkNum];
            var newBufferMap = new Dictionary<Vector2Int, int>();
            if (grassPass0.BufferSize == 0) // first run
            {
                int idx = 0;
                foreach (var chunk in chunks)
                {
                    if (idx >= chunkNum) break;
                    chunk.index = idx++;
                    grassPass0.chunks[chunk.index] = chunk;
                    newBufferMap.Add(chunk.grid, chunk.index);
                }
            }
            else
            {
                if (grassPass0.BufferSize / settings.GrassNumPerChunk < chunkNum)
                {
                    Debug.LogError($"The chunks generated exceed compute buffer's size.");
                    return;
                }
                HashSet<int> availableIndices = new HashSet<int>(Enumerable.Range(0, chunkNum));
                HashSet<GrassChunk> remainings = new HashSet<GrassChunk>();
                int counter = 0;
                foreach (var chunk in chunks)
                {
                    if (counter >= chunkNum) break;
                    if (chunkBufferMap.ContainsKey(chunk.grid))
                    {
                        chunk.index = chunkBufferMap[chunk.grid];
                        grassPass0.chunks[chunk.index] = chunk;
                        availableIndices.Remove(chunk.index);
                    }
                    else remainings.Add(chunk);
                    counter++;
                }
                foreach (var chunk in remainings)
                {
                    int idx = availableIndices.First();
                    availableIndices.Remove(idx);
                    chunk.index = idx;
                    grassPass0.chunks[idx] = chunk;
                    newBufferMap.Add(chunk.grid, idx);
                }
            }
            chunkBufferMap = newBufferMap;
            #endregion
        }
        private void OnDrawGizmosSelected()
        {
            if (grassPass0 != null)
            {
                //Gizmos.matrix = transform.localToWorldMatrix;
                Color cubeColor = new Color(0, 1, 0, 0.5f);
                Color wireColor = new Color(1f, 0f, 0f, 0.5f);
                foreach (var chunk in grassPass0.chunks)
                {
                    Gizmos.color = cubeColor;
                    Gizmos.DrawCube(new Vector3(chunk.X + chunk.size * 0.5f, (TerrainData.MaxHeight + grassPass0.GrassHeight) * 0.5f, chunk.Y + chunk.size * 0.5f), new Vector3(chunk.size, (TerrainData.MaxHeight  + grassPass0.GrassHeight), chunk.size));
                    Gizmos.color = wireColor;
                    Gizmos.DrawWireCube(new Vector3(chunk.X + chunk.size * 0.5f, (TerrainData.MaxHeight  + grassPass0.GrassHeight) * 0.5f, chunk.Y + chunk.size * 0.5f), new Vector3(chunk.size, (TerrainData.MaxHeight  + grassPass0.GrassHeight), chunk.size));
                }
            }
            if (frustumTriangle != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 vert0 = new Vector3(frustumTriangle[0].x, 0, frustumTriangle[0].y);
                Vector3 vert1 = new Vector3(frustumTriangle[1].x, 0, frustumTriangle[1].y);
                Vector3 vert2 = new Vector3(frustumTriangle[2].x, 0, frustumTriangle[2].y);
                Gizmos.DrawSphere(vert0, 0.1f);
                Gizmos.DrawSphere(vert1, 0.1f);
                Gizmos.DrawSphere(vert2, 0.1f);
                Gizmos.DrawLine(vert0, vert1);
                Gizmos.DrawLine(vert0, vert2);
                Gizmos.DrawLine(vert1, vert2);
            }
        }
        bool IsChunkVisible(GrassChunk chunk)
        {
            var bounds = chunk.GetBounds((TerrainData.MaxHeight + grassPass0.GrassHeight) * 1.1f);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }
        bool IsPointVisible(Vector3 pos)
        {
            Vector3 pos2 = cam.WorldToViewportPoint(pos);
            return pos2.x > 0 && pos2.x < 1 && pos2.y > 0 && pos2.y < 1 && pos2.x > 0 && pos2.z > 0 && pos2.z <= maxViewDistance;
        }

        int GridIndex(float x) => Mathf.FloorToInt(x / settings.chunkSize);
    }
}