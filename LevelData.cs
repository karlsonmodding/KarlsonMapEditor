using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static KarlsonMapEditor.LevelEditor;
using UnityEngine;
using HarmonyLib;

namespace KarlsonMapEditor
{
    public enum GeometryShape
    {
        Cube,
        Sphere,
        Cylinder,
        Plane,
        SquarePyramid,
        TrianglePrism,
        QuarterPyramid,
        QuarterPipe,
    }

    public enum PrefabType
    {
        Pistol,
        Ak47,
        Shotgun,
        Boomer,
        Grappler,
        DummyGrappler,
        Table,
        Barrel,
        Locker,
        Screen,
        Milk,
        Enemey
    }

    public class LevelData
    {
        
        public LevelData(byte[] _data)
        {
            // decompress
            byte[] data = SevenZipHelper.Decompress(_data);
            MemoryStream stream = new MemoryStream(data);
            using (BinaryReader br = new BinaryReader(stream))
            {
                int version = br.ReadInt32();
                Loadson.Console.Log("Loading level version " + version);
                if (version == 1)
                    LoadLevel_Version1(br);
                else if (version == 2)
                    LoadLevel_Version2(br);
                else if (version == 3)
                    LoadLevel_Version3(br);
                else if (version == 4)
                    LoadLevel_Version4(br);
                else if (version == 5)
                    LoadLevel_Version5(stream);
                else
                {
                    Loadson.Console.Log("<color=red>Unknown level version " + version + "</color>");
                    Loadson.Console.Log("Try to update KME to the latest version.");
                }
            }
        }

        #region loaders
        private void LoadLevel_Version1(BinaryReader br)
        {
            isKMEv2 = false;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            GlobalObject = ReadObjectGroup_v1(br);
            AutomataScript = "";
        }

        private void LoadLevel_Version2(BinaryReader br)
        {
            isKMEv2 = false;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            GlobalObject = ReadObjectGroup_v1(br, true);
            AutomataScript = "";
        }

        private void LoadLevel_Version3(BinaryReader br)
        {
            isKMEv2 = true;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
            AutomataScript = "";
        }

        private void LoadLevel_Version4(BinaryReader br)
        {
            isKMEv2 = true;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            AutomataScript = br.ReadString();
            GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
        }

        private void LoadLevel_Version5(Stream stream)
        {
            isKMEv2 = true;
            Map map = Map.Parser.ParseFrom(stream);
            gridAlign = map.GridAlign;
            startingGun = map.StartingGun;
            startPosition = map.StartPosition;
            startOrientation = map.StartOrientation;
            AutomataScript = map.AutomataScript;
            GlobalObject = map.LoadTree();
            SetupMaterials = map.LoadMaterials;
            SetupGlobalLight = map.LoadGlobalLight;
        }
        #endregion

        #region LoadHelpers
        private void ReadGlobalProperties(BinaryReader br)
        {
            gridAlign = br.ReadSingle();
            startingGun = br.ReadInt32();
            startPosition = br.ReadVector3();
            startOrientation = br.ReadSingle();
        }
        private void ReadTextures(BinaryReader br)
        {
            int _len;
            int _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                string _name = br.ReadString();
                _len = br.ReadInt32();
                Texture2D tex = new Texture2D(1, 1);
                tex.LoadImage(br.ReadBytes(_len));
                tex.name = _name;
                OldTextures.Add(tex);
            }
        }

        private ObjectGroup ReadObjectGroup_v1(BinaryReader br, bool objLayerData = false)
        {
            ObjectGroup objGroup = new ObjectGroup();
            objGroup.Name = "global object";
            objGroup.Position = Vector3.zero;
            objGroup.Rotation = Vector3.zero;
            objGroup.Scale = Vector3.one;

            int _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                bool prefab = br.ReadBoolean();
                string name = br.ReadString();
                string group = br.ReadString(); // kme v2 removed group names, so this is no longer used
                if (prefab)
                    objGroup.Objects.Add(new LevelObject((PrefabType)br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, br.ReadInt32()));
                else
                    objGroup.Objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), objLayerData ? br.ReadBoolean() : false, GeometryShape.Cube, OldTextureData));
            }
            return objGroup;
        }

        ObjectGroup ReadObjectGroup_v3(byte[] group)
        {
            ObjectGroup objGroup = new ObjectGroup();
            using (BinaryReader br = new BinaryReader(new MemoryStream(group)))
            {
                objGroup.Name = br.ReadString();
                objGroup.Position = br.ReadVector3();
                objGroup.Rotation = br.ReadVector3();
                objGroup.Scale = br.ReadVector3();
                int count = br.ReadInt32();
                while (count-- > 0)
                {
                    bool prefab = br.ReadBoolean();
                    string name = br.ReadString();
                    if (prefab)
                        objGroup.Objects.Add(new LevelObject((PrefabType)br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, br.ReadInt32()));
                    else
                        objGroup.Objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), GeometryShape.Cube, OldTextureData));
                    Loadson.Console.Log(objGroup.Objects.Last().ToString());
                }
                count = br.ReadInt32();
                while (count-- > 0)
                    objGroup.Groups.Add(ReadObjectGroup_v3(br.ReadByteArray()));
                return objGroup;
            }
        }
        #endregion

        
        public bool isKMEv2;
        public float gridAlign;
        public int startingGun;
        public Vector3 startPosition;
        public float startOrientation;

        public ObjectGroup GlobalObject;

        public string AutomataScript;
        public Action<Light> SetupGlobalLight = delegate(Light sun) { sun.Reset(); sun.enabled = false; sun.gameObject.transform.rotation = Quaternion.Euler(70, 0, 0); };
        public Action SetupMaterials;

        // used for loading material data from older saves (versions 1-4)
        private List<(int, Color, bool)> OldTextureData = new List<(int, Color, bool)>();
        private List<Texture2D> OldTextures = new List<Texture2D>();

        // sets up materials from older saves (versions 1-4)
        private void SetupMaterialsOld()
        {
            MaterialManager.InitInternalTextures();
            foreach (Texture2D tex in OldTextures)
            {
                MaterialManager.AddTexture(tex);
            }
            foreach ((int, Color, bool) data in OldTextureData)
            {
                MaterialManager.InstanceMaterial(data.Item1, data.Item2, data.Item3);
            }
        }

        #region Object Data
        public class LevelObject
        {
            // empty
            public LevelObject() { }
            // prefab
            public LevelObject(PrefabType prefabId, Vector3 position, Vector3 rotation, Vector3 scale, string name, int prefabData)
            {
                IsPrefab = true;
                PrefabId = prefabId;

                Position = position;
                Rotation = rotation;
                Scale = scale;

                Name = name;

                PrefabData = prefabData;
            }
            // geometry
            public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, int textureId, Color color, string name, bool bounce, bool glass, bool lava, bool disableTrigger, bool markAsObject, GeometryShape shape = GeometryShape.Cube, List<(int, Color, bool)> textureData = null)
            {
                IsPrefab = false;

                if (textureData == null)
                {
                    MaterialId = MaterialManager.InstanceMaterial(textureId, color, glass | lava);
                }
                else
                {
                    MaterialId = textureData.Count;
                    textureData.Add((textureId, color, glass | lava));
                }
                ShapeId = shape;

                Position = position;
                Rotation = rotation;
                Scale = scale;

                Name = name;
                Bounce = bounce;
                Glass = glass && !disableTrigger;
                Lava = lava;
                MarkAsObject = markAsObject;
            }
            
            // common to all level objects
            public bool IsPrefab;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 Scale;
            public string Name;
            public string GroupName;

            // prefab specific
            public PrefabType PrefabId;
            public int PrefabData;
            public GameObject _enemyFix;
            public float _enemyFixY;

            // geometry specific
            public GeometryShape ShapeId;
            public int MaterialId;
            public float UVNormalizedScale = 10f;
            public bool Bounce;
            public bool Glass;
            public bool Lava;
            // public bool DisableTrigger;
            public bool MarkAsObject;

            public override string ToString()
            {
                string st = "(PF:" + IsPrefab;
                if (IsPrefab)
                    st += " " + PrefabId;
                st += " " + Position + " " + Rotation + " " + Scale;
                if (!IsPrefab)
                    st += " mat:" + MaterialId;
                st += ")";
                return st;
            }
        }

        public class ObjectGroup
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 Scale;
            public List<ObjectGroup> Groups;
            public List<LevelObject> Objects;

            public ObjectGroup()
            {
                Groups = new List<ObjectGroup>();
                Objects = new List<LevelObject>();
            }
        }
        #endregion

        public static GameObject MakePrefab(PrefabType prefab)
        {
            switch (prefab)
            {
                case PrefabType.Pistol:
                    return LoadsonAPI.PrefabManager.NewPistol();
                case PrefabType.Ak47:
                    return LoadsonAPI.PrefabManager.NewAk47();
                case PrefabType.Shotgun:
                    return LoadsonAPI.PrefabManager.NewShotgun();
                case PrefabType.Boomer:
                    return LoadsonAPI.PrefabManager.NewBoomer();
                case PrefabType.Grappler:
                    return LoadsonAPI.PrefabManager.NewGrappler();
                case PrefabType.DummyGrappler:
                    return LoadsonAPI.PrefabManager.NewDummyGrappler();
                case PrefabType.Table:
                    return LoadsonAPI.PrefabManager.NewTable();
                case PrefabType.Barrel:
                    return LoadsonAPI.PrefabManager.NewBarrel();
                case PrefabType.Locker:
                    return LoadsonAPI.PrefabManager.NewLocker();
                case PrefabType.Screen:
                    return LoadsonAPI.PrefabManager.NewScreen();
                case PrefabType.Milk:
                    return LoadsonAPI.PrefabManager.NewMilk();
                case PrefabType.Enemey:
                    return LoadsonAPI.PrefabManager.NewEnemy();
                default:
                    return null;
            }
        }
    }
}
