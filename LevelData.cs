using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static KarlsonMapEditor.LevelEditor;
using UnityEngine;

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
        public static readonly PrimitiveType[] typeToInt = new PrimitiveType[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder, PrimitiveType.Plane, PrimitiveType.Quad };

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
                        objGroup.Objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean()));
                    Loadson.Console.Log(objGroup.Objects.Last().ToString());
                }
                count = br.ReadInt32();
                while (count-- > 0)
                    objGroup.Groups.Add(ReadObjectGroup_v3(br.ReadByteArray()));
                return objGroup;
            }
        }

        private void LoadLevel_Version1(BinaryReader br)
        {
            isKMEv2 = false;
            MaterialManager.InitInternalTextures();
            gridAlign = br.ReadSingle();
            startingGun = br.ReadInt32();
            startPosition = br.ReadVector3();
            startOrientation = br.ReadSingle();
            int _len;
            int _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                string _name = br.ReadString();
                _len = br.ReadInt32();
                Texture2D tex = new Texture2D(1, 1);
                tex.LoadImage(br.ReadBytes(_len));
                tex.name = _name;
                MaterialManager.AddTexture(tex);
            }

            List<LevelObject> objects = new List<LevelObject>();
            _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                bool prefab = br.ReadBoolean();
                string name = br.ReadString();
                string group = br.ReadString();
                if (prefab)
                    objects.Add(new LevelObject((PrefabType)br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, group, br.ReadInt32()));
                else
                    objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, group, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), false));
            }
            Objects = objects.ToArray();
            AutomataScript = "";
        }

        private void LoadLevel_Version2(BinaryReader br)
        {
            isKMEv2 = false;
            MaterialManager.InitInternalTextures();
            gridAlign = br.ReadSingle();
            startingGun = br.ReadInt32();
            startPosition = br.ReadVector3();
            startOrientation = br.ReadSingle();
            int _len;
            int _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                string _name = br.ReadString();
                _len = br.ReadInt32();
                Texture2D tex = new Texture2D(1, 1);
                tex.LoadImage(br.ReadBytes(_len));
                tex.name = _name;
                MaterialManager.AddTexture(tex);
            }
            List<LevelObject> objects = new List<LevelObject>();
            _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                bool prefab = br.ReadBoolean();
                string name = br.ReadString();
                string group = br.ReadString();
                if (prefab)
                    objects.Add(new LevelObject((PrefabType)br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, group, br.ReadInt32()));
                else
                    objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, group, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean()));
            }
            Objects = objects.ToArray();
            AutomataScript = "";
        }

        private void LoadLevel_Version3(BinaryReader br)
        {
            isKMEv2 = true;
            MaterialManager.InitInternalTextures();
            gridAlign = br.ReadSingle();
            startingGun = br.ReadInt32();
            startPosition = br.ReadVector3();
            startOrientation = br.ReadSingle();
            int _len;
            int _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                string _name = br.ReadString();
                _len = br.ReadInt32();
                Texture2D tex = new Texture2D(1, 1);
                tex.LoadImage(br.ReadBytes(_len));
                tex.name = _name;
                MaterialManager.AddTexture(tex);
            }
            GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
            AutomataScript = "";
        }

        private void LoadLevel_Version4(BinaryReader br)
        {
            isKMEv2 = true;
            MaterialManager.InitInternalTextures();
            gridAlign = br.ReadSingle();
            startingGun = br.ReadInt32();
            startPosition = br.ReadVector3();
            startOrientation = br.ReadSingle();
            int _len;
            int _texl = br.ReadInt32();
            while (_texl-- > 0)
            {
                string _name = br.ReadString();
                _len = br.ReadInt32();
                Texture2D tex = new Texture2D(1, 1);
                tex.LoadImage(br.ReadBytes(_len));
                tex.name = _name;
                MaterialManager.AddTexture(tex);
            }
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
            map.LoadMaterials();
            SetupGlobalLight = map.LoadGlobalLight;
        }

        public bool isKMEv2;
        public float gridAlign;
        public int startingGun;
        public Vector3 startPosition;
        public float startOrientation;

        public LevelObject[] Objects;
        public ObjectGroup GlobalObject;

        public string AutomataScript;
        public Action<Light> SetupGlobalLight = delegate { };

        public class LevelObject
        {
            // kme v2 removed group names
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
            public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, int textureId, Color color, string name, bool bounce, bool glass, bool lava, bool disableTrigger, bool markAsObject, GeometryShape shape = GeometryShape.Cube)
            {
                IsPrefab = false;
                MaterialId = MaterialManager.InstanceMaterial(textureId, color, glass | lava);
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

            public LevelObject(PrefabType prefabId, Vector3 position, Vector3 rotation, Vector3 scale, string name, string groupName, int prefabData)
            {
                IsPrefab = true;
                PrefabId = prefabId;

                Position = position;
                Rotation = rotation;
                Scale = scale;

                Name = name;
                GroupName = groupName;

                PrefabData = prefabData;
            }
            public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, int textureId, Color color, string name, string groupName, bool bounce, bool glass, bool lava, bool disableTrigger, bool markAsObject, GeometryShape shape = GeometryShape.Cube)
            {
                IsPrefab = false;
                MaterialId = MaterialManager.InstanceMaterial(textureId, color, glass | lava);
                ShapeId = shape;

                Position = position;
                Rotation = rotation;
                Scale = scale;

                Name = name;
                GroupName = groupName;
                Bounce = bounce;
                Glass = glass && !disableTrigger;
                Lava = lava;
                MarkAsObject = markAsObject;
            }
            public LevelObject() { }

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
