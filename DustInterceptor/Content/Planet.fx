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

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    float2 center = float2(0.5, 0.5);
    float2 pos = uv - center;
    float dist = length(pos) * 2.0;
    
    if (dist > 1.0)
        discard;
    
    float angle = atan2(pos.y, pos.x);
    float latitude = pos.y + 0.5;
    
    float bandFreq = 8.0;
    float bandWarp = fbm(float2(latitude * 3.0 + Time * 0.02, angle * 0.5), 4) * 0.3;
    float bands = sin((latitude + bandWarp) * bandFreq * 3.14159) * 0.5 + 0.5;
    
    float swirl = fbm(float2(angle * 2.0 + Time * 0.05, latitude * 4.0 - Time * 0.03), 5);
    float detail = fbm(float2(pos.x * 8.0 + Time * 0.01, pos.y * 8.0), 3) * 0.2;
    
    float3 color1 = lerp(BaseColor, BandColor1, bands);
    float3 color2 = lerp(color1, BandColor2, swirl * 0.4);
    float3 finalColor = color2 + detail * 0.3;
    
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
