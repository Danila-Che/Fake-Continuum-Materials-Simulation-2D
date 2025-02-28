Shader "Instanced/SH_InstancedParticle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Size ("Size", float) = 1.0
    }
    
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct Particle
            {
                float2 position;
                float2 displacement;
                float2x2 deformation_gradient;
                float2x2 deformation_displacement;
                
                float liquid_density;
                float mass;
                float volume;
                float logJp;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            fixed4 _Color;
            float _Size;
            float2 _GridHalfSize;
            float _BoxSize;

            StructuredBuffer<Particle> _ParticleBuffer;

            v2f vert(appdata_base v, uint svInstanceID : SV_InstanceID)
            {
                uint instance_id = GetIndirectInstanceID(svInstanceID);
                
                float3 data = float3((_ParticleBuffer[instance_id].position - _GridHalfSize) * _BoxSize, 0.0f);

                float3 local_position = v.vertex.xyz * _Size;
                float3 world_position = data + local_position;
                
                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(world_position, 1.0f));
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
