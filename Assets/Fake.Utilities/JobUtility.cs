using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Fake.Utilities
{
    public static class JobUtility
    {
        [BurstCompile]
        private struct FillJob<T> : IJob
            where T : unmanaged
        {
            [WriteOnly]
            public NativeArray<T> array;

            public T value;
            
            public unsafe void Execute()
            {
                var arrayPtr = (T*)array.GetUnsafePtr();
                var length = array.Length;
                
                for (int i = 0; i < length; i++)
                {
                    arrayPtr[i] = value;
                }
            }
        }
        
        public static void Fill<T>(NativeArray<T> array, T value)
            where T : unmanaged
        {
            new FillJob<T>
            {
                array = array,
                value = value
            }.Run();
        }
    }
}
