#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float Time;
float3 BaseColor;
float3 BandColor1;
float3 BandColor2;

sampler2D SpriteTextureSampler : register(s0);

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + float2(1.0, 0.0));
    float c = hash(i + float2(0.0, 1.0));
    float d = hash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * noise(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

// Clockwise rotation helper
float2 rot(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

// Hexagon signed distance (approx) for a hex centered at origin.
// Returns ~0 at boundary, negative inside, positive outside.
float sdHex(float2 p, float r)
{
    // Inigo Quilez style hex SDF
    const float3 k = float3(-0.8660254, 0.5, 0.5773503);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    float2 center = float2(0.5, 0.5);
    float2 pos = uv - center;
    float dist = length(pos) * 2.0;
    
    if (dist > 1.0)
        discard;

    // Normalized radius from center of disk
    float r = saturate(dist);
    float angle = atan2(pos.y, pos.x);

    // --- Base gas-giant look: circular banded storms with differential rotation ---
    // Differential rotation: inner rotates faster, outer slower.
    // r is in [0..1]. Use a curve to keep outer almost still.
    float spinRate = lerp(1.0, 0.15, pow(r, 1.6));
    float spin = Time * 0.18 * spinRate;

    // Rotate sampling space to create the rotating storm field
    float2 pRot = rot(pos, spin);

    // Build circular bands from radius, then warp with noise
    float bandFreq = 12.0;
    float warp = (fbm(pRot * 6.0 + float2(Time * 0.02, -Time * 0.015), 4) - 0.5) * 0.18;

    // Add subtle angular structure so storms are not perfect concentric circles
    float angWarp = (fbm(float2(angle * 1.8 + Time * 0.02, r * 10.0), 3) - 0.5) * 0.06;

    float rings = sin((r + warp + angWarp) * bandFreq * 3.14159) * 0.5 + 0.5;

    // A second rotating octave for larger storm cells
    float2 pRot2 = rot(pos, Time * 0.08 * lerp(0.8, 0.2, pow(r, 1.2)));
    float cells = fbm(pRot2 * 10.0 + float2(Time * 0.01, Time * 0.013), 4);

    float3 color1 = lerp(BaseColor, BandColor1, rings);
    float3 color2 = lerp(color1, BandColor2, cells * 0.55);

    // Small high-frequency detail
    float detail = fbm(pRot * 24.0 + float2(Time * 0.03, 0.0), 3);

    float3 finalColor = color2 + (detail - 0.5) * 0.12;

    // --- Hexagonal polar storm (Saturn-like) ---
    // Place storm at the top pole. 
    float2 stormCenter = float2(0.0, 0.0);
    float2 pStorm = pos - stormCenter;

    // Polar mask: confines to cap region
    float stormRadius = 0.24;
    float stormDist = length(pStorm);
    float polarMask = smoothstep(stormRadius, stormRadius * 0.7, stormDist);

    // Swirl the storm coordinates over time for animation
    float localAng = atan2(pStorm.y, pStorm.x);
    float stormSpin = Time * 0.35;
    float shear = (fbm(pStorm * 18.0 + float2(Time * 0.05, -Time * 0.03), 3) - 0.5) * 0.35;
    float2 pSwirl = rot(pStorm, stormSpin + shear);

    // Hex frame: a big hex ring + inner turbulent eye
    float hexR = 0.12;
    float dHex = sdHex(pSwirl * 1.0, hexR);

    // Ring thickness
    float ring = 1.0 - smoothstep(0.004, 0.018, abs(dHex));

    // Add some animated waviness to the ring edge
    float wav = fbm(pSwirl * 40.0 + float2(Time * 0.15, Time * 0.11), 4);
    ring *= smoothstep(0.25, 0.85, wav);

    // Inner eye/spiral texture
    float eye = smoothstep(0.08, 0.0, stormDist);
    float spiral = fbm(float2(localAng * 2.2 + Time * 0.9, stormDist * 22.0 - Time * 0.4), 4);
    float eyeDetail = eye * (0.35 + 0.65 * spiral);

    float stormStrength = polarMask * saturate(ring * 1.1 + eyeDetail * 0.55);

    // Storm tint: brighter, slightly different hue to pop
    float3 stormColor = lerp(BandColor2, float3(0.95, 0.92, 1.0), 0.65);
    finalColor = lerp(finalColor, stormColor, stormStrength);

    // Slight emissive highlight on the storm ring
    finalColor += stormStrength * float3(0.08, 0.10, 0.14);
    
    float edge = 1.0 - smoothstep(0.9, 1.0, dist);
    float atmosphere = smoothstep(0.7, 1.0, dist) * 0.3;
    finalColor += float3(0.3, 0.4, 0.6) * atmosphere;
    
    float sphere = sqrt(1.0 - dist * dist * 0.8);
    finalColor *= (0.6 + sphere * 0.4);
    
    return float4(finalColor * edge, edge);
}

technique GasGiant
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
