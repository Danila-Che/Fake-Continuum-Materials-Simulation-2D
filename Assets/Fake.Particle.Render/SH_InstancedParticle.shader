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
                float4 color : COLOR0;
            };

            fixed4 _Color;
            float _Size;
            float2 _GridHalfSize;
            float _BoxSize;

            StructuredBuffer<Particle> _ParticleBuffer;

            v2f vert(appdata_base v, uint svInstanceID : SV_InstanceID)
            {
                uint instance_id = GetIndirectInstanceID(svInstanceID);

                Particle particle = _ParticleBuffer[instance_id];
                float3 data = float3((particle.position - _GridHalfSize) * _BoxSize, 0.0f);

                float3 local_position = v.vertex.xyz * _Size;
                float3 world_position = data + local_position;
                
                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(world_position, 1.0f));
                
                float l = length(particle.displacement);
                l = clamp(l / 0.1f, 0, 1);
                
                float3 red = float3(1.0f, 0.0f, 0.0f);
                float3 blue = float3(0.0f, 0.0f, 1.0f);
                float3 c = lerp(blue, red, l);
                o.color = float4(c, 1.0f);
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return _Color;
                // return i.color;
            }
            ENDCG
        }
    }
}
