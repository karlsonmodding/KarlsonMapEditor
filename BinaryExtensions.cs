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
        public static Vector3 Snap(Vector3 original, float snap)
        {
            Vector3 result = new Vector3();
            // x axis
            if (Mathf.Abs(original.x) < snap / 2) // edge case
                result.x = 0f;
            else
            {
                float comp = original.x + snap / 2f;
                if (original.x < 0)
                    comp = -comp;
                while (comp >= snap) comp -= snap;
                result.x = original.x + snap / 2f - comp;
                if (original.x < 0)
                    result.x = original.x - snap / 2f + comp;
            }

            // y axis
            if (Mathf.Abs(original.y) < snap / 2) // edge case
                result.y = 0f;
            else
            {
                float comp = original.y + snap / 2f;
                if (original.y < 0)
                    comp = -comp;
                while (comp >= snap) comp -= snap;
                result.y = original.y + snap / 2f - comp;
                if (original.y < 0)
                    result.y = original.y - snap / 2f + comp;
            }

            // z axis
            if (Mathf.Abs(original.z) < snap / 2) // edge case
                result.z = 0f;
            else
            {
                float comp = original.z + snap / 2f;
                if (original.z < 0)
                    comp = -comp;
                while (comp >= snap) comp -= snap;
                result.z = original.z + snap / 2f - comp;
                if (original.z < 0)
                    result.z = original.z - snap / 2f + comp;
            }

            return result;
        }
    }
}
