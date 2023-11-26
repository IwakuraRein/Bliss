using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bliss
{
    public struct GrassChunk : IComparable<GrassChunk>
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
            if (Vector3.Magnitude(cameraLocalCoord) < Vector3.Magnitude(other.cameraLocalCoord)) return -1;
            //if (MathF.Abs(cameraLocalCoord.z) < MathF.Abs(other.cameraLocalCoord.z)) return -1;
            return 1;
        }
    }
    [Serializable]
    public struct GrassPassSettings
    {
        public float chunkSize;

        public Vector3 scaleOverride;
        public float windFieldSpeed;
        public float windFieldMagnitude;
        public float grassStiffness;
        public float timeScale;
        [Range(0.01f, 0.99f)]
        public float mouseEventDecay;
        public float mouseEventRadius;
        public float mouseEventWindForce;
    }

    public class GrassPass : ScriptableRenderPass
    {
        internal struct GrassRenderProperty
        {
            public Vector4 v0;
            public Vector4 v1andv2;
            public Vector4 right;
            public Vector4 color;
            public Vector4 innerForce;
            public static int Size()
            {
                return
                    sizeof(float) * 4 * 5;
            }
        };
        public float GrassHeight
        {
            get
            {
                return meshes[0].bounds.size.y * settings.scaleOverride.y;
            }
        }
        public float GrassWidth
        {
            get
            {
                return meshes[0].bounds.size.x * settings.scaleOverride.x;
            }
        }
        public int GrassNumPerChunk
        {
            get { return chunkGrassSize * chunkGrassSize; }
        }
        internal ProfilingSampler pofilingSampler = new ProfilingSampler("Grass");
        internal Material material;
        internal Mesh[] meshes;
        internal ComputeShader compute;
        internal bool enableInSceneViewPort = false;
        internal int chunkGrassSize;

        internal int LODCount;
        internal int[] LODs;
        internal int chunkNum;
        internal GrassRenderProperty[] initialProperties;
        internal ComputeBuffer meshRenderPropertyBuffer; // store matrics for grass
        internal ComputeBuffer[] drawIndirectArgsBuffers; // store number, lod, grid origin position, etc
        internal GrassChunk[] chunks;
        internal GrassPassSettings settings;

        internal Vector3 mouseClickPos;
        internal int mouseClicked;
        internal Color mouseEventColor;

        public int BufferSize
        {
            get => meshRenderPropertyBuffer != null ? meshRenderPropertyBuffer.count : 0;
        }

        public GrassPass(Material material, Mesh[] meshes, ComputeShader compute, int[] LODs, int chunkGrassSize, GrassPassSettings settings, RenderPassEvent injectionPoint)
        {
            if (meshes.Length != LODs.Length)
            {
                Debug.LogWarning("The mesh and LOD size doesn't match.");
            }
            this.LODCount = Mathf.Min(meshes.Length, LODs.Length);
            this.LODs = LODs;
            this.chunkGrassSize = chunkGrassSize;
            this.chunkNum = 0;
            for (int i = 0; i < LODCount; ++i) { this.chunkNum += LODs[i]; }
            this.material = material;
            this.meshes = meshes;
            this.compute = compute;
            this.settings = settings;
            renderPassEvent = injectionPoint;

            InitializeBuffers();
        }
        public void InitializeBuffers()
        {
            Dispose();
            int kernel = compute.FindKernel("CSMain");
            // Argument buffer used by DrawMeshInstancedIndirect.
            // It has 5 uint values

            drawIndirectArgsBuffers = new ComputeBuffer[LODCount];
            for (int i = 0; i < LODCount; ++i)
            {
                drawIndirectArgsBuffers[i] = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            }

            meshRenderPropertyBuffer = new ComputeBuffer(chunkNum * GrassNumPerChunk, GrassRenderProperty.Size());
            for (int i = 0; i < chunkNum; ++i)
            {
                InitializeChunk(i);
            }
            compute.SetBuffer(kernel, "_Properties", meshRenderPropertyBuffer);
            material.SetBuffer("_Properties", meshRenderPropertyBuffer);
        }
        public void InitializeChunk(int chunkIndex)
        {
            if (meshRenderPropertyBuffer.count == 0)
            {
                Debug.LogError($"No compute buffer is allocated"); return;
            }
            if (chunkIndex >= chunkNum)
            {
                Debug.LogError($"Chunk index exceeds chunk size"); return;
            }
            if (initialProperties == null || initialProperties.Length != GrassNumPerChunk)
            {
                initialProperties = new GrassRenderProperty[GrassNumPerChunk];
                for (int i = 0; i < GrassNumPerChunk; i++)
                {
                    initialProperties[i].v1andv2.x = 1f;
                    initialProperties[i].v1andv2.y = 0f;
                    initialProperties[i].v1andv2.z = 1f;
                    initialProperties[i].v1andv2.w = 0f;
                    initialProperties[i].color = Vector4.one;
                    initialProperties[i].color.w = 0f; // blending
                    initialProperties[i].innerForce = Vector4.zero;
                }
            }
            meshRenderPropertyBuffer.SetData(initialProperties, 0, chunkIndex * GrassNumPerChunk, GrassNumPerChunk);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if ((!enableInSceneViewPort || cameraData.camera.cameraType != CameraType.SceneView) &&
                (cameraData.camera.cameraType != CameraType.Game && cameraData.camera.cameraType != CameraType.Preview))
            {
                return;
            }

            if (material == null)
                return;
            var scaleMat = Matrix4x4.Scale(settings.scaleOverride);
            //var rotMat = Matrix4x4.identity;

            int kernel = compute.FindKernel("CSMain");
            //compute.SetMatrix("_ScaleMat", scaleMat);
            //compute.SetMatrix("_RotMat", rotMat);

            compute.SetVector("_Gravity", Physics.gravity);
            compute.SetVector("_MouseClickPos", mouseClickPos);
            compute.SetInt("_MouseClicked", mouseClicked);
            compute.SetFloat("_MouseEventDecay", settings.mouseEventDecay);
            compute.SetFloat("_MouseEventRadius", settings.mouseEventRadius);
            compute.SetFloat("_MouseEventWindForce", settings.mouseEventWindForce);
            compute.SetVector("_MouseEventColor", mouseEventColor);
            compute.SetFloat("_TimeScale", settings.timeScale);
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

            int chunkCount = 0;
            if (chunks != null)
            {
                foreach (var chunk in chunks)
                {
                    if (++chunkCount > chunkNum) break;
                    //var chunk = chunks[0];
                    compute.SetVector("_GridOrigin", chunk.pos2d);
                    compute.SetFloat("_GridSize", chunk.size / chunkGrassSize);
                    compute.SetInt("_ChunkWidth", chunkGrassSize);
                    compute.SetInt("_PropertiesStartIdx", chunk.index * GrassNumPerChunk);
                    // We used to just be able to use `population` here, but it looks like a Unity update imposed a thread limit (65535) on my device.
                    // This is probably for the best, but we have to do some more calculation.  Divide population by numthreads.x in the compute shader.
                    compute.Dispatch(kernel, Mathf.CeilToInt(GrassNumPerChunk / 64f), 1, 1);
                }
            }
            chunkCount = 0;
            for (int i = 0; i < LODCount; ++i)
            {
                if (LODs[i] <= 0) continue;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, pofilingSampler))
                {
                    // 0: index count per instance, 1: instance count, 2: start index location, 3: base vertex location, 4: start instance location
                    // "start instance location" may act differently on DX11 devices. reference: https://forum.unity.com/threads/graphics-drawmeshinstancedindirect-and-argsoffset.765959/
                    // doesn't work on my machine
                    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                    args[0] = (uint)meshes[i].GetIndexCount(0);
                    args[1] = (uint)(GrassNumPerChunk * LODs[i]);
                    args[2] = (uint)meshes[i].GetIndexStart(0);
                    args[3] = (uint)meshes[i].GetBaseVertex(0);
                    //args[4] = (uint)(chunkCount * GrassNumPerChunk); // doesn't work
                    cmd.SetGlobalInt("_GrassRenderPropertyStartIndex", chunkCount * GrassNumPerChunk);
                    drawIndirectArgsBuffers[i].SetData(args);
                    cmd.DrawMeshInstancedIndirect(meshes[i], 0, material, 0, drawIndirectArgsBuffers[i]);
                    chunkCount += LODs[i];
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
        }
        public void Dispose()
        {
            if (meshRenderPropertyBuffer != null)
            {
                meshRenderPropertyBuffer.Release();
            }
            meshRenderPropertyBuffer = null;

            if (drawIndirectArgsBuffers != null)
            {
                foreach (var buf in drawIndirectArgsBuffers) buf.Release();
            }
            drawIndirectArgsBuffers = null;
        }
    }
    //[ExecuteInEditMode] // cause memeory leak
    public class GrassRenderer : MonoBehaviour
    {
        [SerializeField]
        TerrainCloudRenderer terrainRenderer;
        [SerializeField]
        bool enableInScene = false;
        [SerializeField]
        RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
        [SerializeField]
        Camera cam;
        [SerializeField]
        internal float maxViewDistance = 100f;
        [Range(2, 500)]
        [Tooltip("A chunk will of ChunkGrassSize^2 grass blades.")]
        internal int chunkGrassSize = 250;
        [SerializeField]
        ComputeShader compute;
        [SerializeField]
        Material material; // vert and frag
        [SerializeField]
        internal int[] LOD = new int[3] { 10, 30, 50 };
        [SerializeField]
        internal Mesh[] meshes = new Mesh[3];
        [SerializeField]
        [ColorUsage(true, true)]
        Color[] mouseEventColors = new Color[2] { Color.cyan, Color.blue };
        [SerializeField]
        internal GrassPassSettings settings;

        Plane[] frustumPlanes;
        Vector2[] frustumTriangle = new Vector2[3];
        Vector2[] frustumTriangleLocal = new Vector2[3];

        GrassPass grassPass;
        Dictionary<Vector2Int, int>[] chunkBufferMaps;
        SortedSet<GrassChunk> chunkSet;

        Nullable<Vector3> RayCastPos;

        Color mouseEventColor = Color.white;
        public int DrawNum
        {
            get
            {
                if (grassPass != null && grassPass.chunks != null) return grassPass.chunks.Length * grassPass.GrassNumPerChunk;
                return 0;
            }
        }
        public void ResetRenderPass()
        {
            if (grassPass != null) grassPass.Dispose();
            grassPass = new GrassPass(material, meshes, compute, LOD, chunkGrassSize, settings, injectionPoint);
            GenerateChunks();
        }
        void OnEnable()
        {
            grassPass = new GrassPass(material, meshes, compute, LOD, chunkGrassSize, settings, injectionPoint);
            GenerateChunks();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            grassPass.Dispose();
            grassPass = null;
        }

        private void Update()
        {
            // oriente along camera forward direction
            transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);
            transform.forward = Vector3.Normalize(new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z));

            Profiler.BeginSample("Generate Visible Grass Chunks");
            GenerateChunks();
            Profiler.EndSample();

            Profiler.BeginSample("Mouse Ray March");
            RayCast();
            Profiler.EndSample();

            if (Input.GetMouseButtonDown(0)) mouseEventColor = mouseEventColors[UnityEngine.Random.Range(0, mouseEventColors.Length)];
        }
        void RayCast()
        {
            RayCastPos = null;
            if (Input.GetMouseButton(0))
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = Camera.main.nearClipPlane;
                Vector3 target = cam.ScreenToWorldPoint(mousePos);
                Vector3 dir = Vector3.Normalize(target - cam.transform.position);
                float3 pos = cam.transform.position;
                //if (TerrainData.RayMarch(ref pos, dir, 0.8f, 1000f))
                if (TerrainData.RayMarch(ref pos, dir, terrainRenderer.terrainMaterial.GetFloat("_RayMarchStep"), terrainRenderer.terrainMaterial.GetFloat("_RayMarchMaxDistance")))
                {
                    RayCastPos = (Vector3)pos - dir * 0.1f;
                }
            }
        }
        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            grassPass.settings = settings;
            grassPass.enableInSceneViewPort = enableInScene;
            //Shader.SetGlobalInt("_MouseClicked", RayCastPos.HasValue ? 1 : 0);
            //Shader.SetGlobalVector("_MouseClickPos", RayCastPos.HasValue ? RayCastPos.Value : Vector3.zero);
            grassPass.enableInSceneViewPort = enableInScene;
            grassPass.mouseClicked = RayCastPos.HasValue ? 1 : 0;
            grassPass.mouseClickPos = RayCastPos.HasValue ? RayCastPos.Value : Vector3.zero;
            grassPass.mouseEventColor = mouseEventColor;
            cam.GetUniversalAdditionalCameraData()
                .scriptableRenderer.EnqueuePass(grassPass);
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
            var foo = transform.TransformPoint(minX, 0f, minZ);
            frustumTriangle[1] = new Vector2(foo.x, foo.z);
            foo = transform.TransformPoint(maxX, 0f, minZ);
            frustumTriangle[2] = new Vector2(foo.x, foo.z);

            frustumTriangleLocal[0] = transform.InverseTransformPoint(cam.transform.position);
            frustumTriangleLocal[1] = new Vector2(minX, minZ);
            frustumTriangleLocal[2] = new Vector2(maxX, minZ);
            #endregion

            #region raterize the triangle
            chunkSet = new SortedSet<GrassChunk>();

            void AddChunk(Vector2Int coord)
            {
                var chunk = new GrassChunk(coord, cam, settings.chunkSize, -1);
                if (IsChunkVisible(chunk))
                {
                    chunkSet.Add(chunk);
                }
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
                    for (int x = Mathf.Min(xx1, xx2)-1; x <= Mathf.Max(xx1, xx2)+1; x++)
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
                    for (int x = Mathf.Min(xx1, xx2)-1; x <= Mathf.Max(xx1, xx2)+1; x++)
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
            Profiler.BeginSample("Write Chunks");
            bool firstRun = grassPass.chunks == null;
            //if (grassPass.chunkSize > chunks.Count) // only a few visible chunks
            //{
            //    Debug.LogError($"The chunks generated are less than compute buffer's size.");
            //    return;
            //}
            if (chunkSet.Count > 0)
            {
                if (firstRun)
                {
                    chunkBufferMaps = new Dictionary<Vector2Int, int>[LOD.Length];
                    for (int i = 0; i < LOD.Length; ++i) chunkBufferMaps[i] = new Dictionary<Vector2Int, int>();
                    grassPass.chunks = new GrassChunk[grassPass.chunkNum];
                    int idx = 0;
                    int it = 0;
                    bool reachedEnd = false;
                    for (int i = 0; i < LOD.Length; ++i)
                    {
                        if (!reachedEnd)
                        {
                            int counter = 0;
                            while (true)
                            {
                                if (++counter > LOD[i]) break;
                                var current = chunkSet.ElementAt(it);
                                grassPass.chunks[idx] = current;
                                grassPass.chunks[idx].index = idx;
                                chunkBufferMaps[i].Add(current.grid, idx);
                                idx++;
                                if (++it >= chunkSet.Count)
                                {
                                    reachedEnd = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    int it = 0;
                    int startIdx = 0;
                    bool reachedEnd = false;
                    for (int i = 0; i < LOD.Length; ++i)
                    {
                        var newBufferMap = new Dictionary<Vector2Int, int>();

                        if (!reachedEnd)
                        {
                            var availableIndices = new HashSet<int>(Enumerable.Range(startIdx, LOD[i]));
                            startIdx += LOD[i];
                            var remainings = new HashSet<GrassChunk>();
                            int counter = 0;
                            while (true)
                            {
                                if (++counter > LOD[i]) break;
                                var current = chunkSet.ElementAt(it);
                                if (chunkBufferMaps[i].ContainsKey(current.grid))
                                {
                                    int idx = chunkBufferMaps[i][current.grid];
                                    grassPass.chunks[idx] = current;
                                    grassPass.chunks[idx].index = idx;
                                    availableIndices.Remove(idx);

                                    newBufferMap.Add(current.grid, idx);
                                }
                                else
                                    remainings.Add(current);
                                if (++it >= chunkSet.Count)
                                {
                                    reachedEnd = true;
                                    break;
                                }
                            }
                            foreach (var chunk in remainings)
                            {
                                int idx = availableIndices.First();
                                availableIndices.Remove(idx);
                                grassPass.chunks[idx] = chunk;
                                grassPass.chunks[idx].index = idx;
                                newBufferMap.Add(chunk.grid, idx);
                                //grassPass.InitializeChunk(idx); // largely increase cpu time
                            }
                        }
                        chunkBufferMaps[i] = newBufferMap;
                    }
                }
            }
            Profiler.EndSample();
            #endregion
        }
        private void OnDrawGizmosSelected()
        {
            if (grassPass != null)
            {
                Color wireColor = new Color(1f, 0f, 0f, 0.5f);
                Color cubeColor = new Color(0, 1, 0, 0.5f);

                Gizmos.color = wireColor;
                if (chunkSet != null)
                {
                    foreach (var chunk in chunkSet)
                    {
                        Gizmos.DrawWireCube(new Vector3(chunk.X + chunk.size * 0.5f, (TerrainData.MaxHeight + grassPass.GrassHeight * 2f) * 0.5f, chunk.Y + chunk.size * 0.5f), new Vector3(chunk.size, (TerrainData.MaxHeight + grassPass.GrassHeight * 2f), chunk.size));
                    }
                }
                Gizmos.color = cubeColor;
                //Gizmos.matrix = transform.localToWorldMatrix;
                foreach (var chunk in grassPass.chunks)
                {
                    Gizmos.DrawCube(new Vector3(chunk.X + chunk.size * 0.5f, (TerrainData.MaxHeight + grassPass.GrassHeight * 2f) * 0.5f, chunk.Y + chunk.size * 0.5f), new Vector3(chunk.size, (TerrainData.MaxHeight + grassPass.GrassHeight * 2f), chunk.size));
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

                Gizmos.color = Color.red;
                var frustumCorners = new Vector3[4];
                frustumCorners[0] = cam.ViewportToWorldPoint(new Vector3(0, 0, maxViewDistance));
                frustumCorners[1] = cam.ViewportToWorldPoint(new Vector3(1, 0, maxViewDistance));
                frustumCorners[2] = cam.ViewportToWorldPoint(new Vector3(0, 1, maxViewDistance));
                frustumCorners[3] = cam.ViewportToWorldPoint(new Vector3(1, 1, maxViewDistance));
                Gizmos.DrawSphere(frustumCorners[0], 0.1f);
                Gizmos.DrawSphere(frustumCorners[1], 0.1f);
                Gizmos.DrawSphere(frustumCorners[2], 0.1f);
                Gizmos.DrawSphere(frustumCorners[3], 0.1f);
            }

        }
        bool IsChunkVisible(GrassChunk chunk)
        {
            var bounds = chunk.GetBounds((TerrainData.MaxHeight + grassPass.GrassHeight * 2f));
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