# Voxel Asteroid Generator Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a GPU-based asteroid generator that creates destructible, physics-driven voxel asteroids using sphere+noise SDF, integrated into the existing SVO generation pipeline.

**Architecture:** A new HLSL generator stage (`AsteroidGenerator.hlsl`) defines the asteroid SDF. A `_GenerationMode` uniform in `SVOBuilder.compute` branches between terrain and asteroid pipelines. A C# `AsteroidGenerator` MonoBehaviour orchestrates volume allocation, GPU generation, and physics setup.

**Tech Stack:** Unity 6 URP, HLSL compute shaders, C# MonoBehaviour, existing SVO/VoxelVolume/VoxelVolumePool systems.

---

## Prerequisites

Before starting, ensure:
- The voxel engine has been migrated to LookAtTheSky (already done in previous commit)
- Unity project compiles without errors after opening (URP renderer features may need to be re-added to `Assets/Settings/PC_Renderer.asset` via Unity Inspector)

---

### Task 1: Create AsteroidGenerator.hlsl

**Files:**
- Create: `Assets/Scripts/VoxelEngine/Generation/Compute/Generators/AsteroidGenerator.hlsl`

**Context:** This file defines the asteroid SDF function. It must follow the same pattern as other generators in the same directory (e.g., `TerrainGenerator.hlsl`, `Spheres.hlsl`). It uses the existing `Noise.hlsl` (which provides `snoise()` and `fbm()`) and `GenerationContext.hlsl` (which provides `GenerationContext`, `InitContext()`, `UnionSmooth()`).

**Step 1: Create the HLSL file**

```hlsl
#ifndef ASTEROID_GENERATOR_H
#define ASTEROID_GENERATOR_H

// Uniforms set from C# AsteroidGenerator.cs
float3 _AsteroidCenter;
float _AsteroidRadius;
float _AsteroidNoiseFrequency;
float _AsteroidNoiseAmplitude;
int _AsteroidNoiseOctaves;
float3 _AsteroidSeedOffset;
uint _AsteroidMaterialID;

float AsteroidSDF(float3 worldPos)
{
    float3 p = worldPos - _AsteroidCenter;
    float baseDist = length(p) - _AsteroidRadius;
    
    // FBM noise displacement for uneven surface
    float3 noisePos = worldPos * _AsteroidNoiseFrequency + _AsteroidSeedOffset;
    float noise = fbm(noisePos, _AsteroidNoiseOctaves, 0.5, 2.0, 1.0);
    
    return baseDist + noise * _AsteroidNoiseAmplitude;
}

float3 AsteroidGradient(float3 worldPos)
{
    // Central differences for surface normal
    float eps = 0.5;
    float3 grad;
    grad.x = AsteroidSDF(worldPos + float3(eps, 0, 0)) - AsteroidSDF(worldPos - float3(eps, 0, 0));
    grad.y = AsteroidSDF(worldPos + float3(0, eps, 0)) - AsteroidSDF(worldPos - float3(0, eps, 0));
    grad.z = AsteroidSDF(worldPos + float3(0, 0, eps)) - AsteroidSDF(worldPos - float3(0, 0, eps));
    return normalize(grad);
}

void Stage_Asteroid(inout GenerationContext ctx)
{
    float d = AsteroidSDF(ctx.position);
    float3 grad = AsteroidGradient(ctx.position);
    
    uint mat = _AsteroidMaterialID;
    if (mat == 0) mat = 1;
    
    UnionSmooth(ctx, d, grad, mat, 0.0);
}

GenerationContext RunAsteroidPipeline(float3 worldPos)
{
    GenerationContext ctx;
    InitContext(ctx, worldPos);
    Stage_Asteroid(ctx);
    return ctx;
}

#endif
```

**Step 2: Verify file exists at correct path**

Check that the file is at `Assets/Scripts/VoxelEngine/Generation/Compute/Generators/AsteroidGenerator.hlsl` alongside the other generator HLSL files.

**Step 3: Commit**

```bash
git add Assets/Scripts/VoxelEngine/Generation/Compute/Generators/AsteroidGenerator.hlsl
git commit -m "feat(voxel): add AsteroidGenerator.hlsl with sphere+FBM SDF"
```

---

### Task 2: Integrate into GeneratorPipeline.hlsl

**Files:**
- Modify: `Assets/Scripts/VoxelEngine/Generation/Compute/GeneratorPipeline.hlsl`

**Context:** This file includes all generator stages and defines `RunGeneratorPipeline()`. We need to add an include for the new asteroid generator. The `RunAsteroidPipeline()` function is already defined in `AsteroidGenerator.hlsl`, so we just need the include.

**Step 1: Add the include**

After line 9 (`#include "./Generators/OakTrees.hlsl"`), add:

```hlsl
#include "./Generators/AsteroidGenerator.hlsl"
```

**Step 2: Commit**

```bash
git add Assets/Scripts/VoxelEngine/Generation/Compute/GeneratorPipeline.hlsl
git commit -m "feat(voxel): include AsteroidGenerator in generation pipeline"
```

---

### Task 3: Add generation mode branching to SVOBuilder.compute

**Files:**
- Modify: `Assets/Scripts/VoxelEngine/Generation/Compute/SVOBuilder.compute`

**Context:** The `BuildBricks` kernel currently always calls `RunGeneratorPipeline()`. We need to add a `_GenerationMode` uniform and branch to `RunAsteroidPipeline()` when mode is 1.

**Step 1: Add the uniform**

After line 35 (`int _NumDynamicObjects;`), add:

```hlsl
int _GenerationMode; // 0 = terrain (default), 1 = asteroid
```

**Step 2: Add branching in BuildBricks**

Replace the two locations where `RunGeneratorPipeline` is called in `BuildBricks`.

At line 179 (the center sample), change:

```hlsl
    GenerationContext centerCtx = RunGeneratorPipeline(brickCenterWorld, activeObjects, 0);
```

to:

```hlsl
    GenerationContext centerCtx;
    if (_GenerationMode == 1)
    {
        centerCtx = RunAsteroidPipeline(brickCenterWorld);
    }
    else
    {
        centerCtx = RunGeneratorPipeline(brickCenterWorld, activeObjects, 0);
    }
```

At line 203 (pass 1 uniformity check voxel sample), change:

```hlsl
                    GenerationContext vCtx = RunGeneratorPipeline(voxelPosWorld, activeObjects, 0);
```

to:

```hlsl
                    GenerationContext vCtx;
                    if (_GenerationMode == 1)
                    {
                        vCtx = RunAsteroidPipeline(voxelPosWorld);
                    }
                    else
                    {
                        vCtx = RunGeneratorPipeline(voxelPosWorld, activeObjects, 0);
                    }
```

At line 256 (pass 2 allocation voxel sample), change:

```hlsl
                    GenerationContext voxelCtx = RunGeneratorPipeline(voxelPosWorld, activeObjects, 0);
```

to:

```hlsl
                    GenerationContext voxelCtx;
                    if (_GenerationMode == 1)
                    {
                        voxelCtx = RunAsteroidPipeline(voxelPosWorld);
                    }
                    else
                    {
                        voxelCtx = RunGeneratorPipeline(voxelPosWorld, activeObjects, 0);
                    }
```

**Important:** When `_GenerationMode == 1`, the `ApplyEdits` and `ApplyDynamicObjects` calls after the pipeline still run. Edits should be skipped for asteroids (they won't have terrain edits). Dynamic objects can be kept (they allow SDF carving). To skip edits cleanly, the existing `if (_EditCount == 0) return;` guard in `ApplyEdits` already handles this — asteroids won't have edits registered.

**Step 3: Commit**

```bash
git add Assets/Scripts/VoxelEngine/Generation/Compute/SVOBuilder.compute
git commit -m "feat(voxel): add generation mode branching for asteroid pipeline in SVOBuilder"
```

---

### Task 4: Add generationMode parameter to SVOGenerator.cs

**Files:**
- Modify: `Assets/Scripts/VoxelEngine/Generation/SVOGenerator.cs`

**Context:** The C# `SVOGenerator.Build()` static method dispatches the compute shader. We need to add a `generationMode` parameter and set the `_GenerationMode` uniform before dispatch.

**Step 1: Modify the Build method signature**

Change line 25:

```csharp
public static void Build(ComputeShader shader, SVOBufferManager buffers, int resolution, Vector3 chunkOrigin, float chunkSize, bool empty = false)
```

to:

```csharp
public static void Build(ComputeShader shader, SVOBufferManager buffers, int resolution, Vector3 chunkOrigin, float chunkSize, bool empty = false, int generationMode = 0)
```

**Step 2: Set the uniform before dispatch**

After line 74 (`shader.SetFloat("_ChunkWorldSize", chunkSize);`), add:

```csharp
            shader.SetInt("_GenerationMode", generationMode);
```

**Step 3: Commit**

```bash
git add Assets/Scripts/VoxelEngine/Generation/SVOGenerator.cs
git commit -m "feat(voxel): add generationMode parameter to SVOGenerator.Build()"
```

---

### Task 5: Create AsteroidGenerator.cs MonoBehaviour

**Files:**
- Create: `Assets/Scripts/VoxelEngine/Generation/AsteroidGenerator.cs`

**Context:** This MonoBehaviour allocates a VoxelVolume, sets asteroid-specific uniforms, triggers GPU generation, then sets up physics. It uses:
- `VoxelVolumePool.Instance.GetVolume()` — allocates a volume from the pool (see `Assets/Scripts/VoxelEngine/Streaming/VoxelVolumePool.cs:494`)
- `SVOGenerator.Build()` — dispatches GPU generation (see `Assets/Scripts/VoxelEngine/Generation/SVOGenerator.cs:25`)
- `VoxelPhysicsManager.Instance.Enqueue()` — generates marching cubes collider (see `Assets/Scripts/VoxelEngine/Physics/VoxelPhysicsManager.cs`)
- `AsyncGPUReadback` — reads counter buffer for voxel count / mass calculation

**Step 1: Create the MonoBehaviour**

```csharp
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
```

**Step 2: Verify the file compiles**

The file references these types from the existing engine:
- `VoxelVolume` from `VoxelEngine.Core` (Memory/VoxelVolume.cs)
- `SVOGenerator` from `VoxelEngine.Core.Generators` (Generation/SVOGenerator.cs)
- `VoxelVolumePool` from `VoxelEngine.Core.Streaming` (Streaming/VoxelVolumePool.cs)
- `VoxelPhysicsManager` from `VoxelEngine.Physics` (Physics/VoxelPhysicsManager.cs)

**Step 3: Commit**

```bash
git add Assets/Scripts/VoxelEngine/Generation/AsteroidGenerator.cs
git commit -m "feat(voxel): add AsteroidGenerator MonoBehaviour for spawning voxel asteroids"
```

---

### Task 6: Verify full integration

**Steps:**

1. Open Unity, let it recompile
2. Check Console for compilation errors — fix any if present
3. Create a test scene or use existing scene
4. Ensure the scene has: `VoxelVolumePool` (with SVOVolume prefab assigned, pool size > 0), `VoxelPhysicsManager`
5. Create an empty GameObject, add `AsteroidGenerator` component
6. Set `generateOnStart = true`, `resolution = 64`, `worldSize = 64`
7. Ensure a `VoxelRaytraceFeature` and `VoxelCompositeFeature` are added to the URP renderer (in `Assets/Settings/PC_Renderer.asset`, Add Renderer Feature in Inspector)
8. Enter Play mode — asteroid should appear as a roughly spherical voxel volume
9. Verify: volume is visible (raytraced), has Rigidbody, has mesh collider after a moment
10. Test editing: if TerrainEditorTool is set up, subtract voxels from the asteroid and verify debris splitting works

**Step: Commit any fixes**

```bash
git add -A
git commit -m "fix(voxel): resolve integration issues with asteroid generator"
```

---

## Post-Implementation Notes

### URP Renderer Feature Setup (Manual, in Unity Editor)

The voxel renderer features must be added to the URP renderer asset manually:

1. Select `Assets/Settings/PC_Renderer.asset` in Inspector
2. Click "Add Renderer Feature"
3. Add: `VoxelRaytraceFeature` — assign `VoxelRaytracer.compute`, `VoxelEdgeBlend.shader`, blue noise texture (`stbn_scalar_2Dx1Dx1D_128x128x64x1_0.png`)
4. Add: `VoxelCompositeFeature` — assign `VoxelComposite.shader`, `VoxelFXAA.shader`
5. Optionally add: `VoxelTAAFeature`, `VoxelGodRaysFeature`, `VoxelVegetationFeature`

### Scene Setup Requirements

A working asteroid scene needs these GameObjects:
- **VoxelVolumePool** — MonoBehaviour with SVOVolume prefab, pool size, global buffer allocation
- **VoxelPhysicsManager** — MonoBehaviour for async collider generation
- **VoxelDefinitionManager** — ScriptableObjectSingleton loaded from `Resources/VoxelDefinitionManager`
- **Camera** with URP rendering (automatic with PC_Renderer)
- **AsteroidGenerator** — the new component
