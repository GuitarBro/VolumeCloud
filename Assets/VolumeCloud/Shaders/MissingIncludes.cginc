#include "HLSLSupport.cginc"

CBUFFER_START(UnityLighting)
#ifdef USING_DIRECTIONAL_LIGHT
half4 _WorldSpaceLightPos0;
#else
float4 _WorldSpaceLightPos0;
#endif

float4 _LightPositionRange; // xyz = pos, w = 1/range
float4 _LightProjectionParams; // for point light projection: x = zfar / (znear - zfar), y = (znear * zfar) / (znear - zfar), z=shadow bias, w=shadow scale bias

float4 unity_4LightPosX0;
float4 unity_4LightPosY0;
float4 unity_4LightPosZ0;
half4 unity_4LightAtten0;

half4 unity_LightColor[8];


float4 unity_LightPosition[8]; // view-space vertex light positions (position,1), or (-direction,0) for directional lights.
							   // x = cos(spotAngle/2) or -1 for non-spot
							   // y = 1/cos(spotAngle/4) or 1 for non-spot
							   // z = quadratic attenuation
							   // w = range*range
half4 unity_LightAtten[8];
float4 unity_SpotDirection[8]; // view-space spot light directions, or (0,0,1,0) for non-spot

							   // SH lighting environment
half4 unity_SHAr;
half4 unity_SHAg;
half4 unity_SHAb;
half4 unity_SHBr;
half4 unity_SHBg;
half4 unity_SHBb;
half4 unity_SHC;

// part of Light because it can be used outside of shadow distance
half4 unity_OcclusionMaskSelector;
half4 unity_ProbesOcclusion;
CBUFFER_END

CBUFFER_START(UnityPerCameraRare)
float4 unity_CameraWorldClipPlanes[6];

#if !defined(USING_STEREO_MATRICES)
// Projection matrices of the camera. Note that this might be different from projection matrix
// that is set right now, e.g. while rendering shadows the matrices below are still the projection
// of original camera.
float4x4 unity_CameraInvProjection;
float4x4 unity_CameraToWorld;
#endif
CBUFFER_END


#define UNITY_MATRIX_VP unity_MatrixVP

// Tranforms position from object to homogenous space
inline float4 UnityObjectToClipPos(in float3 pos)
{
#if defined(STEREO_CUBEMAP_RENDER_ON)
	return UnityObjectToClipPosODS(pos);
#else
	// More efficient than computing M*VP matrix product
	return mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(pos, 1.0)));
#endif
}
inline float4 UnityObjectToClipPos(float4 pos) // overload for float4; avoids "implicit truncation" warning for existing shaders
{
	return UnityObjectToClipPos(pos.xyz);
}

inline float4 ComputeNonStereoScreenPos(float4 pos) {
	float4 o = pos * 0.5f;
	o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w;
	o.zw = pos.zw;
	return o;
}

inline float4 ComputeScreenPos(float4 pos) {
	float4 o = ComputeNonStereoScreenPos(pos);
#if defined(UNITY_SINGLE_PASS_STEREO)
	o.xy = TransformStereoScreenSpaceTex(o.xy, pos.w);
#endif
	return o;
}

half4 _LightColor0;
