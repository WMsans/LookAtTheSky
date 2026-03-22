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
