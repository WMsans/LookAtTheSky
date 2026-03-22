# Voxel Asteroid Generator Design

## Overview

A GPU-based asteroid generator that creates destructible voxel asteroids as standalone debris volumes. Each asteroid is a `VoxelVolume` with `IsTransient = true`, a Rigidbody (no gravity), and a marching-cubes mesh collider. The existing connectivity system handles fracturing when asteroids are damaged.

## Requirements

- Asteroids are standalone free-floating voxel volumes (space debris, not embedded in terrain)
- Shape: sphere base + 3D FBM noise displacement for uneven surface
- Scale: chunk-sized (resolution 64-128, worldSize 64-128)
- Single configurable rock material per asteroid
- Physics from start: Rigidbody with no gravity, configurable initial velocity and angular velocity
- Fully destructible: when damaged, disconnected pieces split off as new debris via existing connectivity system
- Generator component persists as a controller/reference to the spawned asteroid

## Architecture

### Approach: New Generator Stage in Existing Pipeline

Add `AsteroidGenerator.hlsl` as a new generation stage. A `_GenerationMode` integer uniform in `SVOBuilder.compute` branches between terrain pipeline (mode 0, default) and asteroid pipeline (mode 1). This keeps the existing terrain path untouched.

In `BuildBricks`, the branch is:
```hlsl
GenerationContext ctx;
if (_GenerationMode == 1)
    ctx = RunAsteroidPipeline(brickCenterWorld);
else
    ctx = RunGeneratorPipeline(brickCenterWorld, activeObjects, 0);
```

### Asteroid SDF

Defined in `AsteroidGenerator.hlsl`:

```
SDF(p) = length(p - center) - radius + fbm(p * frequency + seedOffset) * amplitude
```

- `center` = volume midpoint (half of chunk world size added to chunk origin)
- `radius` = configurable, default ~40% of chunk size
- `fbm()` = existing `fbm(float3, octaves, persistence, lacunarity, scale)` from `Noise.hlsl`
- `frequency` = controls bump scale (higher = more craters/detail)
- `amplitude` = controls displacement depth (roughness)
- `seedOffset` = `float3` offset for uniqueness per asteroid

Surface normals computed via central differences of the SDF.

### GPU Uniforms

| Uniform | Type | Default | Purpose |
|---------|------|---------|---------|
| `_AsteroidCenter` | float3 | volume midpoint | SDF center |
| `_AsteroidRadius` | float | 0.4 * chunkSize | Base sphere radius |
| `_AsteroidNoiseFrequency` | float | 2.0 | Noise spatial frequency |
| `_AsteroidNoiseAmplitude` | float | 0.3 * radius | Displacement amplitude |
| `_AsteroidNoiseOctaves` | int | 4 | FBM octave count |
| `_AsteroidSeedOffset` | float3 | random | Per-asteroid seed |
| `_AsteroidMaterialID` | uint | 1 | VoxelDefinition index |
| `_GenerationMode` | int | 0 | 0=terrain, 1=asteroid |

## Components

### AsteroidGenerator.cs (MonoBehaviour)

Located in `Scripts/VoxelEngine/Generation/`. Persists as asteroid controller.

```
[Header("Shape")]
float radius = 0.4f;           // Fraction of volume size
float noiseFrequency = 2.0f;
float noiseAmplitude = 0.3f;    // Fraction of radius
int noiseOctaves = 4;

[Header("Material")]
int materialID = 1;

[Header("Volume")]
int resolution = 64;            // 64 or 128
float worldSize = 64f;

[Header("Physics")]
float density = 2.5f;
Vector3 initialVelocity;
Vector3 initialAngularVelocity;

[Header("Seed")]
int seed = -1;                  // -1 = random
```

### Generation Flow

1. `Generate()` called (or on `Start()`)
2. Allocate volume: `VoxelVolumePool.Instance.GetVolume(position, worldSize, -1, -1, resolution, generateEmpty: true, skipGeneration: true)`
3. Set asteroid uniforms on `svoCompute`
4. Set `_GenerationMode = 1`
5. Call `SVOGenerator.Build(shader, buffers, resolution, origin, worldSize, empty: false, generationMode: 1)`
6. Reset `_GenerationMode = 0`
7. Mark `volume.IsTransient = true`
8. Async GPU readback of counters to calculate mass
9. Add Rigidbody: mass from voxel count, useGravity=false, drag=0, angularDrag=0.05
10. Set initial velocity/angular velocity
11. Enqueue to `VoxelPhysicsManager` for mesh collider generation

### Physics Setup

- Mass: `solidVoxelCount * (worldSize/resolution)^3 * density`
- Gravity: disabled
- Drag: 0 (space)
- Angular drag: 0.05 (slight spin damping)
- Collider: marching cubes MeshCollider via existing VoxelPhysicsManager

## Debris / Connectivity Integration

No changes to the existing connectivity system. When `IsTransient = true`:

1. `StructuralIntegrityAnalyzer` uses island detection (multi-component analysis) rather than ground-based connectivity
2. When voxels are removed, the analyzer detects disconnected islands by converged labels
3. The largest island stays in the original volume
4. Smaller islands are extracted by `StructuralCleaner` into new debris volumes with Rigidbodies
5. Recursive splitting works: further damage to fragments triggers re-analysis

The ground threshold is not applied to transient volumes -- the analyzer groups by label convergence instead.

## Files to Create

1. **`Assets/Scripts/VoxelEngine/Generation/Compute/Generators/AsteroidGenerator.hlsl`** -- GPU asteroid SDF function
2. **`Assets/Scripts/VoxelEngine/Generation/AsteroidGenerator.cs`** -- MonoBehaviour spawner/controller

## Files to Modify

3. **`Assets/Scripts/VoxelEngine/Generation/Compute/SVOBuilder.compute`** -- Add `_GenerationMode` uniform, branch in `BuildBricks`
4. **`Assets/Scripts/VoxelEngine/Generation/Compute/GeneratorPipeline.hlsl`** -- Add `#include` for AsteroidGenerator.hlsl, add `RunAsteroidPipeline()` function
5. **`Assets/Scripts/VoxelEngine/Generation/SVOGenerator.cs`** -- Add `generationMode` parameter to `Build()`, set uniform before dispatch

## Unchanged Systems

- StructuralIntegrityAnalyzer / StructuralCleaner (debris works as-is with IsTransient=true)
- VoxelVolumePool (GetVolume API already supports empty generation + skip)
- VoxelPhysicsManager (collider generation works as-is)
- Rendering pipeline (volumes auto-register via VoxelVolumeRegistry.Register on OnEnable)
- VFX debris particles (triggered by existing VoxelVFXManager)
