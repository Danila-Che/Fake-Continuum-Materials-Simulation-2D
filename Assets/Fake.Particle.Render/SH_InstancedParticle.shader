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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            fixed4 _Color;
            float _Size;

            StructuredBuffer<Particle> particle_buffer;

            v2f vert(appdata_base v, uint svInstanceID : SV_InstanceID)
            {
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                
                float3 data = float3(particle_buffer[instanceID].position, 0.0f);

                float3 localPosition = v.vertex.xyz * _Size;
                float3 worldPosition = data + localPosition;
                
                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPosition, 1.0f));
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
