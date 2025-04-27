using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static KarlsonMapEditor.LevelEditor;
using UnityEngine;
using TMPro;
using UnityEngine.AI;
using MoonSharp.Interpreter;

namespace KarlsonMapEditor
{
    public enum ObjectType
    {
        Geometry,
        Prefab,
        Light,
        Text,
        Internal,
    }

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
        Enemy
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
            LuaScript = "";
        }

        private void LoadLevel_Version2(BinaryReader br)
        {
            isKMEv2 = false;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            GlobalObject = ReadObjectGroup_v1(br, true);
            AutomataScript = "";
            LuaScript = "";
        }

        private void LoadLevel_Version3(BinaryReader br)
        {
            isKMEv2 = true;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
            AutomataScript = "";
            LuaScript = "";
        }

        private void LoadLevel_Version4(BinaryReader br)
        {
            isKMEv2 = true;
            SetupMaterials = SetupMaterialsOld;
            ReadGlobalProperties(br);
            ReadTextures(br);
            AutomataScript = br.ReadString();
            GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
            LuaScript = "";
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
            LuaScript = map.LuaScript;
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
                    objGroup.Objects.Add(new LevelObject(
                        prefabId: (PrefabType)br.ReadInt32(),
                        position: br.ReadVector3(),
                        rotation: br.ReadVector3(),
                        scale: br.ReadVector3(), 
                        name: name, 
                        prefabData: br.ReadInt32()));
                else
                    objGroup.Objects.Add(new LevelObject(
                        position: br.ReadVector3(), 
                        rotation: br.ReadVector3(), 
                        scale: br.ReadVector3(), 
                        textureId: br.ReadInt32(), 
                        color: br.ReadColor(), 
                        name: name, 
                        bounce: br.ReadBoolean(), 
                        glass: br.ReadBoolean(), 
                        lava: br.ReadBoolean(), 
                        disableTrigger: br.ReadBoolean(), 
                        markAsObject: objLayerData ? br.ReadBoolean() : false, 
                        shape: GeometryShape.Cube, 
                        textureData: OldTextureData));
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
                        objGroup.Objects.Add(new LevelObject(
                            prefabId: (PrefabType)br.ReadInt32(), 
                            position: br.ReadVector3(), 
                            rotation: br.ReadVector3(), 
                            scale: br.ReadVector3(), 
                            name: name, 
                            prefabData: br.ReadInt32()));
                    else
                        objGroup.Objects.Add(new LevelObject(
                            position: br.ReadVector3(), 
                            rotation: br.ReadVector3(), 
                            scale: br.ReadVector3(), 
                            textureId: br.ReadInt32(), 
                            color: br.ReadColor(), 
                            name: name, 
                            bounce: br.ReadBoolean(), 
                            glass: br.ReadBoolean(), 
                            lava: br.ReadBoolean(), 
                            disableTrigger: br.ReadBoolean(), 
                            markAsObject: br.ReadBoolean(), 
                            shape: GeometryShape.Cube, 
                            textureData: OldTextureData));
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
        public string LuaScript;
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
            // clone existing level object
            public LevelObject(LevelObject model)
            {
                Type = model.Type;
                Position = model.Position;
                Rotation = model.Rotation;
                Scale = model.Scale;
                Name = model.Name;
                PrefabId = model.PrefabId;
                PrefabData = model.PrefabData;
                ShapeId = model.ShapeId;
                MaterialId = model.MaterialId;
                UVNormalizedScale = model.UVNormalizedScale;
                Bounce = model.Bounce;
                Glass = model.Glass;
                Lava = model.Lava;
                MarkAsObject = model.MarkAsObject;
                Color = model.Color;
                LightType = model.LightType;
                Intensity = model.Intensity;
                Range = model.Range;
                SpotAngle = model.SpotAngle;
                Text = model.Text;
            }
            // prefab
            [MoonSharpHidden]
            public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, string name, PrefabType prefabId, int prefabData)
            {
                Type = ObjectType.Prefab;
                PrefabId = prefabId;

                Position = position;
                Rotation = rotation;
                Scale = scale;

                Name = name;

                PrefabData = prefabData;
            }
            // geometry
            [MoonSharpHidden]
            public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, string name, int textureId, Color color, bool bounce, bool glass, bool lava, bool disableTrigger, bool markAsObject, GeometryShape shape = GeometryShape.Cube, List<(int, Color, bool)> textureData = null)
            {
                Type = ObjectType.Geometry;

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
            public ObjectType Type;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 Scale = Vector3.one;
            public string Name;

            // prefab specific
            public PrefabType PrefabId;
            public int PrefabData;

            // geometry specific
            public GeometryShape ShapeId;
            public int MaterialId;
            public float UVNormalizedScale = 10;
            public bool Bounce;
            public bool Glass;
            public bool Lava;
            public bool MarkAsObject;

            // light and text
            public Color Color;

            // light specific
            public LightType LightType;
            public float Intensity;
            public float Range;
            public float SpotAngle;

            // text specific
            public string Text;

            public override string ToString()
            {
                string st = "(Type:" + Type.ToString();
                if (Type == ObjectType.Prefab)
                    st += " " + PrefabId;
                st += " " + Position + " " + Rotation + " " + Scale;
                if (Type == ObjectType.Geometry)
                    st += " mat:" + MaterialId;
                st += ")";
                return st;
            }

            private System.Reflection.MethodInfo GiveGun = typeof(Enemy).GetMethod("GiveGun", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            [MoonSharpHidden]
            public void setGun(GameObject go)
            {
                if (Type != ObjectType.Prefab || PrefabId != PrefabType.Enemy) return;
                Enemy e = go.GetComponent<Enemy>();
                if (e == null) return;

                if (e.currentGun != null)
                {
                    Object.Destroy(e.currentGun);
                    e.currentGun = null;
                }
                if (PrefabData != 0)
                {
                    PrefabType gun = (PrefabType)(PrefabData - 1);
                    e.startGun = MakePrefab(gun);
                    GiveGun.Invoke(e, null);

                    Object.Destroy(e.startGun);
                    e.startGun = null;
                }
            }

            public GameObject LoadObject(GameObject parent, bool originalScale = false) // used by lua as well
            {
                return LoadObject(parent, true, originalScale);
            }
            [MoonSharpHidden]
            public GameObject LoadEditorObject(GameObject parent, bool newObject)
            {
                return LoadObject(parent, false, newObject);
            }
            private GameObject LoadObject(GameObject parent, bool playMode, bool originalScale = false)
            {
                GameObject go;
                
                switch (Type)
                {
                    case ObjectType.Prefab:
                        go = MakePrefab(PrefabId);
                        if (originalScale) Scale = go.transform.localScale;
                        if (PrefabId == PrefabType.Enemy) setGun(go);
                        if (!playMode && go.GetComponent<Rigidbody>() != null)
                            go.GetComponent<Rigidbody>().isKinematic = true;
                        break;
                    case ObjectType.Geometry:
                        go = MeshBuilder.GetGeometryGO(ShapeId);
                        go.GetComponent<KMETextureScaling>().Scale = UVNormalizedScale;
                        go.GetComponent<MeshRenderer>().sharedMaterial = MaterialManager.Materials[MaterialId];
                        if (playMode)
                        {
                            // set up breakable glass
                            if (Glass)
                            {
                                go.GetComponent<Collider>().isTrigger = true;
                                Glass newGlass = go.AddComponent<Glass>();

                                // copy in the required GOs
                                GameObject prefabGlassCube = LoadsonAPI.PrefabManager.NewGlass();
                                Glass prefabGlass = prefabGlassCube.GetComponent<Glass>();
                                newGlass.glass = prefabGlass.glass;
                                newGlass.glassSfx = prefabGlass.glassSfx;
                                Object.Destroy(prefabGlass);
                                Object.Destroy(prefabGlassCube);

                                // reset the transform
                                newGlass.glass.transform.SetParent(go.transform);
                                newGlass.glass.transform.localPosition = Vector3.zero;
                                newGlass.glass.transform.localScale = Vector3.one;
                                newGlass.glass.transform.localRotation = Quaternion.identity;

                                // fix particle system
                                ParticleSystem ps = newGlass.glass.GetComponent<ParticleSystem>();
                                ParticleSystem.ShapeModule shape = ps.shape;
                                shape.scale = Scale;
                                shape.rotation = Rotation;
                                float volume = shape.scale.x * shape.scale.y * shape.scale.z;
                                ParticleSystem.MainModule main = ps.main;
                                main.maxParticles = Math.Max((int)(1000f * volume / 160f), 1);
                            }
                            // set up deadly lava
                            else if (Lava)
                            {
                                go.GetComponent<Collider>().isTrigger = true;
                                go.AddComponent<Lava>();
                            }
                            // disable wallrun and grappling on objects
                            if (MarkAsObject)
                                go.layer = LayerMask.NameToLayer("Object");
                            // set up bounce
                            if (Bounce)
                                go.GetComponent<Collider>().material = LoadsonAPI.PrefabManager.BounceMaterial();
                            // prevent enemies from walking on objects they shouldn't be on
                            if (Glass || Lava || Bounce)
                            {
                                NavMeshModifier modifier = go.AddComponent<NavMeshModifier>();
                                modifier.area = NavMesh.GetAreaFromName("Not Walkable");
                                modifier.overrideArea = true;
                            }
                        }
                        break;
                    case ObjectType.Light:
                        go = new GameObject();
                        Light light = go.AddComponent<Light>();
                        light.type = LightType;
                        light.color = Color;
                        light.intensity = Intensity;
                        light.range = Range;
                        light.spotAngle = SpotAngle;
                        Scale = Vector3.one;
                        if (!playMode)
                        {
                            // clickable hitbox
                            go.AddComponent<SphereCollider>().radius = 0.4f;
                            // render as billboard
                            GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Quad);
                            vis.transform.parent = go.transform;
                            vis.GetComponent<MeshRenderer>().material = MaterialManager.InstanceLightMaterial(Color);
                            Object.Destroy(vis.GetComponent<MeshCollider>());
                        }
                        break;
                    case ObjectType.Text:
                        go = new GameObject();
                        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
                        tmp.alignment = TextAlignmentOptions.Center;
                        tmp.enableWordWrapping = false;
                        tmp.text = Text;
                        tmp.color = Color;
                        if (!playMode)
                            go.AddComponent<MeshCollider>().sharedMesh = tmp.mesh;
                        break;
                    default:
                        go = new GameObject();
                        break;
                }
                go.name = Name;
                go.transform.parent = parent.transform;
                go.transform.localPosition = Position;
                go.transform.localRotation = Quaternion.Euler(Rotation);
                go.transform.localScale = Scale;
                if (playMode && Type == ObjectType.Prefab) go.transform.SetParent(null, true);
                return go;
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

            public GameObject LoadObject(GameObject parent)
            {
                GameObject go = new GameObject(Name);
                if (parent != null)
                {
                    go.transform.parent = parent.transform;
                }
                go.transform.localPosition = Position;
                go.transform.localRotation = Quaternion.Euler(Rotation);
                go.transform.localScale = Scale;

                return go;
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
                case PrefabType.Enemy:
                    return LoadsonAPI.PrefabManager.NewEnemy();
                default:
                    return null;
            }
        }
    }
}
