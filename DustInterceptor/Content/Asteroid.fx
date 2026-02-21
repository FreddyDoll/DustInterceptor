#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float3 IceColor;
float3 IronColor;
float3 RockColor;
float IceRatio;
float IronRatio;
float RockRatio;
float Seed;

sampler2D SpriteTextureSampler : register(s0);

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float hash(float2 p)
{
    return frac(sin(dot(p + Seed, float2(127.1, 311.7))) * 43758.5453);
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

float fbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++)
    {
        value += amplitude * noise(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    float2 center = float2(0.5, 0.5);
    float2 pos = uv - center;
    float dist = length(pos) * 2.0;
    
    if (dist > 1.0)
        discard;
    
    float angle = atan2(pos.y, pos.x);
    float noiseVal = fbm(float2(angle * 3.0 + Seed, dist * 5.0 + Seed * 0.7)) * 0.15;
    float edgeDist = dist + noiseVal;
    
    if (edgeDist > 0.95)
        discard;
    
    float3 baseColor = IceColor * IceRatio + IronColor * IronRatio + RockColor * RockRatio;
    
    float pattern1 = fbm(pos * 8.0 + Seed);
    float pattern2 = fbm(pos * 12.0 + Seed * 1.3);
    float pattern3 = fbm(pos * 4.0 + Seed * 0.7);
    
    float3 iceVariation = IceColor * (0.8 + pattern1 * 0.4);
    float3 ironVariation = IronColor * (0.7 + pattern2 * 0.6);
    float3 rockVariation = RockColor * (0.8 + pattern3 * 0.4);
    
    float blend1 = smoothstep(0.3, 0.7, pattern1);
    float blend2 = smoothstep(0.4, 0.6, pattern2);
    
    float3 finalColor = baseColor;
    finalColor = lerp(finalColor, iceVariation, IceRatio * blend1);
    finalColor = lerp(finalColor, ironVariation, IronRatio * blend2);
    finalColor = lerp(finalColor, rockVariation, RockRatio * (1.0 - blend1));
    
    float detail = fbm(pos * 20.0 + Seed * 2.0) * 0.15;
    finalColor += detail - 0.075;
    
    float sphere = sqrt(max(0.0, 1.0 - dist * dist));
    finalColor *= (0.5 + sphere * 0.5);
    
    float edge = 1.0 - smoothstep(0.85, 0.95, edgeDist);
    
    return float4(finalColor * edge, edge);
}

technique Asteroid
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
