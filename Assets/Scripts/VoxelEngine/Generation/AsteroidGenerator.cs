using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core;
using VoxelEngine.Core.Generators;
using VoxelEngine.Core.Streaming;
using VoxelEngine.Physics;

namespace VoxelEngine.Core.Generators
{
    public class AsteroidGenerator : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Base sphere radius as a fraction of volume world size.")]
        [Range(0.1f, 0.5f)]
        public float radius = 0.4f;

        [Tooltip("Noise spatial frequency. Higher = more bumps.")]
        public float noiseFrequency = 2.0f;

        [Tooltip("Noise displacement amplitude as a fraction of the base radius.")]
        [Range(0.05f, 0.8f)]
        public float noiseAmplitude = 0.3f;

        [Tooltip("Number of FBM octaves for surface detail.")]
        [Range(1, 8)]
        public int noiseOctaves = 4;

        [Header("Material")]
        [Tooltip("VoxelDefinition material index for the asteroid surface.")]
        public int materialID = 1;

        [Header("Volume")]
        [Tooltip("SVO grid resolution (must be power of 2: 16, 32, 64, 128).")]
        public int resolution = 64;

        [Tooltip("World-space size of the asteroid volume.")]
        public float worldSize = 64f;

        [Header("Physics")]
        [Tooltip("Density multiplier for mass calculation (kg per cubic voxel unit).")]
        public float density = 2.5f;

        [Tooltip("Initial linear velocity applied to the asteroid.")]
        public Vector3 initialVelocity = Vector3.zero;

        [Tooltip("Initial angular velocity (spin) applied to the asteroid.")]
        public Vector3 initialAngularVelocity = Vector3.zero;

        [Header("Seed")]
        [Tooltip("Random seed for noise offset. -1 = random on generate.")]
        public int seed = -1;

        [Header("Generation")]
        [Tooltip("If true, generates the asteroid on Start().")]
        public bool generateOnStart = true;

        // Runtime references
        private VoxelVolume _volume;
        private ComputeShader _svoCompute;

        /// <summary>
        /// The spawned VoxelVolume. Null until Generate() completes.
        /// </summary>
        public VoxelVolume Volume => _volume;

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        /// <summary>
        /// Allocate a VoxelVolume, generate asteroid SDF on GPU, then set up physics.
        /// </summary>
        public void Generate()
        {
            if (VoxelVolumePool.Instance == null)
            {
                Debug.LogError("[AsteroidGenerator] VoxelVolumePool not found in scene.");
                return;
            }

            // 1. Allocate empty volume
            _volume = VoxelVolumePool.Instance.GetVolume(
                transform.position,
                worldSize,
                requestedNodes: -1,
                requestedBricks: -1,
                resolution: resolution,
                generateEmpty: true,
                skipGeneration: true
            );

            if (_volume == null)
            {
                Debug.LogError("[AsteroidGenerator] Failed to allocate VoxelVolume from pool.");
                return;
            }

            _volume.gameObject.name = $"Asteroid_{gameObject.name}";
            _volume.IsTransient = true;

            // 2. Cache compute shader reference from the volume
            _svoCompute = _volume.svoCompute;
            if (_svoCompute == null)
            {
                Debug.LogError("[AsteroidGenerator] VoxelVolume has no svoCompute shader assigned.");
                return;
            }

            // 3. Calculate asteroid parameters
            float actualRadius = radius * worldSize;
            float actualAmplitude = noiseAmplitude * actualRadius;

            Vector3 volumeCenter = transform.position + Vector3.one * (worldSize * 0.5f);

            // Seed
            Vector3 seedOffset;
            if (seed < 0)
            {
                seedOffset = new Vector3(
                    Random.Range(-1000f, 1000f),
                    Random.Range(-1000f, 1000f),
                    Random.Range(-1000f, 1000f)
                );
            }
            else
            {
                Random.State oldState = Random.state;
                Random.InitState(seed);
                seedOffset = new Vector3(
                    Random.Range(-1000f, 1000f),
                    Random.Range(-1000f, 1000f),
                    Random.Range(-1000f, 1000f)
                );
                Random.state = oldState;
            }

            // 4. Set asteroid uniforms on compute shader
            _svoCompute.SetVector("_AsteroidCenter", volumeCenter);
            _svoCompute.SetFloat("_AsteroidRadius", actualRadius);
            _svoCompute.SetFloat("_AsteroidNoiseFrequency", noiseFrequency);
            _svoCompute.SetFloat("_AsteroidNoiseAmplitude", actualAmplitude);
            _svoCompute.SetInt("_AsteroidNoiseOctaves", noiseOctaves);
            _svoCompute.SetVector("_AsteroidSeedOffset", seedOffset);
            _svoCompute.SetInt("_AsteroidMaterialID", materialID);

            // 5. Run SVO generation with asteroid mode
            SVOGenerator.Build(
                _svoCompute,
                _volume.BufferManager,
                resolution,
                transform.position,
                worldSize,
                empty: false,
                generationMode: 1
            );

            // 6. Reset generation mode to avoid affecting other volumes
            _svoCompute.SetInt("_GenerationMode", 0);

            // 7. Async readback to get voxel count for mass calculation
            AsyncGPUReadback.Request(_volume.CounterBuffer, (req) =>
            {
                if (req.hasError)
                {
                    Debug.LogWarning("[AsteroidGenerator] GPU readback failed, using estimated mass.");
                    SetupPhysics(resolution * resolution * resolution / 4); // rough estimate
                    return;
                }

                var data = req.GetData<uint>();
                int brickVoxelCount = (int)data[2];
                // Estimate solid voxels from brick voxel count
                // Each brick is 216 storage voxels, ~64 logical. Rough: brickVoxels / 216 * 32 average solid
                int estimatedSolid = Mathf.Max(1, brickVoxelCount / 7);
                SetupPhysics(estimatedSolid);
            });

            // 8. Request physics collider generation
            if (VoxelPhysicsManager.Instance != null)
            {
                VoxelPhysicsManager.Instance.Enqueue(_volume);
            }

            Debug.Log($"[AsteroidGenerator] Generated asteroid '{_volume.gameObject.name}' " +
                      $"at {transform.position}, radius={actualRadius:F1}, res={resolution}");
        }

        private void SetupPhysics(int estimatedSolidVoxels)
        {
            if (_volume == null) return;

            Rigidbody rb = _volume.GetComponent<Rigidbody>();
            if (rb == null)
                rb = _volume.gameObject.AddComponent<Rigidbody>();

            float voxelSize = worldSize / resolution;
            float voxelVolume = voxelSize * voxelSize * voxelSize;
            rb.mass = Mathf.Max(1f, estimatedSolidVoxels * voxelVolume * density);
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;

            // Apply initial velocities
            rb.linearVelocity = initialVelocity;
            rb.angularVelocity = initialAngularVelocity;

            Debug.Log($"[AsteroidGenerator] Physics setup: mass={rb.mass:F1}, " +
                      $"vel={initialVelocity}, angVel={initialAngularVelocity}");
        }

        /// <summary>
        /// Destroy the asteroid volume and return it to the pool.
        /// </summary>
        public void DestroyAsteroid()
        {
            if (_volume != null && VoxelVolumePool.Instance != null)
            {
                VoxelVolumePool.Instance.ReturnVolume(_volume);
                _volume = null;
            }
        }

        private void OnDestroy()
        {
            // Don't auto-destroy the volume — it may still be in the world as debris
        }

        private void OnDrawGizmosSelected()
        {
            // Preview the asteroid sphere in the editor
            float actualRadius = radius * worldSize;
            Vector3 center = transform.position + Vector3.one * (worldSize * 0.5f);

            Gizmos.color = new Color(0.8f, 0.6f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(center, actualRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireCube(
                transform.position + Vector3.one * (worldSize * 0.5f),
                Vector3.one * worldSize
            );
        }
    }
}
