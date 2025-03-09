using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor
{
    public static class BinaryExtensions
    {
        public static Vector3 ReadVector3(this BinaryReader br)
        {
            float f1 = br.ReadSingle();
            float f2 = br.ReadSingle();
            float f3 = br.ReadSingle();
            return new Vector3(f1, f2, f3);
        }

        public static void Write(this BinaryWriter bw, Vector3 v)
        {
            bw.Write(v.x);
            bw.Write(v.y);
            bw.Write(v.z);
        }

        public static Color ReadColor(this BinaryReader br)
        {
            float f1 = br.ReadSingle();
            float f2 = br.ReadSingle();
            float f3 = br.ReadSingle();
            float f4 = br.ReadSingle();
            return new Color(f1, f2, f3, f4);
        }

        public static void Write(this BinaryWriter bw, Color c)
        {
            bw.Write(c.r);
            bw.Write(c.g);
            bw.Write(c.b);
            bw.Write(c.a);
        }

        public static byte[] ReadByteArray(this BinaryReader br)
        {
            int len = br.ReadInt32();
            return br.ReadBytes(len);
        }

        public static void WriteByteArray(this BinaryWriter bw, byte[] data)
        {
            bw.Write(data.Length);
            bw.Write(data);
        }
    }

    public static class Vector3Extensions
    {
        public static float DistanceOnDirection(Vector3 origin, Vector3 point, Vector3 direction)
        {
            Vector3 translated = point - origin;
            if (direction.x > 0.01f)
                return translated.x / direction.x;
            if (direction.y > 0.01f)
                return translated.y / direction.y;
            if (direction.z > 0.01f)
                return translated.z / direction.z;
            return 0f; // something went wrong. or point == origin
        }

        // snaps to a rotated grid of snap points
        public static Vector3 SnapPos(Vector3 original, float snap, Vector3 angles)
        {
            Quaternion quat = Quaternion.Euler(angles);
            return quat * Snap(Quaternion.Inverse(quat) * original, snap);
        }
        // snaps except when the object is smaller than the snap size
        public static Vector3 SnapScale(Vector3 original, float snap)
        {
            Vector3 snapped = Snap(original, snap);
            if (Mathf.Abs(original.x) < snap) snapped.x = snap * Mathf.Sign(original.x);
            if (Mathf.Abs(original.y) < snap) snapped.y = snap * Mathf.Sign(original.y);
            if (Mathf.Abs(original.z) < snap) snapped.z = snap * Mathf.Sign(original.z);
            return snapped;
        }

        static Vector3 HalfOne = Vector3.one * 0.5f;
        // snaps to the nearest snap point
        public static Vector3 Snap(Vector3 original, float snap)
        {
            return snap * (Vector3)Vector3Int.FloorToInt((original / snap) + HalfOne);
        }
    }
}
