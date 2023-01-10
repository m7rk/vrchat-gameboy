
using System.Runtime.CompilerServices;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BitOps : UdonSharpBehaviour
{
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte bitSet(byte n, byte v) {
            return v |= (byte)(1 << n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte bitClear(int n, byte v) {
            return v &= (byte)(0xff & (~(1 << n)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool isBit(int n, int v) {
            return ((v >> n) & 1) == 1;
        }
}
