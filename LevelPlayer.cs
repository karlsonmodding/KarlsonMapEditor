using HarmonyLib;
using KarlsonMapEditor.Scripting_API;
using Loadson;
using LoadsonAPI;
using SevenZip.Compression.LZMA;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static KarlsonMapEditor.LevelEditor;

namespace KarlsonMapEditor
{
    public static class LevelPlayer
    {
        public static string currentLevel { get; private set; } = "";
        public static void ExitedLevel() => currentLevel = "";
        private static LevelData levelData;
        private static Automata.Backbone.FunctionRunner mainFunction = null;
        public static ScriptRunner currentScript { get; private set; } = null;
        private static bool dirtyNavMesh;
        private static bool needsNavMesh;

        static void LoadScript()
        {
            if (levelData.AutomataScript.Trim().Length > 0)
            { // load level script
                var tokens = Automata.Parser.Tokenizer.Tokenize(Automata.Parser.ProgramCleaner.CleanProgram(levelData.AutomataScript));
                var program = new Automata.Parser.ProgramParser(tokens).ParseProgram();
                mainFunction = new Automata.Backbone.FunctionRunner(new List<(Automata.Backbone.VarResolver, Automata.Backbone.BaseValue.ValueType)> { }, program);
            }
        }

        public static void LoadLevel(string levelPath)
        {
            currentLevel = Path.GetFileName(levelPath);
            levelData = new LevelData(File.ReadAllBytes(levelPath));
            LoadScript();
            dirtyNavMesh = true;
            needsNavMesh = false;
            SceneManager.sceneLoaded += LoadLevelData;
            UnityEngine.Object.FindObjectOfType<Lobby>().LoadMap("4Escape0");
        }
        public static void LoadLevel(string name, byte[] data)
        {
            currentLevel = name;
            levelData = new LevelData(data);
            LoadScript();
            dirtyNavMesh = true;
            needsNavMesh = false;
            SceneManager.sceneLoaded += LoadLevelData;
            UnityEngine.Object.FindObjectOfType<Lobby>().LoadMap("4Escape0");
        }

        public static void GenerateNavMesh()
        {
            if (dirtyNavMesh && needsNavMesh)
            {
                GameObject navmeshGO = new GameObject("NavMesh Surface");
                navmeshGO.transform.position = levelData.startPosition;
                var navmeshs = navmeshGO.AddComponent<NavMeshSurface>();
                navmeshs.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                navmeshs.collectObjects = CollectObjects.All;
                navmeshs.BuildNavMesh();
                Loadson.Console.Log("navmesh pos: " + navmeshs.navMeshData.position);
            }
            dirtyNavMesh = false;
        }

        public static void LoadLevelData(Scene arg0, LoadSceneMode arg1)
        {
            foreach (Collider c in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (c.gameObject != PlayerMovement.Instance.gameObject && c.gameObject.GetComponent<DetectWeapons>() == null) UnityEngine.Object.Destroy(c.gameObject);
            if(levelData.startingGun != 0)
                PlayerMovement.Instance.spawnWeapon = LevelData.MakePrefab(levelData.startingGun - 1);

            PlayerMovement.Instance.transform.position = levelData.startPosition;
            PlayerMovement.Instance.playerCam.transform.localRotation = Quaternion.Euler(0f, levelData.startOrientation, 0f);
            PlayerMovement.Instance.orientation.transform.localRotation = Quaternion.Euler(0f, levelData.startOrientation, 0f);

            if (!levelData.isKMEv2)
                LoadLevelData_v2();
            else
            {
                List<LevelData.LevelObject> enemyToFix = new List<LevelData.LevelObject>();
                void ReplicateObjectGroup(LevelData.ObjectGroup group, GameObject parentObject)
                {
                    GameObject objGroup = new GameObject(group.Name);
                    if (parentObject != null)
                        objGroup.transform.parent = parentObject.transform;
                    objGroup.transform.localPosition = group.Position;
                    objGroup.transform.localRotation = Quaternion.Euler(group.Rotation);
                    objGroup.transform.localScale = group.Scale;
                    foreach (var obj in group.Objects)
                    {
                        GameObject go;
                        if (obj.IsPrefab)
                        {
                            go = LevelData.MakePrefab(obj.PrefabId);
                            go.name = obj.Name;
                            go.transform.parent = objGroup.transform;
                            go.transform.localPosition = obj.Position;
                            go.transform.localRotation = Quaternion.Euler(obj.Rotation);
                            go.transform.localScale = obj.Scale;
                            go.transform.parent = null;
                            if (obj.PrefabId == 11)
                            {
                                needsNavMesh = true;
                                enemyToFix.Add(obj);
                                obj._enemyFixY = go.transform.position.y;
                                Enemy e = go.GetComponent<Enemy>();
                                if (obj.PrefabData != 0)
                                {
                                    e.startGun = LevelData.MakePrefab(obj.PrefabData - 1);
                                    typeof(Enemy).GetMethod("GiveGun", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(e, Array.Empty<object>());

                                    go.GetComponent<NavMeshAgent>().enabled = true;
                                    go.GetComponent<NavMeshAgent>().enabled = false;
                                    go.GetComponent<NavMeshAgent>().enabled = true;
                                }
                                obj._enemyFix = go;
                            }
                            continue;
                        }

                        if (obj.Glass)
                        {
                            go = LoadsonAPI.PrefabManager.NewGlass();
                            // fix collider
                            go.GetComponent<BoxCollider>().size = Vector3.one;
                            if (obj.DisableTrigger)
                            {
                                go.GetComponent<BoxCollider>().isTrigger = false;
                                UnityEngine.Object.Destroy(go.GetComponent<Glass>());
                            }
                        }
                        else if (obj.Lava)
                        {
                            go = LoadsonAPI.PrefabManager.NewGlass();
                            UnityEngine.Object.Destroy(go.GetComponent<Glass>());
                            go.AddComponent<Lava>();
                        }
                        else
                            go = LoadsonAPI.PrefabManager.NewCube();
                        if (obj.MarkAsObject)
                            // set layer to object so you can't wallrun / grapple
                            go.layer = LayerMask.NameToLayer("Object");
                        if (obj.TextureId < Main.gameTex.Length)
                            go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.TextureId];
                        else
                            go.GetComponent<MeshRenderer>().material.mainTexture = levelData.Textures[obj.TextureId - Main.gameTex.Length];
                        go.GetComponent<MeshRenderer>().material.color = obj._Color;
                        if (obj.Bounce)
                            go.GetComponent<BoxCollider>().material = LoadsonAPI.PrefabManager.BounceMaterial();
                        go.name = obj.Name;
                        go.transform.parent = objGroup.transform;
                        go.transform.localPosition = obj.Position;
                        go.transform.localRotation = Quaternion.Euler(obj.Rotation);
                        go.transform.localScale = obj.Scale;
                        if(obj.Glass)
                        {
                            // fix particle system
                            var ps = go.GetComponent<Glass>().glass.GetComponent<ParticleSystem>();
                            var shape = ps.shape;
                            shape.scale = go.transform.lossyScale;
                            var volume = (shape.scale.x * shape.scale.y * shape.scale.z);
                            var main = ps.main;
                            main.maxParticles = Math.Max((int)(1000f * volume / 160f), 1);
                            main.startSpeed = 0.5f;
                        }
                    }
                    foreach (var grp in group.Groups)
                        ReplicateObjectGroup(grp, objGroup);
                }
                ReplicateObjectGroup(levelData.GlobalObject, null);

                GenerateNavMesh();
                IEnumerator enemyFix()
                {
                    yield return new WaitForEndOfFrame();
                    foreach (var obj in enemyToFix)
                    {
                        if (obj.PrefabId == 11)
                        {
                            float deltaY = obj._enemyFix.GetComponent<NavMeshAgent>().nextPosition.y - obj._enemyFixY;
                            Loadson.Console.Log("delta: " + deltaY);
                            obj._enemyFix.AddComponent<Enemy_ProjectPos>().delta = deltaY;
                        }
                    }
                }
                Coroutines.StartCoroutine(enemyFix());

                // load script
                if (mainFunction != null)
                    currentScript = new ScriptRunner(mainFunction);
                else
                    currentScript = null;
            }

            void LoadLevelData_v2()
            {
                foreach (var obj in levelData.Objects)
                {
                    if (obj.IsPrefab) continue;
                    GameObject go;
                    if (obj.Glass)
                    {
                        go = LoadsonAPI.PrefabManager.NewGlass();
                        if (obj.DisableTrigger)
                        {
                            // fix collider
                            go.GetComponent<BoxCollider>().isTrigger = false;
                            go.GetComponent<BoxCollider>().size = Vector3.one;
                            UnityEngine.Object.Destroy(go.GetComponent<Glass>());
                        }
                    }
                    else if (obj.Lava)
                    {
                        go = LoadsonAPI.PrefabManager.NewGlass();
                        UnityEngine.Object.Destroy(go.GetComponent<Glass>());
                        go.AddComponent<Lava>();
                    }
                    else
                        go = LoadsonAPI.PrefabManager.NewCube();
                    if (obj.MarkAsObject)
                        // set layer to object so you can't wallrun / grapple
                        go.layer = LayerMask.NameToLayer("Object");
                    if (obj.TextureId < Main.gameTex.Length)
                        go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.TextureId];
                    else
                        go.GetComponent<MeshRenderer>().material.mainTexture = levelData.Textures[obj.TextureId - Main.gameTex.Length];
                    go.GetComponent<MeshRenderer>().material.color = obj._Color;
                    if (obj.Bounce)
                        go.GetComponent<BoxCollider>().material = LoadsonAPI.PrefabManager.BounceMaterial();
                    go.transform.position = obj.Position;
                    go.transform.rotation = Quaternion.Euler(obj.Rotation);
                    go.transform.localScale = obj.Scale;
                }

                foreach (var obj in levelData.Objects)
                {
                    if (!obj.IsPrefab) continue;
                    GameObject go;
                    go = LevelData.MakePrefab(obj.PrefabId);
                    go.transform.position = obj.Position;
                    go.transform.rotation = Quaternion.Euler(obj.Rotation);
                    go.transform.localScale = obj.Scale;
                    if (obj.PrefabId == 11)
                    {
                        needsNavMesh = true;
                        Enemy e = go.GetComponent<Enemy>();
                        if (obj.PrefabData != 0)
                        {
                            e.startGun = LevelData.MakePrefab(obj.PrefabData - 1);
                            typeof(Enemy).GetMethod("GiveGun", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(e, Array.Empty<object>());

                            go.GetComponent<NavMeshAgent>().enabled = true;
                            go.GetComponent<NavMeshAgent>().enabled = false;
                            go.GetComponent<NavMeshAgent>().enabled = true;
                        }
                        obj._enemyFix = go;
                    }
                }
                GenerateNavMesh();
                IEnumerator enemyFix()
                {
                    yield return new WaitForEndOfFrame();
                    foreach (var obj in levelData.Objects)
                    {
                        if (obj.PrefabId == 11)
                        {
                            float deltaY = obj._enemyFix.GetComponent<NavMeshAgent>().nextPosition.y - obj.Position.y;
                            Loadson.Console.Log("delta: " + deltaY);
                            obj._enemyFix.AddComponent<Enemy_ProjectPos>().delta = deltaY;
                        }
                    }
                }
                Coroutines.StartCoroutine(enemyFix());
            }
        }

        public class LevelData
        {
            public static readonly PrimitiveType[] typeToInt = new PrimitiveType[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder, PrimitiveType.Plane, PrimitiveType.Quad };

            public LevelData(byte[] _data)
            {
                // decompress
                byte[] data = SevenZipHelper.Decompress(_data);
                using(BinaryReader br = new BinaryReader(new MemoryStream(data)))
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
                            objGroup.Objects.Add(new LevelObject(br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, br.ReadInt32()));
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
                gridAlign = br.ReadSingle();
                startingGun = br.ReadInt32();
                startPosition = br.ReadVector3();
                startOrientation = br.ReadSingle();
                int _len;
                List<Texture2D> list = new List<Texture2D>();
                int _texl = br.ReadInt32();
                while (_texl-- > 0)
                {
                    string _name = br.ReadString();
                    _len = br.ReadInt32();
                    list.Add(new Texture2D(1, 1));
                    list.Last().LoadImage(br.ReadBytes(_len));
                    list.Last().name = _name;
                }
                Textures = list.ToArray();
                List<LevelObject> objects = new List<LevelObject>();
                _texl = br.ReadInt32();
                while (_texl-- > 0)
                {
                    bool prefab = br.ReadBoolean();
                    string name = br.ReadString();
                    string group = br.ReadString();
                    if (prefab)
                        objects.Add(new LevelObject(br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, group, br.ReadInt32()));
                    else
                        objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, group, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), false));
                }
                Objects = objects.ToArray();
                AutomataScript = "";
            }

            private void LoadLevel_Version2(BinaryReader br)
            {
                isKMEv2 = false;
                gridAlign = br.ReadSingle();
                startingGun = br.ReadInt32();
                startPosition = br.ReadVector3();
                startOrientation = br.ReadSingle();
                int _len;
                List<Texture2D> list = new List<Texture2D>();
                int _texl = br.ReadInt32();
                while (_texl-- > 0)
                {
                    string _name = br.ReadString();
                    _len = br.ReadInt32();
                    list.Add(new Texture2D(1, 1));
                    list.Last().LoadImage(br.ReadBytes(_len));
                    list.Last().name = _name;
                }
                Textures = list.ToArray();
                List<LevelObject> objects = new List<LevelObject>();
                _texl = br.ReadInt32();
                while (_texl-- > 0)
                {
                    bool prefab = br.ReadBoolean();
                    string name = br.ReadString();
                    string group = br.ReadString();
                    if (prefab)
                        objects.Add(new LevelObject(br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, group, br.ReadInt32()));
                    else
                        objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, group, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean()));
                }
                Objects = objects.ToArray();
                AutomataScript = "";
            }

            private void LoadLevel_Version3(BinaryReader br)
            {
                isKMEv2 = true;
                gridAlign = br.ReadSingle();
                startingGun = br.ReadInt32();
                startPosition = br.ReadVector3();
                startOrientation = br.ReadSingle();
                int _len;
                List<Texture2D> list = new List<Texture2D>();
                int _texl = br.ReadInt32();
                while (_texl-- > 0)
                {
                    string _name = br.ReadString();
                    _len = br.ReadInt32();
                    list.Add(new Texture2D(1, 1));
                    list.Last().LoadImage(br.ReadBytes(_len));
                    list.Last().name = _name;
                }
                Textures = list.ToArray();
                GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
                AutomataScript = "";
            }

            private void LoadLevel_Version4(BinaryReader br)
            {
                isKMEv2 = true;
                gridAlign = br.ReadSingle();
                startingGun = br.ReadInt32();
                startPosition = br.ReadVector3();
                startOrientation = br.ReadSingle();
                int _len;
                List<Texture2D> list = new List<Texture2D>();
                int _texl = br.ReadInt32();
                while (_texl-- > 0)
                {
                    string _name = br.ReadString();
                    _len = br.ReadInt32();
                    list.Add(new Texture2D(1, 1));
                    list.Last().LoadImage(br.ReadBytes(_len));
                    list.Last().name = _name;
                }
                Textures = list.ToArray();
                AutomataScript = br.ReadString();
                GlobalObject = ReadObjectGroup_v3(br.ReadByteArray());
            }

            public bool isKMEv2;
            public float gridAlign;
            public int startingGun;
            public Vector3 startPosition;
            public float startOrientation;

            public Texture2D[] Textures;
            public LevelObject[] Objects;
            public ObjectGroup GlobalObject;

            public string AutomataScript;

            public class LevelObject
            {
                // kme v2 removed group names
                public LevelObject(int prefabId, Vector3 position, Vector3 rotation, Vector3 scale, string name, int prefabData)
                {
                    IsPrefab = true;
                    PrefabId = prefabId;

                    Position = position;
                    Rotation = rotation;
                    Scale = scale;

                    Name = name;

                    PrefabData = prefabData;
                }
                public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, int textureId, Color color, string name, bool bounce, bool glass, bool lava, bool disableTrigger, bool markAsObject)
                {
                    IsPrefab = false;
                    TextureId = textureId;

                    Position = position;
                    Rotation = rotation;
                    Scale = scale;
                    _Color = color;

                    Name = name;
                    Bounce = bounce;
                    Glass = glass;
                    Lava = lava;
                    DisableTrigger = disableTrigger;
                    MarkAsObject = markAsObject;
                }

                public LevelObject(int prefabId, Vector3 position, Vector3 rotation, Vector3 scale, string name, string groupName, int prefabData)
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
                public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, int textureId, Color color, string name, string groupName, bool bounce, bool glass, bool lava, bool disableTrigger, bool markAsObject)
                {
                    IsPrefab = false;
                    TextureId = textureId;

                    Position = position;
                    Rotation = rotation;
                    Scale = scale;
                    _Color = color;

                    Name = name;
                    GroupName = groupName;
                    Bounce = bounce;
                    Glass = glass;
                    Lava = lava;
                    DisableTrigger = disableTrigger;
                    MarkAsObject = markAsObject;
                }

                public bool IsPrefab;
                public Vector3 Position;
                public Vector3 Rotation;
                public Vector3 Scale;
                public Color _Color;
                public string Name;
                public string GroupName;

                public int PrefabId;

                public int TextureId;
                public bool Bounce;
                public bool Glass;
                public bool Lava;
                public bool DisableTrigger;
                public bool MarkAsObject;

                public int PrefabData;
                public GameObject _enemyFix;
                public float _enemyFixY;

                public override string ToString()
                {
                    string st = "(PF:" + IsPrefab;
                    if (IsPrefab)
                        st += " " + PrefabId;
                    st += " " + Position + " " + Rotation + " " + Scale;
                    if (!IsPrefab)
                        st += " tex:" + TextureId;
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

            public static GameObject MakePrefab(int id)
            {
                switch (id)
                {
                    default:
                    case 0:
                        return LoadsonAPI.PrefabManager.NewPistol();
                    case 1:
                        return LoadsonAPI.PrefabManager.NewAk47();
                    case 2:
                        return LoadsonAPI.PrefabManager.NewShotgun();
                    case 3:
                        return LoadsonAPI.PrefabManager.NewBoomer();
                    case 4:
                        return LoadsonAPI.PrefabManager.NewGrappler();
                    case 5:
                        return LoadsonAPI.PrefabManager.NewDummyGrappler();
                    case 6:
                        return LoadsonAPI.PrefabManager.NewTable();
                    case 7:
                        return LoadsonAPI.PrefabManager.NewBarrel();
                    case 8:
                        return LoadsonAPI.PrefabManager.NewLocker();
                    case 9:
                        return LoadsonAPI.PrefabManager.NewScreen();
                    case 10:
                        return LoadsonAPI.PrefabManager.NewMilk();
                    case 11:
                        return LoadsonAPI.PrefabManager.NewEnemy();
                }
            }
            public static string PrefabToName(int id)
            {
                switch (id)
                {
                    default:
                    case 0:
                        return "Pistol";
                    case 1:
                        return "Ak47";
                    case 2:
                        return "Shotgun";
                    case 3:
                        return "Boomer";
                    case 4:
                        return "Grappler";
                    case 5:
                        return "Dummy Grappler";
                    case 6:
                        return "Table";
                    case 7:
                        return "Barrel";
                    case 8:
                        return "Locker";
                    case 9:
                        return "Screen";
                    case 10:
                        return "Milk";
                    case 11:
                        return "Enemy";
                }
            }
        }
    }

    [HarmonyPatch(typeof(Game), "MainMenu")]
    class Hook_Game_MainMenu
    {
        static void Postfix()
        {
            LevelPlayer.ExitedLevel();
            SceneManager.sceneLoaded -= LevelPlayer.LoadLevelData;
        }
    }

    [HarmonyPatch(typeof(Glass), "OnTriggerEnter")]
    class Hook_Glass_OnTriggerEnter
    {
        static bool Prefix(Glass __instance, Collider other)
        {
            if (LevelPlayer.currentLevel == "") return true;
            if (LevelPlayer.currentScript == null) return true;
            // determine break reason
            if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
                return false; // collide with ground, ignore
            int reason = 0;
            if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
                reason = 1;
            if (other.gameObject.layer == LayerMask.NameToLayer("Bullet"))
            {
                if (other.gameObject.name == "Damage")
                    reason = 3;
                else
                    reason = 2;
            }
            var ret = LevelPlayer.currentScript.InvokeFunction("onbreak", __instance, other, reason);
            if (!ret.HoldsTrue() || ret.Type != Automata.Backbone.BaseValue.ValueType.Number) return true; // continue normal execution
            var retN = (double)ret.Value;
            if(retN > 0) // insta-break
                UnityEngine.Object.Destroy(__instance.gameObject);
            return false;
        }
    }
}
