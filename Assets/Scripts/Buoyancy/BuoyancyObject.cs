using System.Collections.Generic;
using UnityEngine;

namespace BuoyancyScope
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(MeshFilter))]
    public class BuoyancyObject : MonoBehaviour
    {
        public bool debugForce;
        public bool debugVoxels;

        [SerializeField]
        private bool calculateDensity;

        [SerializeField]
        private float density = 0.75f;

        [SerializeField]
        [Range(0f, 1f)]
        private float normalizedVoxelSize = 0.5f;

        [SerializeField]
        private float dragInWater = 1f;

        [SerializeField]
        private float angularDragInWater = 1f;

        private static WaterWaves _water;
        private List<Collider> _colliders;
        private Rigidbody _rigidbody;
        private float _initDrag;
        private float _initAngularDrag;
        private Vector3 _voxelSize;
        private Vector3[] _voxels;

        protected void Awake()
        {
            if (!BuoyancyObject._water) BuoyancyObject._water = FindObjectOfType<WaterWaves>();
            _colliders = new List<Collider>(GetComponents<Collider>());
            _rigidbody = GetComponent<Rigidbody>();
            _initDrag = _rigidbody.drag;
            _initAngularDrag = _rigidbody.angularDrag;

            if (calculateDensity)
            {
                density = _rigidbody.mass / MathfUtils.CalculateVolume_Mesh(GetComponent<MeshFilter>().mesh, transform);
            }
        }

        protected void Start()
        {
            _voxels = Voxelization();
        }

        protected void FixedUpdate()
        {
            Solver();
        }

        protected void Solver()
        {
            if (BuoyancyObject._water != null && _voxels.Length > 0)
            {
                Vector3 forceAtSingleVoxel = CalculateMaxBuoyancyForce() / _voxels.Length;
                Bounds bounds = _colliders[0].bounds;
                float voxelHeight = bounds.size.y * normalizedVoxelSize;

                float submergedVolume = 0f;
                for (int i = 0; i < _voxels.Length; i++)
                {
                    Vector3 worldPoint = transform.TransformPoint(_voxels[i]);

                    float waterLevel = BuoyancyObject._water.GetWaterLevel(worldPoint);
                    float deepLevel = waterLevel - worldPoint.y + (voxelHeight / 2f); // How deep is the voxel                    
                    float submergedFactor = Mathf.Clamp(deepLevel / voxelHeight, 0f, 1f); // 0 - voxel is fully out of the water, 1 - voxel is fully submerged
                    submergedVolume += submergedFactor;

                    Vector3 surfaceNormal = BuoyancyObject._water.GetSurfaceNormal(worldPoint);
                    Quaternion surfaceRotation = Quaternion.FromToRotation(BuoyancyObject._water.transform.up, surfaceNormal);
                    surfaceRotation = Quaternion.Slerp(surfaceRotation, Quaternion.identity, submergedFactor);

                    Vector3 finalVoxelForce = surfaceRotation * (forceAtSingleVoxel * submergedFactor);
                    _rigidbody.AddForceAtPosition(finalVoxelForce, worldPoint);

                    if (debugForce) Debug.DrawLine(worldPoint, worldPoint + finalVoxelForce.normalized, Color.blue);
                }

                submergedVolume /= _voxels.Length; // 0 - object is fully out of the water, 1 - object is fully submerged

                _rigidbody.drag = Mathf.Lerp(_initDrag, dragInWater, submergedVolume);
                _rigidbody.angularDrag = Mathf.Lerp(_initAngularDrag, angularDragInWater, submergedVolume);
            }
        }

        protected void OnDrawGizmos()
        {
            if (debugVoxels && _voxels != null)
            {
                for (int i = 0; i < _voxels.Length; i++)
                {
                    Gizmos.color = Color.magenta - new Color(0f, 0f, 0f, 0.75f);
                    Gizmos.DrawCube(transform.TransformPoint(_voxels[i]), _voxelSize * 0.8f);
                }
            }
        }

        private Vector3 CalculateMaxBuoyancyForce()
        {
            float objectVolume = _rigidbody.mass / density;
            Vector3 maxBuoyancyForce = BuoyancyObject._water.Density * objectVolume * -Physics.gravity;

            return maxBuoyancyForce;
        }

        private Vector3[] Voxelization()
        {
            // record world data
            Vector3 initialScale = transform.lossyScale;
            Quaternion initialRotation = transform.rotation;
            Vector3 initialPosition = transform.position;
            Vector3 initialLocalScale = transform.localScale;

            // back to local   
            Vector3 parentScale = transform.parent.localScale;
            transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            Physics.SyncTransforms();

            Bounds bounds = _colliders[0].bounds;
            _voxelSize.x = bounds.size.x * normalizedVoxelSize;
            _voxelSize.y = bounds.size.y * normalizedVoxelSize;
            _voxelSize.z = bounds.size.z * normalizedVoxelSize;
            int voxelsCount = Mathf.RoundToInt(1f / normalizedVoxelSize);
            List<Vector3> voxels = new List<Vector3>(voxelsCount * voxelsCount * voxelsCount);

            for (int i = 0; i < voxelsCount; i++)
            {
                for (int j = 0; j < voxelsCount; j++)
                {
                    for (int k = 0; k < voxelsCount; k++)
                    {
                        float pX = bounds.min.x + _voxelSize.x * (0.5f + i);
                        float pY = bounds.min.y + _voxelSize.y * (0.5f + j);
                        float pZ = bounds.min.z + _voxelSize.z * (0.5f + k);
                        Vector3 point = new Vector3(pX, pY, pZ);
                        if (PointIsInsideCollider(_colliders[0], ref point))
                        {
                            voxels.Add(point);
                        }
                    }
                }
            }

            // transform to world
            transform.rotation = initialRotation;
            transform.position = initialPosition;
            transform.localScale = initialLocalScale;
            _voxelSize.x *= initialScale.x;
            _voxelSize.y *= initialScale.y;
            _voxelSize.z *= initialScale.z;
            return voxels.ToArray();
        }

        private bool PointIsInsideCollider(Collider c, ref Vector3 p)
        {
            var cp = Physics.ClosestPoint(p, c, Vector3.zero, Quaternion.identity);
            return Vector3.Distance(cp, p) < 0.01f;
        }

        private void VoxelBounds(ref Bounds bounds)
        {
            bounds.SetMinMax(Vector3.zero, Vector3.zero);
            foreach (var nextCollider in _colliders)
            {
                bounds.Encapsulate(nextCollider.bounds);
            }
        }
    }
}
