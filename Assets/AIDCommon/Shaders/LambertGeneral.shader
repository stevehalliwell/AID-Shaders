Shader "AID\LambertGeneral" {
//enum Properties https://gist.github.com/smkplus/2a5899bf415e2b6bf6a59726bb1ae2ec
	Properties {
		_Color ("Color", Color) = (0.5,0.5,0.5,1)
		[Range]_AlphaCut ("Alpha CutOff", Range(-0.01,1.01)) = -0.01
		_MainTex ("Albedo (RGBA)", 2D) = "white" {}
		[Normal]_NormalTex ("Normal Map", 2D) = "bump" {}
//		_NormalStrength ("Normal Strength", float) = 1
		[HDR]_RimColor ("Rim Color", Color) = (0.5,0.5,0.5,1)
		[Toggle]_RimUseControlTexture ("Use Control Map", Float) = 0
		_RimTex ("Rim Map (RGBA)", 2D) = "white" {}
		_RimStart ("Rim Start At", Range(0,1)) = 0.75
		[PowerSlider(3)] _RimPower ("Rim Power", Range(0.01, 15)) = 1.1
		_WorldRefTex ("Env Map", CUBE) = "" {}
		_WorldRefContrib ("Env Contrib", float) = .15
		[PowerSlider(3)] _WorldRefFrenPower ("Env Map Frenel Power", Range(0, 30)) = 5
		_WorldRefFrenCo ("Env Map Frenel Co-Eff", Range(0,1)) = 0.2
		[HDR]_EmissColor ("Emissive Color", Color) = (0,0,0,1)
		_EmissTex ("Emissive Map (RGB)", 2D) = "white" {}

		
		[HideInInspector] __Mode ("__Mode", Float) = 0.0
		[Enum(UnityEngine.Rendering.BlendMode)] __SrcBlend("SrcBlend", Float) = 1 //"One"
        [Enum(UnityEngine.Rendering.BlendMode)] __DstBlend("DestBlend", Float) = 0 //"Zero"
		[Space]
		[Enum(UnityEngine.Rendering.CullMode)] __Cull("Cull", Float) = 2 //"Back"
		[Space]
		
        [Enum(UnityEngine.Rendering.CompareFunction)] __ZTest("ZTest", Float) = 4 //"LessEqual"
        [Enum(Off,0,On,1)] __ZWrite("ZWrite", Float) = 1.0 //"On"
        //[Enum(None,0,All,15)] __ColorWriteMask("ColorWriteMask", Float) = 15 //"All"
		[Space]
		__StenRef ("Stencil Ref", Float) = 0.0
        [Enum(UnityEngine.Rendering.CompareFunction)] __StenComp("Stencil Comparison", Float) = 8
		[Enum(UnityEngine.Rendering.StencilOp)] __StencilOp ("Stencil Operation", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] __ZFailOp ("ZFail Operation", Float) = 0
	}
	SubShader {
		// early z
		/*
		Pass {
			ZWrite On
			ColorMask 0
   
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
 
			struct v2f {
				float4 pos : SV_POSITION;
			};
 
			v2f vert (appdata_base v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				return o;
			}
 
			half4 frag (v2f i) : COLOR
			{
				return half4 (0,0,0,0);
			}
			ENDCG  
		}
		*/

		Tags { "RenderType"="Opaque" }
		LOD 200

		Blend [__SrcBlend] [__DstBlend]
        ZTest [__ZTest]
        ZWrite [__ZWrite]
        Cull [__Cull]
        //ColorMask [__ColorWriteMask]
		
		Stencil {
            Ref [__StenRef]
            Comp [__StenComp]
            Pass [__StencilOp] 
            ZFail [__ZFailOp]
        }
		
		CGPROGRAM
		#pragma surface surf Lambert fullforwardshadows keepalpha alphatest:_AlphaCut addshadow

		#pragma shader_feature NORMAL_ENABLED 
		#pragma shader_feature RIM_ENABLED 
		#pragma shader_feature WORLDREF_ENABLED 
		#pragma shader_feature EMISSIVE_ENABLED 
		#pragma shader_feature _ALPHAPREMULTIPLY_ON 

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0


		sampler2D _MainTex;
		fixed4 _Color;

		#if NORMAL_ENABLED
		sampler2D _NormalTex;
		#endif//NORMAL_ENABLED
		
		#if RIM_ENABLED
		sampler2D _RimTex;
		fixed _RimUseControlTexture;
		fixed4 _RimColor;
		float _RimPower;
		fixed _RimStart;
		#endif//RIM_ENABLED
		
		#if WORLDREF_ENABLED
		samplerCUBE  _WorldRefTex;
		fixed _WorldRefContrib;
		float _WorldRefFrenPower;
		float _WorldRefFrenCo;
		#endif//WORLDREF_ENABLED
		
		#if EMISSIVE_ENABLED
		float4 _EmissColor;
		sampler2D _EmissTex;
		#endif//EMISSIVE_ENABLED


		struct Input {
			float2 uv_MainTex;
			#if NORMAL_ENABLED
			float2 uv_NormalTex;
			#endif//NORMAL_ENABLED
			#if WORLDREF_ENABLED
			float3 worldRefl; INTERNAL_DATA
			#endif//WORLDREF_ENABLED
			#if WORLDREF_ENABLED || RIM_ENABLED
			float2 uv_RimTex;
			float3 viewDir;
			#endif// WORLDREF_ENABLED || RIM_ENABLED
			#if EMISSIVE_ENABLED
			float2 uv_EmissTex;
			#endif
		};		
		
		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		/*
		struct SurfaceOutput
		{
			fixed3 Albedo;  // diffuse color
			fixed3 Normal;  // tangent space normal, if written
			fixed3 Emission;
			half Specular;  // specular power in 0..1 range
			fixed Gloss;    // specular intensity
			fixed Alpha;    // alpha for transparencies
		};
		*/

		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			#if NORMAL_ENABLED
			o.Normal = (UnpackNormal(tex2D(_NormalTex, IN.uv_NormalTex)));
			#else
			//normalise the interpolated vert norms
			o.Normal = normalize(o.Normal);
			#endif//NORMAL_ENABLED
			#if WORLDREF_ENABLED || RIM_ENABLED
			float NdotV = max(0,dot(o.Normal, IN.viewDir));
			#endif// WORLDREF || RIM_ENABLED
			
//			if(_NormalStrength != 1)
//			{
//				o.Normal = normalize(o.Normal * float3(_NormalStrength,_NormalStrength, 1.0001f));
//			}

			fixed4 cWorld = fixed4(0,0,0,0);
			#if WORLDREF_ENABLED
			if(_WorldRefContrib != 0)
			{
				float3 worldRefNorm = WorldReflectionVector (IN, o.Normal);
				cWorld = texCUBE(_WorldRefTex, worldRefNorm) * _WorldRefContrib;
				
				if(_WorldRefFrenPower != 0 && _WorldRefFrenCo != 1)
				{
					//Schlick approx http://filmicworlds.com/blog/everything-has-fresnel/
					float frenEffectExp = pow(1-NdotV, _WorldRefFrenPower);
					float frenEffectFactor = saturate(frenEffectExp + _WorldRefFrenCo * (1-frenEffectExp));
					cWorld *= frenEffectFactor;
				}
			}
			#endif//#WORLDREF_ENABLED

			o.Albedo = c.rgb;
			//o.Specular = _Specular;
			//o.Gloss = _Gloss;
			o.Alpha = c.a;
			half4 rimC = half4(0,0,0,0);

			//RimStart allows you to push the rim towards the edge even at its most faded out
			//The fall off is re-ranged between 1 and the start, to avoid having to change the power if the start 
			//is changed and vice versa
			#if RIM_ENABLED
			if(_RimStart < 1)
			{
				float rimRaw = NdotV;
				rimRaw = saturate(1-(rimRaw + _RimStart));
				rimRaw = saturate(rimRaw / (1-_RimStart));
				if(_RimUseControlTexture == 1)
				{
					rimC = tex2D (_RimTex, IN.uv_RimTex) * _RimColor;
				}
				else
				{
					rimC = _RimColor;
				}
				rimC.rgb = saturate(pow( rimRaw,_RimPower*rimC.a) * rimC.rgb);
			}
			#endif//RIM_ENABLED

			half3 baseEmiss = half3(0,0,0);
			#if EMISSIVE_ENABLED
			baseEmiss = tex2D(_EmissTex, IN.uv_EmissTex) * _EmissColor;
			#endif

			o.Emission = baseEmiss + rimC + cWorld.rgb;

			#ifdef _ALPHAPREMULTIPLY_ON
			o.Albedo *= o.Alpha;
			o.Emission *= o.Alpha;
			#endif//_ALPHAPREMULTIPLY_ON
		}
		ENDCG
	}
	FallBack "Diffuse"
	CustomEditor "LambertGeneralInspector"
}
