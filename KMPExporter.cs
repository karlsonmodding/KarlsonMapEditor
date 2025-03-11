using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor
{
    public class KMPExporter
    {
        private static char quartToChar(byte quart)
        {
            if (quart < 26) return (char)('a' + quart);
            quart -= 26;
            if (quart < 26) return (char)('A' + quart);
            quart -= 26;
            if (quart < 10) return (char)('0' + quart);
            quart -= 10;
            if (quart == 0) return '-';
            return '_';
        }
        private static string base64(ulong objid)
        {
            string result = "";
            while(objid > 0)
            {
                result += quartToChar((byte)(objid & 0b111111));
                objid >>= 6;
            }
            return result;
        }

        public static void Export(string levelName, LevelEditor.ObjectGroup globalObject, List<Texture2D> textures)
        {
            Directory.CreateDirectory(Path.Combine(Main.directory, "KMP_Export"));
            // KMP uses v2 format
            // we don't actually need to save v3 data in order to play the map

            // process globalObject and extract relevant objects
            List<LevelEditor.EditorObject> objects = new List<LevelEditor.EditorObject>();
            void dfs(LevelEditor.ObjectGroup group)
            {
                objects.AddRange(group.editorObjects);
                foreach (var g in group.objectGroups)
                    dfs(g);
            }
            dfs(globalObject);
            Dictionary<string, List<(string, Vector3, Vector3)>> kmp_data = new Dictionary<string, List<(string, Vector3, Vector3)>>();

            ulong objid = 1;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(2);
                bw.Write(0f);
                bw.Write(0);
                bw.Write(Vector3.zero);
                bw.Write(0f);
                bw.Write(textures.Count);
                foreach (var t in textures)
                {
                    bw.Write(t.name);
                    byte[] data = t.EncodeToPNG();
                    bw.Write(data.Length);
                    bw.Write(data);
                }
                int internalCount = 0;
                foreach (var obj in objects)
                    if (obj.internalObject || obj.go.name.StartsWith("!KMP") || obj.data.IsPrefab) internalCount++;
                bw.Write(objects.Count - internalCount);
                foreach (var obj in objects)
                {
                    if (obj.go.name.StartsWith("!KMP"))
                    {
                        // don't include kmp objects, instead write them to data file
                        string key = obj.go.name.Split('.')[1];
                        string value = obj.go.name.Split('.')[2];
                        if (!kmp_data.ContainsKey(key))
                            kmp_data.Add(key, new List<(string, Vector3, Vector3)>());
                        kmp_data[key].Add((value, obj.go.transform.position, obj.go.transform.rotation.eulerAngles));
                        continue;
                    }
                    if (obj.internalObject || obj.data.IsPrefab) continue;
                    bw.Write(obj.data.IsPrefab);
                    bw.Write(base64(objid++));
                    bw.Write("");
                    /*if (obj.data.IsPrefab) // prefabs are not supported by kmp
                    {
                        bw.Write(obj.data.PrefabId);
                        bw.Write(obj.go.transform.position);
                        bw.Write(obj.go.transform.rotation.eulerAngles);
                        bw.Write(obj.go.transform.lossyScale);
                        bw.Write(obj.data.PrefabData);
                    }*/
                    bw.Write(obj.go.transform.position);
                    bw.Write(obj.go.transform.rotation.eulerAngles);
                    bw.Write(obj.go.transform.lossyScale);
                    bw.Write(LevelEditor.MaterialManager.GetMainTextureIndex(obj.data.MaterialId));
                    bw.Write(obj.go.GetComponent<MeshRenderer>().material.color);
                    bw.Write(obj.data.Bounce);
                    bw.Write(obj.data.Glass);
                    bw.Write(obj.data.Lava);
                    bw.Write(obj.data.DisableTrigger);
                    bw.Write(obj.data.MarkAsObject);
                }
                bw.Flush();
                File.WriteAllBytes(Path.Combine(Main.directory, "KMP_Export", levelName + ".kme_raw"), ms.ToArray());
            }
            Loadson.Console.Log("Writing kmp data");
            using(MemoryStream ms = new MemoryStream())
            using(BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(kmp_data.Count);
                foreach(var x in kmp_data)
                {
                    bw.Write(x.Key);
                    bw.Write(x.Value.Count);
                    foreach(var val in x.Value)
                    {
                        bw.Write(val.Item1);
                        bw.Write(val.Item2);
                        bw.Write(val.Item3);
                    }
                }
                bw.Flush();
                File.WriteAllBytes(Path.Combine(Main.directory, "KMP_Export", levelName + ".kme_data"), ms.ToArray());
            }

            Process.Start(Path.Combine(Main.directory, "KMP_Export"));
        }
    }
}
