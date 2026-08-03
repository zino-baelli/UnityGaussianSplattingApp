using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GaussianSplatting.Runtime
{
    // Stands in for TextAsset in GaussianSplatAsset's data properties.
    // Backed by either a serialized TextAsset (Editor-created assets)
    // or a raw byte[] (runtime-created assets).
    public sealed class GsDataRef
    {
        readonly TextAsset m_TextAsset;
        readonly byte[] m_Bytes;

        public GsDataRef(TextAsset textAsset) { m_TextAsset = textAsset; }
        public GsDataRef(byte[] bytes) { m_Bytes = bytes; }

        public long dataSize => m_TextAsset != null ? m_TextAsset.dataSize : (m_Bytes?.Length ?? 0);
        
        public NativeArray<T> GetData<T>() where T : unmanaged
        {
            if (m_TextAsset != null)
                return m_TextAsset.GetData<T>();

            var native = new NativeArray<byte>(m_Bytes, Allocator.Temp);
            return native.Reinterpret<T>(UnsafeUtility.SizeOf<byte>());
        }

//         public NativeArray<T> GetData<T>() where T : unmanaged
//         {
//             if (m_TextAsset != null)
//                 return m_TextAsset.GetData<T>();
//
//             // Wrap the byte[] as a NativeArray<T> view, same shape TextAsset.GetData<T> returns.
//             unsafe
//             {
//                 fixed (byte* ptr = m_Bytes)
//                 {
//                     var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
//                         ptr, m_Bytes.Length / UnsafeUtility.SizeOf<T>(), Allocator.None);
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//                     NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, AtomicSafetyHandle.Create());
// #endif
//                     return arr;
//                 }
//             }
//         }
    }
}