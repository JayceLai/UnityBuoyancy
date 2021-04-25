using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BuoyancyScope
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter))]
    public class WaterWaves : MonoBehaviour
    {
        [Header("Water Wave")]
        public bool useJobs = false;

        [SerializeField]
        private float speed = 1f;

        [SerializeField]
        private float height = 0.2f;

        [SerializeField]
        private float noiseWalk = 0.5f;

        [SerializeField]
        private float noiseStrength = 0.1f;

        [Header("Water Volume")]
        [SerializeField]
        private float density = 1f;
        public float Density { get { return density; } }
        [SerializeField]
        private int rows = 10;

        [SerializeField]
        private int columns = 10;

        [SerializeField]
        private float quadSegmentSize = 1f;

        [Header("Water Debug")]
        public bool debugMesh;
        public bool debugNormal;
        public bool debugTriangleSearch;
        public Transform debugTransformForTrinagleSearch;

        private Mesh mesh;
        private Vector3[] baseVertices;
        private Vector3[] vertices;
        private Vector3[] worldVertices;
        private NativeArray<Vector3> baseVerticesNative;
        private NativeArray<Vector3> verticesNative;
        private NativeArray<Vector3> worldVerticesNative;

        protected void Awake()
        {
            ResizeBoxCollider();
            mesh = GetComponent<MeshFilter>().mesh;
            mesh.MarkDynamic();
            if (useJobs)
            {
                baseVerticesNative = new NativeArray<Vector3>(mesh.vertices, Allocator.Persistent);
                verticesNative = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Persistent);
                worldVerticesNative = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Persistent);
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    worldVerticesNative[i] = transform.TransformPoint(baseVerticesNative[i]);
                }
            }
            else
            {
                baseVertices = mesh.vertices;
                vertices = new Vector3[mesh.vertexCount];
                worldVertices = new Vector3[mesh.vertexCount];
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    worldVertices[i] = transform.TransformPoint(baseVertices[i]);
                }
            }
        }

        protected void Update()
        {
            if (useJobs)
            {
                WaterWavesJob wavesJob = new WaterWavesJob()
                {
                    vertices = verticesNative,
                    baseVertices = baseVerticesNative,
                    worldVertices = worldVerticesNative,
                    time = Time.time,
                    speed = speed,
                    height = height,
                    noiseWalk = noiseWalk,
                    noiseStrength = noiseStrength,
                    localScaleY = transform.localScale.y,
                    worldMat = transform.localToWorldMatrix,
                };
                wavesJob.Schedule(verticesNative.Length, 64).Complete();
                mesh.SetVertices(verticesNative);
                mesh.RecalculateNormals();
            }
            else
            {
                for (var i = 0; i < vertices.Length; i++)
                {
                    var vertex = baseVertices[i];
                    vertex.y +=
                        Mathf.Sin(Time.time * speed + baseVertices[i].x + baseVertices[i].y + baseVertices[i].z) *
                        (height / transform.localScale.y);

                    vertex.y += Mathf.PerlinNoise(baseVertices[i].x + noiseWalk, baseVertices[i].y /*+ Mathf.Sin(Time.time * 0.1f)*/) * noiseStrength;

                    vertices[i] = vertex;
                    worldVertices[i] = transform.TransformPoint(vertex);
                }
                mesh.SetVertices(vertices);
                mesh.RecalculateNormals();
            }
        }

        protected void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(GetComponent<BoxCollider>().center, GetComponent<BoxCollider>().size);
        }

        protected void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = Color.cyan - new Color(0f, 0f, 0f, 0.75f);
                Gizmos.matrix = transform.localToWorldMatrix;

                Gizmos.DrawCube(GetComponent<BoxCollider>().center - Vector3.up * 0.01f, GetComponent<BoxCollider>().size);

                Gizmos.color = Color.cyan - new Color(0f, 0f, 0f, 0.5f);
                Gizmos.DrawWireCube(GetComponent<BoxCollider>().center, GetComponent<BoxCollider>().size);

                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                // Debug sufrace normal
                if (debugNormal)
                {
                    int[] triangles = mesh.triangles;
                    if (useJobs)
                    {
                        NativeArray<Vector3> vertices = worldVerticesNative;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Gizmos.color = Color.white;
                            Gizmos.DrawLine(vertices[triangles[i + 0]], vertices[triangles[i + 1]]);
                            Gizmos.DrawLine(vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                            Gizmos.DrawLine(vertices[triangles[i + 2]], vertices[triangles[i + 0]]);

                            Vector3 center = MathfUtils.GetAveratePoint(vertices[triangles[i + 0]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                            Vector3 normal = GetSurfaceNormal(center);

                            Gizmos.color = Color.green;
                            Gizmos.DrawLine(center, center + normal);
                        }
                    }
                    else
                    {
                        var vertices = worldVertices;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Gizmos.color = Color.white;
                            Gizmos.DrawLine(vertices[triangles[i + 0]], vertices[triangles[i + 1]]);
                            Gizmos.DrawLine(vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                            Gizmos.DrawLine(vertices[triangles[i + 2]], vertices[triangles[i + 0]]);

                            Vector3 center = MathfUtils.GetAveratePoint(vertices[triangles[i + 0]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                            Vector3 normal = GetSurfaceNormal(center);

                            Gizmos.color = Color.green;
                            Gizmos.DrawLine(center, center + normal);
                        }
                    }
                }

                // Debug mesh vertices
                if (debugMesh)
                {
                    if (worldVertices != null)
                    {
                        for (int i = 0; i < worldVertices.Length; i++)
                        {
                            DebugUtils.DrawPoint(worldVertices[i], Color.red);
                        }
                    }
                    else if (worldVerticesNative != null)
                    {
                        for (int i = 0; i < worldVerticesNative.Length; i++)
                        {
                            DebugUtils.DrawPoint(worldVerticesNative[i], Color.red);
                        }
                    }
                }

                // Test GetSurroundingTrianglePolygon
                if (debugTriangleSearch)
                {
                    if (debugTransformForTrinagleSearch != null)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawSphere(debugTransformForTrinagleSearch.position, 0.1f);
                        var point = debugTransformForTrinagleSearch.position;
                        var triangle = GetSurroundingTrianglePolygon(point);
                        if (triangle != null)
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawLine(triangle[0], triangle[1]);
                            Gizmos.DrawLine(triangle[1], triangle[2]);
                            Gizmos.DrawLine(triangle[2], triangle[0]);
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (useJobs)
            {
                if (verticesNative.IsCreated) verticesNative.Dispose();
                if (baseVerticesNative.IsCreated) baseVerticesNative.Dispose();
                if (worldVerticesNative.IsCreated) worldVerticesNative.Dispose();
            }
        }

        private void ResizeBoxCollider()
        {
            var boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                Vector3 center = boxCollider.center;
                center.y = boxCollider.size.y / -2f;
                center.y += (height + noiseStrength) / transform.localScale.y;

                boxCollider.center = center;
            }
        }

        struct WaterWavesJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Vector3> baseVertices;
            public NativeArray<Vector3> vertices;
            public NativeArray<Vector3> worldVertices;

            [ReadOnly]
            public float speed;

            [ReadOnly]
            public float noiseWalk;

            [ReadOnly]
            public float noiseStrength;

            [ReadOnly]
            public float time;

            [ReadOnly]
            public float height;

            [ReadOnly]
            public float localScaleY;

            [ReadOnly]
            public Matrix4x4 worldMat;

            public void Execute(int index)
            {
                var vertex = baseVertices[index];
                vertex.y += math.sin(time * speed + vertex.x + vertex.y + vertex.z) * (height / localScaleY);
                // float noiseValue = Noise(vertex.x + noiseWalk, vertex.y);
                vertex.y += Noise(vertex.x + noiseWalk, vertex.y /*+ Mathf.Sin(Time.time * 0.1f)*/) * noiseStrength;
                vertices[index] = vertex;
                var point = new Vector4(vertex.x, vertex.y, vertex.z, 1.0f);
                worldVertices[index] = worldMat * point;
            }

            private float Noise(float x, float y)
            {
                float2 pos = Unity.Mathematics.math.float2(x, y);
                return noise.snoise(pos);
            }
        }

        private int GetIndex(int row, int column)
        {
            return row * columns + row + column;
        }

        public Vector3 GetSurfaceNormal(Vector3 worldPoint)
        {
            Vector3[] meshPolygon = GetSurroundingTrianglePolygon(worldPoint);
            if (meshPolygon != null)
            {
                Vector3 planeV1 = meshPolygon[1] - meshPolygon[0];
                Vector3 planeV2 = meshPolygon[2] - meshPolygon[0];
                Vector3 planeNormal = Vector3.Cross(planeV1, planeV2).normalized;
                if (planeNormal.y < 0f)
                {
                    planeNormal *= -1f;
                }

                return planeNormal;
            }

            return transform.up;
        }

        public float GetWaterLevel(Vector3 worldP)
        {
            Vector3[] meshPolygon = GetSurroundingTrianglePolygon(worldP);
            if (meshPolygon != null)
            {
                Vector3 p1 = meshPolygon[1] - meshPolygon[0];
                Vector3 p2 = meshPolygon[2] - meshPolygon[0];
                Vector3 pNorm = Vector3.Cross(p1, p2).normalized;
                if (pNorm.y < 0f)
                {
                    pNorm *= -1f;
                }
                float yOnWaterSurface = (-(worldP.x * pNorm.x) - (worldP.z * pNorm.z) + Vector3.Dot(meshPolygon[0], pNorm)) / pNorm.y;
                return yOnWaterSurface;
            }

            return transform.position.y;
        }

        public Vector3[] GetSurroundingTrianglePolygon(Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            int x = Mathf.CeilToInt(localPoint.x / quadSegmentSize);
            int z = Mathf.CeilToInt(localPoint.z / quadSegmentSize);
            if (x <= 0 || z <= 0 || x >= (columns + 1) || z >= (rows + 1))
            {
                return null;
            }

            Vector3[] trianglePolygon = new Vector3[3];
            if (useJobs)
            {
                if ((worldPoint - worldVerticesNative[GetIndex(z, x)]).sqrMagnitude <
                    ((worldPoint - worldVerticesNative[GetIndex(z - 1, x - 1)]).sqrMagnitude))
                {
                    trianglePolygon[0] = worldVerticesNative[GetIndex(z, x)];
                }
                else
                {
                    trianglePolygon[0] = worldVerticesNative[GetIndex(z - 1, x - 1)];
                }
                trianglePolygon[1] = worldVerticesNative[GetIndex(z - 1, x)];
                trianglePolygon[2] = worldVerticesNative[GetIndex(z, x - 1)];
            }
            else
            {
                if ((worldPoint - worldVertices[GetIndex(z, x)]).sqrMagnitude <
                    ((worldPoint - worldVertices[GetIndex(z - 1, x - 1)]).sqrMagnitude))
                {
                    trianglePolygon[0] = worldVertices[GetIndex(z, x)];
                }
                else
                {
                    trianglePolygon[0] = worldVertices[GetIndex(z - 1, x - 1)];
                }
                trianglePolygon[1] = worldVertices[GetIndex(z - 1, x)];
                trianglePolygon[2] = worldVertices[GetIndex(z, x - 1)];
            }

            return trianglePolygon;
        }
    }
}
