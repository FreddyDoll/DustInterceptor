#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float Time;
float2 Resolution;
float2 CameraPosition;
float CameraZoom;
float GridSpacing;
float RadialLineCount;
float GridLineWidth;

sampler2D SpriteTextureSampler : register(s0);

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float CalculateGridLayer(float2 worldPos, float dist, float spacing, float lineWidth, float radialCount, float opacity, float aaWidth)
{
    float pi = 3.14159265;
    float circlePhase = dist / spacing;
    float nearestCircle = round(circlePhase) * spacing;
    float circleDistance = abs(dist - nearestCircle);
    float circleEdge = lineWidth * 0.5;
    float circleGrid = 1.0 - smoothstep(circleEdge - aaWidth, circleEdge + aaWidth, circleDistance);
    
    float angle = atan2(worldPos.y, worldPos.x);
    float angleSpacing = 2.0 * pi / radialCount;
    float nearestAngle = round(angle / angleSpacing) * angleSpacing;
    float angleDiff = angle - nearestAngle;
    float radialDistance = abs(sin(angleDiff) * dist);
    float radialEdge = lineWidth * 0.5;
    float radialGrid = 1.0 - smoothstep(radialEdge - aaWidth, radialEdge + aaWidth, radialDistance);
    
    float centerFade = smoothstep(0.0, spacing * 2.0, dist);
    return max(circleGrid, radialGrid) * centerFade * opacity;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TexCoord;
    float2 screenCenter = float2(0.5, 0.5);
    float2 worldPos = (uv - screenCenter) * Resolution / CameraZoom + CameraPosition;
    float dist = length(worldPos);
    
    float pixelSize = 1.0 / CameraZoom;
    float aaWidth = pixelSize * 5.0;
    
    float layer1 = CalculateGridLayer(worldPos, dist, GridSpacing, GridLineWidth, RadialLineCount, 0.2, aaWidth);
    float layer2 = CalculateGridLayer(worldPos, dist, GridSpacing / 10.0, GridLineWidth / 10.0, RadialLineCount * 4.0, 0.15, aaWidth);
    float layer3 = CalculateGridLayer(worldPos, dist, GridSpacing / 100.0, GridLineWidth / 300.0, RadialLineCount * 36.0, 0.1, aaWidth);
    
    float farFade = 1.0 - smoothstep(GridSpacing * 200.0, GridSpacing * 250.0, dist);
    float pattern = max(max(layer1, layer2), layer3) * farFade;
    
    float3 gridColor = float3(0.15, 0.18, 0.25);
    float alpha = pattern;
    return float4(gridColor * alpha, alpha);
}

technique BackgroundGrid
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
