using HarmonyLib;
using LoadsonAPI;
using SevenZip.Compression.LZMA;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace KarlsonMapEditor
{
    public static class LevelPlayer
    {
        public static string currentLevel { get; private set; } = "";
        public static void ExitedLevel() => currentLevel = "";
        private static LevelData levelData;

        public static void LoadLevel(string levelPath)
        {
            currentLevel = Path.GetFileName(levelPath);
            levelData = new LevelData(File.ReadAllBytes(levelPath));
            SceneManager.sceneLoaded += LoadLevelData;
            UnityEngine.Object.FindObjectOfType<Lobby>().LoadMap("4Escape0");
        }
        public static void LoadLevel(string name, byte[] data)
        {
            currentLevel = name;
            levelData = new LevelData(data);
            SceneManager.sceneLoaded += LoadLevelData;
            UnityEngine.Object.FindObjectOfType<Lobby>().LoadMap("4Escape0");
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

            foreach (var obj in levelData.Objects)
            {
                GameObject go;
                if (obj.IsPrefab)
                {
                    go = LevelData.MakePrefab(obj.PrefabId);
                    if(obj.PrefabId == 11)
                    {
                        Enemy e = go.GetComponent<Enemy>();
                        if(obj.PrefabData != 0)
                        {
                            e.startGun = LevelData.MakePrefab(obj.PrefabData - 1);
                            typeof(Enemy).GetMethod("GiveGun", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(e, Array.Empty<object>());
                        }
                    }
                }
                else
                {
                    if(obj.Lava)
                    {
                        go = LoadsonAPI.PrefabManager.NewGlass();
                        UnityEngine.Object.Destroy(go.GetComponent<Glass>());
                        go.AddComponent<Lava>();
                    }
                    else if (obj.Glass)
                        go = LoadsonAPI.PrefabManager.NewGlass();
                    else
                        go = LoadsonAPI.PrefabManager.NewCube();
                    if (obj.TextureId < Main.gameTex.Length)
                        go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.TextureId];
                    else
                        go.GetComponent<MeshRenderer>().material.mainTexture = levelData.Textures[obj.TextureId - Main.gameTex.Length];
                    go.GetComponent<MeshRenderer>().material.color = obj._Color;
                    if (obj.Bounce)
                        go.GetComponent<BoxCollider>().material = LoadsonAPI.PrefabManager.BounceMaterial();
                    if (obj.DisableTrigger)
                        go.GetComponent<BoxCollider>().isTrigger = false;
                }
                go.transform.position = obj.Position;
                go.transform.rotation = Quaternion.Euler(obj.Rotation);
                go.transform.localScale = obj.Scale;
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
                        if(prefab)
                            objects.Add(new LevelObject(br.ReadInt32(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), name, group, br.ReadInt32()));
                        else
                            objects.Add(new LevelObject(br.ReadVector3(), br.ReadVector3(), br.ReadVector3(), br.ReadInt32(), br.ReadColor(), name, group, br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean(), br.ReadBoolean()));
                    }
                    Objects = objects.ToArray();
                }
            }

            public float gridAlign;
            public int startingGun;
            public Vector3 startPosition;
            public float startOrientation;

            public Texture2D[] Textures;
            public LevelObject[] Objects;

            public class LevelObject
            {
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
                public LevelObject(Vector3 position, Vector3 rotation, Vector3 scale, int textureId, Color color, string name, string groupName, bool bounce, bool glass, bool lava, bool disableTrigger)
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

                public int PrefabData;

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
}
