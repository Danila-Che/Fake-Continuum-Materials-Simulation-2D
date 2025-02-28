using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Fake.Utilities
{
    public static class JobUtility
    {
        [BurstCompile]
        public struct FillJob<T> : IJob
            where T : unmanaged
        {
            [WriteOnly] public NativeArray<T> array;

            public T value;

            public unsafe void Execute()
            {
                var length = array.Length;
                var arrayPtr = (T*)array.GetUnsafePtr();

                for (int i = 0; i < length; i++)
                {
                    arrayPtr[i] = value;
                }
            }
        }
        
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static void Fill<T>(NativeArray<T> array, T value)
        //     where T : unmanaged
        // {
        //     new FillJob<T>
        //     {
        //         array = array,
        //         value = value
        //     }.Run();
        // }
    }
}
