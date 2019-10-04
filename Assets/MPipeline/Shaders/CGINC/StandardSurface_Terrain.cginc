#ifndef __STANDARDSURFACE_INCLUDE__
#define __STANDARDSURFACE_INCLUDE__
sampler2D _PreIntDefault;
Texture2DArray<float> _VirtualHeightmap; SamplerState sampler_VirtualHeightmap;
Texture2DArray<float4> _VirtualMainTex; SamplerState sampler_VirtualMainTex;
Texture2DArray<float2> _VirtualBumpMap; SamplerState sampler_VirtualBumpMap;
Texture2DArray<float4> _VirtualSMMap; SamplerState sampler_VirtualSMMap;
#include "VirtualTexture.cginc"
float3 ProcessNormal(float2 value)
{
	float z = sqrt(1 - dot(value, value));
	return float3(value, z);
}
		void surf (float2 uv, uint2 vtIndex, inout SurfaceOutputStandardSpecular o) {
			float3 vtUV = GetVirtualTextureUV(vtIndex + floor(uv), frac(uv));
#ifdef LIT_ENABLE
			float4 spec = _VirtualSMMap.SampleLevel(sampler_VirtualSMMap, vtUV, 0);
			float4 c =  _VirtualMainTex.SampleLevel(sampler_VirtualMainTex, vtUV, 0);
			o.Normal = ProcessNormal(_VirtualBumpMap.SampleLevel(sampler_VirtualBumpMap, vtUV, 0));
			o.Albedo = c.rgb;

			o.Alpha = 1;
			o.Occlusion = c.a;
			float metallic =  spec.g;
			o.Specular = lerp(0.04, o.Albedo, metallic); 
			o.Albedo *= lerp(1 - 0.04, 0, metallic);
			o.Smoothness = spec.r;
			#else
			o = (SurfaceOutputStandardSpecular)0;
#endif
			o.Emission = 0;
		}


void VertexOffset(inout float4 vertex, float3 normal, float2 uv)
{
	#ifdef USE_TESSELLATION
	vertex.xyz += _HeightMap.SampleLevel(sampler_HeightMap, uv, 0) * normal * _HeightmapIntensity;
	#endif
}

#endif