using Loadson;
using LoadsonAPI;
using SevenZip.Compression.LZMA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace KarlsonMapEditor
{
    public static class LevelEditor
    {
        private static bool _initd = false;
        private static void _init()
        {
            if(_initd) return;
            _initd = true;
            wid = new int[]{ ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId() };
            wir = new Rect[10];
            wir[(int)WindowId.Startup] = new Rect((Screen.width - 600) / 2, (Screen.height - 300) / 2, 600, 300);
            wir[(int)WindowId.Prompt] = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 35, 200, 70);
            wir[(int)WindowId.TexBrowser] = new Rect((Screen.width - 800) / 2, (Screen.height - 860) / 2, 800, 860);
            wir[(int)WindowId.TexPreview] = new Rect((Screen.width - 400) / 2, (Screen.height - 400) / 2, 400, 400);
            wir[(int)WindowId.TexPick] = new Rect((Screen.width - 800) / 2, (Screen.height - 400) / 2, 800, 400);
            wir[(int)WindowId.LevelBrowser] = new Rect(Screen.width - 305, 30, 300, 500);
            wir[(int)WindowId.ObjectManip] = new Rect(Screen.width - 305, 540, 300, 500);
            wir[(int)WindowId.ObjectGroup] = new Rect(Screen.width - 510, 760, 200, 65);
            wir[(int)WindowId.ObjectGroupManip] = new Rect(Screen.width - 510, 830, 200, 125);
            wir[(int)WindowId.LevelData] = new Rect(Screen.width - 510, 30, 200, 185);

            multiPick = new GUIStyle();
            Texture2D orange = new Texture2D(1,1);
            orange.SetPixel(0, 0, new Color(1f, 0.64f, 0f));
            orange.Apply();
            multiPick.active.background = orange;
            multiPick.active.textColor = Color.white;
            multiPick.normal.background = orange;
            multiPick.normal.textColor = Color.white;
            multiPick.focused.background = orange;
            multiPick.hover.background = orange;

            picker = new ColorPicker(Color.white, Screen.width - 475, 540);
        }
        private enum WindowId
        {
            Startup = 0,
            Prompt,
            TexBrowser,
            TexPreview,
            TexPick,
            LevelBrowser,
            ObjectManip,
            ObjectGroup,
            ObjectGroupManip,
            LevelData,
        }

        private static GUIStyle multiPick;
        private static ColorPicker picker;

        private static int[] wid;
        private static Rect[] wir;

        public static bool editorMode { get; private set; } = false;
        public static void StartEdit()
        {
            _init();
            editorMode = true;
            levelName = "";
            SceneManager.sceneLoaded += InitEditor;
            SceneManager.LoadScene(6);
        }
        static List<GameObject> clickGizmo;
        static Camera gizmoCamera;
        static Vector3 gizmoMovePos;
        static Vector3 gizmoMoveDirection = Vector3.zero;
        static float oldGizmoDistance;

        static string targetGroup = "";
        static GUIex.Dropdown enemyGun;
        static GUIex.Dropdown startGunDD;
        static GUIex.Dropdown spawnPrefabDD;
        private static void InitEditor(Scene arg0, LoadSceneMode arg1)
        {
            SceneManager.sceneLoaded -= InitEditor;
            foreach (Collider c in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (c.gameObject != PlayerMovement.Instance.gameObject & c.gameObject.GetComponent<DetectWeapons>() == null) UnityEngine.Object.Destroy(c.gameObject);
            PlayerMovement.Instance.gameObject.GetComponent<Rigidbody>().isKinematic = false;
            PlayerMovement.Instance.gameObject.GetComponent<Rigidbody>().useGravity = false; //eh
            PlayerMovement.Instance.gameObject.GetComponent<Collider>().enabled = false;

            // create gizmo
            GameObject go1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go1.GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 0, 0, 1));
            go1.transform.position = new Vector3(5000, 5000, 5000);
            go1.transform.transform.localScale = new Vector3(1f, 1f, 1f);
            GameObject go2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go2.GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 0, 1, 1));
            go2.transform.position = new Vector3(5000, 5000, 5001);
            go2.transform.rotation = Quaternion.Euler(90, 0, 0);
            go2.transform.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            GameObject go3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go3.transform.position = new Vector3(5000, 5001, 5000);
            go3.GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 1, 0, 1));
            go3.transform.rotation = Quaternion.Euler(0, 0, 0);
            go3.transform.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            GameObject go4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go4.GetComponent<Renderer>().material.SetColor("_Color", new Color(1, 0, 0, 1));
            go4.transform.position = new Vector3(5001, 5000, 5000);
            go4.transform.rotation = Quaternion.Euler(0, 0, 90);
            go4.transform.transform.localScale = new Vector3(0.3f, 1f, 0.3f);

            // create backplane
            GameObject GObg = GameObject.CreatePrimitive(PrimitiveType.Plane);
            GObg.GetComponent<Renderer>().GetComponent<Renderer>().material.SetColor("_Color", new Color(0.5f, 0.5f, 0.5f, 1));
            GObg.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
            GObg.name = "Gizmo Backplane";

            // create clickable gizmo
            go1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go1.GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 0, 0, 1));
            go1.transform.position = new Vector3(5000, 5000, 5100);
            go1.transform.transform.localScale = new Vector3(1f, 1f, 1f);
            go1.layer = 19;
            go2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go2.GetComponent<Renderer>().material.SetColor("_Color", new Color(0.3f, 0.3f, 1, 1));
            go2.transform.position = new Vector3(5000, 5000, 5100);
            go2.transform.rotation = Quaternion.Euler(90, 0, 0);
            go2.transform.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            go2.layer = 19;
            go3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go3.transform.position = new Vector3(5000, 5000, 5100);
            go3.GetComponent<Renderer>().material.SetColor("_Color", new Color(0.3f, 1, 0.3f, 1));
            go3.transform.rotation = Quaternion.Euler(0, 0, 0);
            go3.transform.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            go3.layer = 19;
            go4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go4.GetComponent<Renderer>().material.SetColor("_Color", new Color(1, 0.3f, 0.3f, 1));
            go4.transform.position = new Vector3(5000, 5000, 5100);
            go4.transform.rotation = Quaternion.Euler(0, 0, 90);
            go4.transform.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            go4.layer = 19;
            clickGizmo = new List<GameObject> { go1, go2, go3, go4 };
            
            gizmoCamera = UnityEngine.Object.Instantiate(Camera.main);
            UnityEngine.Object.Destroy(gizmoCamera.transform.Find("GunCam").gameObject);
            UnityEngine.Object.Destroy(gizmoCamera.transform.Find("Particle System").gameObject);
            gizmoCamera.transform.parent = Camera.main.transform;
            gizmoCamera.clearFlags = CameraClearFlags.Depth;
            gizmoCamera.cullingMask = (1 << 19); // layer 19
            gizmoCamera.fieldOfView = GameState.Instance.GetFov();

            enemyGun = new GUIex.Dropdown(new string[] { "None", "Pistol", "Ak47 / Uzi", "Shotgun", "Boomer" }, 0);
            startGunDD = new GUIex.Dropdown(new string[] { "None", "Pistol", "Ak47 / Uzi", "Shotgun", "Boomer", "Grappler" }, 0);
            spawnPrefabDD = new GUIex.Dropdown(new string[] { "Spawn Prefab", "Pistol", "Ak47 / Uzi", "Shotgun", "Boomer", "Grappler", "Dummy Grappler", "Table", "Barrel", "Locker", "Screen", "Milk", "Enemy" }, 0);
        }

        public static string levelName { get; private set; } = "";
        private static List<EditorObject> objects = new List<EditorObject>();
        private static List<Texture2D> textures = new List<Texture2D>();
        private static float gridAlign;
        private static int startingGun;
        private static Vector3 startPosition;
        private static float startOrientation;

        private static bool dd_file = false;
        private static bool dd_level = false;

        private static bool dg_enabled = false;
        private static string dg_caption = "";
        private static string dg_input = "";
        private static Action dg_action = null;
        public static bool dg_screenshot = false;
        public static void Dialog(string caption, Action<string> action, string input = "")
        {
            dg_caption = caption;
            dg_input = input;
            dg_action = () => action(dg_input);
            dg_enabled = true;
        }

        private static bool tex_browser_enabled = false;
        private static Vector2 tex_browser_scroll = new Vector2(0, 0);
        private static int recent_select = 0;

        private static int selObj = -1;
        private static float moveStep = 0.05f;
        private static Vector2 object_browser_scroll = new Vector2(0, 0);

        public static void _ongui()
        {
            if (!editorMode) return;
            if (dg_screenshot)
            {
                GUI.Box(new Rect(Screen.width / 2 - 200, 10, 400, 25), "Point your camera to set the Thumbnail and press <b>ENTER</b>");
                return;
            }
            if (dg_enabled) wir[(int)WindowId.Prompt] = GUI.Window(wid[(int)WindowId.Prompt], wir[(int)WindowId.Prompt], (windowId) => {
                GUI.SetNextControlName("dg_text_input");
                dg_input = GUI.TextArea(new Rect(5, 20, 190, 20), dg_input);
                if (GUI.Button(new Rect(5, 45, 95, 20), "OK") || dg_input.Contains('\n'))
                {
                    dg_input = dg_input.Replace("\n", "");
                    dg_enabled = false;
                    dg_action();
                }
                if (GUI.Button(new Rect(100, 45, 95, 20), "Cancel")) dg_enabled = false;
                GUI.DragWindow();
                GUI.BringWindowToFront(windowId);
                GUI.FocusControl("dg_text_input");
            }, dg_caption);

            if (levelName == "")
            {
                wir[(int)WindowId.Startup] = GUI.Window(wid[(int)WindowId.Startup], wir[(int)WindowId.Startup], (_) =>
                {
                    if (GUI.Button(new Rect(12f, 29f, 184f, 40f), "Create a new map"))
                        Dialog("Enter map name: ", (name) => {
                            levelName = name;
                            objects.Clear();
                            objects.Add(new EditorObject(new Vector3(0f, 2f, 0f), 0f));
                            textures.Clear();
                            dd_level = true;
                            globalPhysics = true; ToggleGlobalPhysics();
                            targetGroup = "";
                            selObj = -1;
                        });
                    if (GUI.Button(new Rect(208f, 29f, 184f, 40f), "Load existing map"))
                    {
                        void recurence(string file)
                        {
                            if(!File.Exists(Path.Combine(Main.directory, "Levels", file + ".kme")))
                                Dialog("Enter map name:", recurence, "File doesn't exist");
                            else
                                LoadLevel(Path.Combine(Main.directory, "Levels", file + ".kme"));
                        }

                        Dialog("Enter map name:", recurence);
                    }
                        
                    if (GUI.Button(new Rect(404f, 29f, 184f, 40f), "Exit editor"))
                        ExitEditor();
                    GUI.Label(new Rect(5f, 75f, 500f, 20f), "Recent Maps:");
                    List<string> oldList = Main.prefs["edit_recent"].Split(';').ToList();
                    while (oldList.Count < 5) oldList.Insert(0, "");
                    recent_select = GUI.SelectionGrid(new Rect(5f, 95f, 590f, 175f), recent_select, new string[] { oldList[4], oldList[3], oldList[2], oldList[1], oldList[0] }, 1);
                    if(GUI.Button(new Rect(5f, 275f, 590f, 20f), "Load Map"))
                    {
                        if (oldList[4 - recent_select] != "" && File.Exists(Path.Combine(Main.directory, "Levels", oldList[4 - recent_select])))
                            LoadLevel(Path.Combine(Main.directory, "Levels", oldList[4 - recent_select]));
                    }
                    GUI.DragWindow();
                }, "Welcome to the Karlson Map Editor!");
                return;
            }

            GUI.Box(new Rect(-5, 0, Screen.width + 10, 20), "");
            if (GUI.Button(new Rect(0, 0, 100, 20), "File")) { dd_file = !dd_file; dd_level = false; }
            if (GUI.Button(new Rect(100, 0, 100, 20), "Map")) { dd_level = !dd_level; dd_file = false; }
            if (GUI.Button(new Rect(205, 0, 100, 20), "Tex Browser")) tex_browser_enabled = !tex_browser_enabled;
            GUI.Label(new Rect(310, 0, 1000, 20), $"<b>Karlson Map Editor</b> | Current map: <b>{levelName}</b> | Object count: <b>{objects.Count}</b> | Hold <b>right click</b> down to move and look around | Select an object by <b>middle clicking</b> it");

            if (dd_file)
            {
                GUI.Box(new Rect(0, 20, 150, 80), "");
                if (GUI.Button(new Rect(0, 20, 150, 20), "Save Map")) { SaveLevel(); dd_file = false; }
                if (GUI.Button(new Rect(0, 40, 150, 20), "Close Map")) { StartEdit(); dd_file = false; }
                if (GUI.Button(new Rect(0, 60, 150, 20), "Upload to Workshop")) dg_screenshot = true;
                if (GUI.Button(new Rect(0, 80, 150, 20), "Exit Editor")) { ExitEditor(); dd_file = false; }
            }

            if(dd_level)
            {
                GUI.Box(new Rect(100, 20, 450, 20), "");
                spawnPrefabDD.Draw(new Rect(100, 20, 150, 20));
                if(spawnPrefabDD.Index != 0)
                {
                    objects.Add(new EditorObject(spawnPrefabDD.Index - 1, PlayerMovement.Instance.gameObject.transform.position));
                    selObj = objects.Count - 1;
                    Coroutines.StartCoroutine(identifyObject(objects[selObj].go));
                    spawnPrefabDD.Index = 0;
                }
                if (GUI.Button(new Rect(250, 20, 150, 20), "Spawn Cube"))
                {
                    objects.Add(new EditorObject(PlayerMovement.Instance.gameObject.transform.position));
                    dd_level = false;
                    selObj = objects.Count - 1;
                    Coroutines.StartCoroutine(identifyObject(objects[selObj].go));
                    picker.color = Color.white;
                }
                if (GUI.Button(new Rect(400, 20, 150, 20), globalPhysics ? "Disable phyisics" : "Enable physics"))
                {
                    ToggleGlobalPhysics();
                    dd_level = false;
                }
            }

            if (tex_browser_enabled) wir[(int)WindowId.TexBrowser] = GUI.Window(wid[(int)WindowId.TexBrowser], wir[(int)WindowId.TexBrowser], (windowId) => {
                if (GUI.Button(new Rect(750, 0, 50, 20), "Close")) tex_browser_enabled = false;
                if (GUI.Button(new Rect(650, 0, 100, 20), "Load Texture"))
                {
                    string picked = FilePicker.PickFile("Select the texture you wish to import", "Images\0*.png;*.jpg;*.jpeg;*.bmp\0All Files\0*.*\0\0");
                    if(picked != "null")
                    {
                        Texture2D tex = new Texture2D(1, 1);
                        tex.LoadImage(File.ReadAllBytes(picked));
                        tex.name = Path.GetFileName(picked);
                        textures.Add(tex);
                    }
                }
                tex_browser_scroll = GUI.BeginScrollView(new Rect(0, 20, 900, 800), tex_browser_scroll, new Rect(0, 0, 800, 10000));
                int i = 0;
                foreach (var t in Main.gameTex.Concat(textures))
                {
                    GUI.DrawTexture(new Rect(200 * (i % 4), 200 * (i / 4), 200, 200), t);
                    GUI.Box(new Rect(200 * (i % 4), 180 + 200 * (i / 4), 200, 20), "");
                    GUI.Label(new Rect(5 + 200 * (i % 4), 180 + 200 * (i / 4), 200, 20), "<size=9>" + t.name + "</size>");
                    if(selObj != -1)
                    {
                        if (!objects[selObj].data.IsPrefab && !objects[selObj].internalObject)
                        {
                            string color = "^";
                            if (objects[selObj].data.TextureId == i) color = "<color=green>^</color>";
                            if (GUI.Button(new Rect(180 + 200 * (i % 4), 180 + 200 * (i / 4), 20, 20), color))
                            {
                                objects[selObj].data.TextureId = i;
                                objects[selObj].go.GetComponent<MeshRenderer>().material.mainTexture = t;
                            }
                        }
                    }
                    if(i >= Main.gameTex.Length)
                    {
                        int usecount = 0;
                        foreach(var o in objects)
                            if (o.data.TextureId == i) usecount++;
                        if(usecount == 0)
                        {
                            if(GUI.Button(new Rect(200 * (i % 4) + 130, 200 * (i / 4) + 160, 70, 20), "Remove"))
                            {
                                textures.RemoveAt(i - Main.gameTex.Length);
                                foreach (var o in objects)
                                    if (o.data.TextureId > i) o.data.TextureId--;
                            }
                        }
                    }
                    i++;
                }
                GUI.EndScrollView();
                GUI.DragWindow(new Rect(0, 0, 650, 20));
            }, "Texture Browser");

            wir[(int)WindowId.LevelBrowser] = GUI.Window(wid[(int)WindowId.LevelBrowser], wir[(int)WindowId.LevelBrowser], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 1000, 20));
                object_browser_scroll = GUI.BeginScrollView(new Rect(0, 20, 300, 480), object_browser_scroll, new Rect(0, 0, 280, objects.Count * 25));
                int i = 0;
                foreach (var obj in objects)
                {
                    GUI.BeginGroup(new Rect(5, 25 * i, 270, 20));
                    if (GUI.Button(new Rect(0, 0, 20, 20), "S"))
                    {
                        selObj = i;
                        if (!objects[selObj].data.IsPrefab)
                        {
                            picker.color = objects[selObj].go.GetComponent<MeshRenderer>().material.color;
                        }
                        else
                        {
                            if (objects[selObj].data.PrefabId == 11)
                                enemyGun.Index = objects[selObj].data.PrefabData;
                        }
                        Coroutines.StartCoroutine(identifyObject(obj.go));
                    }
                    if (GUI.Button(new Rect(25, 0, 20, 20), "^")) { PlayerMovement.Instance.gameObject.transform.position = obj.go.transform.position + Camera.main.transform.forward * -5f; Coroutines.StartCoroutine(identifyObject(obj.go)); }
                    string color = "<color=white>";
                    if (selObj == i) color = "<color=green>";
                    if (targetGroup != "" && obj.data.GroupName == targetGroup) color = "<color=yellow>";
                    if (selObj == i && targetGroup != "" && obj.data.GroupName == targetGroup) color = "<color=cyan>";
                    GUI.Label(new Rect(50, 0, 200, 20), color + (obj.data.IsPrefab ? LevelPlayer.LevelData.PrefabToName(obj.data.PrefabId) : "Cube") + " | " + obj.go.name + "</color>");
                    GUI.EndGroup();
                    i++;
                }
                GUI.EndScrollView();
            }, "Map Object Browser");

            wir[(int)WindowId.ObjectManip] = GUI.Window(wid[(int)WindowId.ObjectManip], wir[(int)WindowId.ObjectManip], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 1000, 20));
                if (selObj == -1)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "No object selected");
                    return;
                }
                EditorObject obj = objects[selObj];
                GUI.Label(new Rect(5, 20, 290, 20), "Selected object: " + obj.go.name + ", prefab: " + (obj.data.IsPrefab ? LevelPlayer.LevelData.PrefabToName(obj.data.PrefabId) : "Cube"));
                if(obj.internalObject)
                    GUI.Label(new Rect(5, 40, 290, 60), "This is an internal object.\nProperties are limited.");
                if (!obj.internalObject)
                {
                    if (GUI.Button(new Rect(5, 40, 75, 20), "Duplicate"))
                    {
                        if (obj.data.IsPrefab)
                            objects.Add(new EditorObject(obj.data.PrefabId, obj.aPosition));
                        else
                        {
                            objects.Add(new EditorObject(obj.aPosition));
                            objects.Last().data.TextureId = obj.data.TextureId;
                            if (obj.data.TextureId < Main.gameTex.Length)
                                obj.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.data.TextureId];
                            else
                                obj.go.GetComponent<MeshRenderer>().material.mainTexture = textures[obj.data.TextureId - Main.gameTex.Length];
                            objects.Last().go.GetComponent<MeshRenderer>().material.color = obj.go.GetComponent<MeshRenderer>().material.color;

                            objects.Last().data.Bounce = obj.data.Bounce;
                            objects.Last().data.Glass = obj.data.Glass;
                            objects.Last().data.Lava = obj.data.Lava;
                            objects.Last().data.DisableTrigger = obj.data.DisableTrigger;
                        }
                        objects.Last().aRotation = obj.aRotation;
                        objects.Last().aScale = obj.aScale;
                        objects.Last().go.name = obj.go.name + " (Clone)";
                        objects.Last().data.GroupName = obj.data.GroupName;
                        selObj = objects.Count - 1;
                        return;
                    }
                    if (GUI.Button(new Rect(85, 40, 75, 20), "Delete"))
                    {
                        UnityEngine.Object.Destroy(obj.go);
                        objects.Remove(obj);
                        selObj = -1;
                        return;
                    }
                }
                if (GUI.Button(new Rect(165, 40, 75, 20), "Identify")) Coroutines.StartCoroutine(identifyObject(obj.go));
                if (GUI.Button(new Rect(245, 40, 50, 20), "Find")) { PlayerMovement.Instance.gameObject.transform.position = obj.aPosition + Camera.main.transform.forward * -5f; Coroutines.StartCoroutine(identifyObject(obj.go)); }

                if (obj.data.PrefabId == 11)
                {
                    GUI.Label(new Rect(5, 60, 40, 20), "Gun");
                    enemyGun.Draw(new Rect(35, 60, 100, 20));
                    obj.data.PrefabData = enemyGun.Index;
                }

                GUI.BeginGroup(new Rect(0, 100, 300, 80));
                GUI.Label(new Rect(5, 0, 290, 20), "[Pos] X: " + obj.aPosition.x + " Y: " + obj.aPosition.y + " Z: " + obj.aPosition.z);
                GUI.Label(new Rect(5, 20, 20, 20), "X:");
                if (GUI.Button(new Rect(20, 20, 20, 20), "-")) obj.aPosition += new Vector3(-moveStep, 0, 0);
                if (GUI.RepeatButton(new Rect(40, 20, 225, 20), "<----------------|---------------->"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                    obj.aPosition += new Vector3(x / 450, 0, 0);
                }
                if (GUI.Button(new Rect(265, 20, 20, 20), "+")) obj.aPosition += new Vector3(moveStep, 0, 0);
                GUI.Label(new Rect(5, 40, 20, 20), "Y:");
                if (GUI.Button(new Rect(20, 40, 20, 20), "-")) obj.aPosition += new Vector3(0, -moveStep, 0);
                if (GUI.RepeatButton(new Rect(40, 40, 225, 20), "<----------------|---------------->"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                    obj.aPosition += new Vector3(0, x / 450, 0);
                }
                if (GUI.Button(new Rect(265, 40, 20, 20), "+")) obj.aPosition += new Vector3(0, moveStep, 0);
                GUI.Label(new Rect(5, 60, 20, 20), "Z:");
                if (GUI.Button(new Rect(20, 60, 20, 20), "-")) obj.aPosition += new Vector3(0, 0, -moveStep);
                if (GUI.RepeatButton(new Rect(40, 60, 225, 20), "<----------------|---------------->"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                    obj.aPosition += new Vector3(0, 0, x / 450);
                }
                if (GUI.Button(new Rect(265, 60, 20, 20), "+")) obj.aPosition += new Vector3(0, 0, moveStep);
                GUI.EndGroup();
                if (!obj.internalObject)
                {
                    GUI.BeginGroup(new Rect(0, 190, 300, 80));
                    GUI.Label(new Rect(5, 0, 290, 20), "[Rot] X: " + obj.aRotation.x + " Y: " + obj.aRotation.y + " Z: " + obj.aRotation.z);
                    GUI.Label(new Rect(5, 20, 20, 20), "X:");
                    if (GUI.Button(new Rect(20, 20, 20, 20), "-")) obj.aRotation += new Vector3(-moveStep, 0f, 0f);
                    if (GUI.RepeatButton(new Rect(40, 20, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        obj.aRotation += new Vector3(x / 450, 0f, 0f);
                    }
                    if (GUI.Button(new Rect(265, 20, 20, 20), "+")) obj.aRotation += new Vector3(moveStep, 0f, 0f);
                    GUI.Label(new Rect(5, 40, 20, 20), "Y:");
                    if (GUI.Button(new Rect(20, 40, 20, 20), "-")) obj.aRotation += new Vector3(0f, -moveStep, 0f);
                    if (GUI.RepeatButton(new Rect(40, 40, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        obj.aRotation += new Vector3(0f, x / 450, 0f);
                    }
                    if (GUI.Button(new Rect(265, 40, 20, 20), "+")) obj.aRotation += new Vector3(0f, moveStep, 0f);
                    GUI.Label(new Rect(5, 60, 20, 20), "Z:");
                    if (GUI.Button(new Rect(20, 60, 20, 20), "-")) obj.aRotation += new Vector3(0f, 0f, -moveStep);
                    if (GUI.RepeatButton(new Rect(40, 60, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        obj.aRotation += new Vector3(0f, 0f, x / 450);
                    }
                    if (GUI.Button(new Rect(265, 60, 20, 20), "+")) obj.aRotation += new Vector3(0f, 0f, moveStep);
                    GUI.EndGroup();

                    GUI.BeginGroup(new Rect(0, 280, 300, 80));
                    GUI.Label(new Rect(5, 0, 290, 20), "[Scale] X: " + obj.aScale.x + " Y: " + obj.aScale.y + " Z: " + obj.aScale.z);
                    GUI.Label(new Rect(5, 20, 20, 20), "X:");
                    if (GUI.Button(new Rect(20, 20, 20, 20), "-")) obj.aScale += new Vector3(-moveStep, 0f, 0f);
                    if (GUI.RepeatButton(new Rect(40, 20, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        obj.aScale += new Vector3(x / 450, 0, 0);
                    }
                    if (GUI.Button(new Rect(265, 20, 20, 20), "+")) obj.aScale += new Vector3(moveStep, 0f, 0f);
                    GUI.Label(new Rect(5, 40, 20, 20), "Y:");
                    if (GUI.Button(new Rect(20, 40, 20, 20), "-")) obj.aScale += new Vector3(0f, -moveStep, 0f);
                    if (GUI.RepeatButton(new Rect(40, 40, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        obj.aScale += new Vector3(0, x / 450, 0);
                    }
                    if (GUI.Button(new Rect(265, 40, 20, 20), "+")) obj.aScale += new Vector3(0f, moveStep, 0f);
                    GUI.Label(new Rect(5, 60, 20, 20), "Z:");
                    if (GUI.Button(new Rect(20, 60, 20, 20), "-")) obj.aScale += new Vector3(0f, 0f, -moveStep);
                    if (GUI.RepeatButton(new Rect(40, 60, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        obj.aScale += new Vector3(0, 0, x / 450);
                    }
                    if (GUI.Button(new Rect(265, 60, 20, 20), "+")) obj.aScale += new Vector3(0f, 0f, moveStep);
                    GUI.EndGroup();
                }

                GUI.BeginGroup(new Rect(5, 370, 300, 80));
                GUI.Label(new Rect(0, 0, 300, 20), "Numerical values:");
                GUI.Label(new Rect(0, 20, 50, 20), "Pos:");
                {
                    float x = obj.aPosition.x, y = obj.aPosition.y, z = obj.aPosition.z;
                    x = float.Parse(GUI.TextField(new Rect(50, 20, 70, 20), x.ToString("0.00")));
                    y = float.Parse(GUI.TextField(new Rect(125, 20, 70, 20), y.ToString("0.00")));
                    z = float.Parse(GUI.TextField(new Rect(200, 20, 70, 20), z.ToString("0.00")));
                    obj.aPosition = new Vector3(x, y, z);
                }
                if(!obj.internalObject)
                {
                    GUI.Label(new Rect(0, 40, 50, 20), "Rot:");
                    {
                        float x = obj.aRotation.x, y = obj.aRotation.y, z = obj.aRotation.z;
                        x = float.Parse(GUI.TextField(new Rect(50, 40, 70, 20), x.ToString("0.00")));
                        y = float.Parse(GUI.TextField(new Rect(125, 40, 70, 20), y.ToString("0.00")));
                        z = float.Parse(GUI.TextField(new Rect(200, 40, 70, 20), z.ToString("0.00")));
                        obj.aRotation = new Vector3(x, y, z);
                    }

                    GUI.Label(new Rect(0, 60, 50, 20), "Scale:");
                    {
                        float x = obj.aScale.x, y = obj.aScale.y, z = obj.aScale.z;
                        x = float.Parse(GUI.TextField(new Rect(50, 60, 70, 20), x.ToString("0.00")));
                        y = float.Parse(GUI.TextField(new Rect(125, 60, 70, 20), y.ToString("0.00")));
                        z = float.Parse(GUI.TextField(new Rect(200, 60, 70, 20), z.ToString("0.00")));
                        obj.aScale = new Vector3(x, y, z);
                    }
                }
                else
                {
                    GUI.Label(new Rect(0, 40, 50, 20), "Rot:");
                    {
                        float y = obj.aRotation.y;
                        y = float.Parse(GUI.TextField(new Rect(125, 40, 70, 20), y.ToString("0.00")));
                        obj.aRotation = new Vector3(0, y, 0);
                    }
                }
                
                GUI.EndGroup();

                GUI.Label(new Rect(5, 470, 100, 20), "Editor Step: ");
                moveStep = float.Parse(GUI.TextField(new Rect(80, 470, 70, 20), moveStep.ToString("0.00")));
                if (GUI.Button(new Rect(155, 470, 50, 20), "Reset")) moveStep = 0.05f;
                if (obj.data.IsPrefab && obj.go.GetComponent<Rigidbody>() != null)
                {
                    GUI.Label(new Rect(5, 490, 50, 20), "Physics");
                    if (GUIex.Toggle(new Rect(55, 490, 70, 20), ref obj.physicsToggle, "enabled", "disabled"))
                        obj.go.GetComponent<Rigidbody>().isKinematic = !obj.go.GetComponent<Rigidbody>().isKinematic;
                }
                if (!obj.data.IsPrefab && !obj.internalObject)
                {
                    if (GUI.Button(new Rect(5, 490, 200, 20), "Update texture scailing"))
                    {
                        string name = obj.go.name;
                        Color c = obj.go.GetComponent<MeshRenderer>().material.color;
                        UnityEngine.Object.Destroy(obj.go);
                        if(obj.data.Glass || obj.data.Lava) 
                            obj.go = LoadsonAPI.PrefabManager.NewGlass();
                        else
                            obj.go = LoadsonAPI.PrefabManager.NewCube();
                        if (obj.data.TextureId < Main.gameTex.Length)
                            obj.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.data.TextureId];
                        else
                            obj.go.GetComponent<MeshRenderer>().material.mainTexture = textures[obj.data.TextureId - Main.gameTex.Length];
                        obj.go.GetComponent<MeshRenderer>().material.color = c;
                        obj.go.transform.position = obj.data.Position;
                        obj.go.transform.rotation = Quaternion.Euler(obj.data.Rotation);
                        obj.go.transform.localScale = obj.data.Scale;
                        obj.go.name = name;
                    }
                    obj.data.Bounce = GUI.Toggle(new Rect(5, 60, 75, 20), obj.data.Bounce, "Bounce");
                    obj.data.Glass = GUI.Toggle(new Rect(85, 60, 75, 20), obj.data.Glass, "Glass");
                    obj.data.MarkAsObject = GUI.Toggle(new Rect(5, 80, 120, 20), obj.data.MarkAsObject, "Mark as Object");
                    if(obj.data.Glass)
                        obj.data.DisableTrigger = GUI.Toggle(new Rect(165, 60, 120, 20), obj.data.DisableTrigger, "Disable Trigger");
                    else
                        obj.data.Lava = GUI.Toggle(new Rect(165, 60, 75, 20), obj.data.Lava, "Lava");
                    if((obj.data.Glass || obj.data.Lava) && obj.go.GetComponent<Glass>() == null)
                    {
                        string name = obj.go.name;
                        Color c = obj.go.GetComponent<MeshRenderer>().material.color;
                        UnityEngine.Object.Destroy(obj.go);
                        obj.go = LoadsonAPI.PrefabManager.NewGlass();
                        if (obj.data.TextureId < Main.gameTex.Length)
                            obj.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.data.TextureId];
                        else
                            obj.go.GetComponent<MeshRenderer>().material.mainTexture = textures[obj.data.TextureId - Main.gameTex.Length];
                        obj.go.GetComponent<MeshRenderer>().material.color = c;
                        obj.go.transform.position = obj.data.Position;
                        obj.go.transform.rotation = Quaternion.Euler(obj.data.Rotation);
                        obj.go.transform.localScale = obj.data.Scale;
                        obj.go.name = name;
                    }
                    if(!obj.data.Glass && !obj.data.Lava && obj.go.GetComponent<Glass>() != null)
                    {
                        string name = obj.go.name;
                        Color c = obj.go.GetComponent<MeshRenderer>().material.color;
                        UnityEngine.Object.Destroy(obj.go);
                        obj.go = LoadsonAPI.PrefabManager.NewCube();
                        if (obj.data.TextureId < Main.gameTex.Length)
                            obj.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[obj.data.TextureId];
                        else
                            obj.go.GetComponent<MeshRenderer>().material.mainTexture = textures[obj.data.TextureId - Main.gameTex.Length];
                        obj.go.GetComponent<MeshRenderer>().material.color = c;
                        obj.go.transform.position = obj.data.Position;
                        obj.go.transform.rotation = Quaternion.Euler(obj.data.Rotation);
                        obj.go.transform.localScale = obj.data.Scale;
                        obj.go.name = name;
                    }
                    // TODO: add hint labels to every option
                }
                else
                {
                    if (obj.data.PrefabId == 11) // only draw, so it appears on top
                        enemyGun.Draw(new Rect(35, 60, 100, 20));
                    // for some reason, the first button rendered takes priority over the mouse click
                    // even if it is below .. idk
                }
                GUI.DragWindow(new Rect(0, 0, 300, 20));
            }, "Object Properties");

            wir[(int)WindowId.ObjectGroup] = GUI.Window(wid[(int)WindowId.ObjectGroup], wir[(int)WindowId.ObjectGroup], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 200, 20));
                if (selObj == -1)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "No object selected");
                    return;
                }
                if (objects[selObj].internalObject)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "This is an internal object");
                    return;
                }
                GUI.Label(new Rect(5, 20, 50, 20), "Name");
                objects[selObj].go.name = GUI.TextField(new Rect(45, 20, 150, 20), objects[selObj].go.name);
                GUI.Label(new Rect(5, 40, 50, 20), "Group");
                objects[selObj].data.GroupName = GUI.TextField(new Rect(45, 40, 150, 20), objects[selObj].data.GroupName);
            }, "Object Settings");
            wir[(int)WindowId.ObjectGroupManip] = GUI.Window(wid[(int)WindowId.ObjectGroupManip], wir[(int)WindowId.ObjectGroupManip], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 200, 20));
                int count = 0;
                foreach (var obj in objects)
                    if (obj.data.GroupName == targetGroup)
                        count++;
                if (targetGroup == "") GUI.Label(new Rect(5, 20, 100, 20), "Group name:");
                else GUI.Label(new Rect(5, 20, 200, 20), "Group name: <color=yellow>(" + count + " objects)</color>");
                targetGroup = GUI.TextField(new Rect(5, 40, 190, 20), targetGroup);
                if (targetGroup == "")
                {
                    GUI.Label(new Rect(5, 60, 200, 20), "Please enter a group name");
                    return;
                }
                if (count == 0)
                {
                    GUI.Label(new Rect(5, 60, 200, 20), "No objects have the targeted group");
                    return;
                }
                GUI.BeginGroup(new Rect(0, 60, 200, 60));
                GUI.Label(new Rect(5, 0, 20, 20), "X:");
                Vector3 delta = Vector3.zero;
                if (GUI.Button(new Rect(20, 0, 20, 20), "-")) delta = new Vector3(-moveStep, 0, 0);
                if (GUI.RepeatButton(new Rect(40, 0, 135, 20), "<------------|------------>"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectGroupManip].x - 107;
                    delta = new Vector3(x / 270, 0, 0);
                }
                if (GUI.Button(new Rect(175, 0, 20, 20), "+")) delta = new Vector3(moveStep, 0, 0);

                GUI.Label(new Rect(5, 20, 20, 20), "Y:");
                if (GUI.Button(new Rect(20, 20, 20, 20), "-")) delta = new Vector3(0, -moveStep, 0);
                if (GUI.RepeatButton(new Rect(40, 20, 135, 20), "<------------|------------>"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectGroupManip].x - 107;
                    delta = new Vector3(0, x / 270, 0);
                }
                if (GUI.Button(new Rect(175, 20, 20, 20), "+")) delta = new Vector3(0, moveStep, 0);

                GUI.Label(new Rect(5, 40, 20, 20), "Z:");
                if (GUI.Button(new Rect(20, 40, 20, 20), "-")) delta = new Vector3(0, 0, -moveStep);
                if (GUI.RepeatButton(new Rect(40, 40, 135, 20), "<------------|------------>"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectGroupManip].x - 107;
                    delta = new Vector3(0, 0, x / 270);
                }
                if (GUI.Button(new Rect(175, 40, 20, 20), "+")) delta = new Vector3(0, 0, moveStep);
                
                foreach(var obj in objects)
                    if (obj.data.GroupName == targetGroup)
                        obj.aPosition += delta;

                GUI.EndGroup();
            }, "Group Manipulation");


            wir[(int)WindowId.LevelData] = GUI.Window(wid[(int)WindowId.LevelData], wir[(int)WindowId.LevelData], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 200, 20));
                GUI.Label(new Rect(5, 20, 100, 20), "Grid Align");
                GUI.TextField(new Rect(95, 20, 50, 20), "TBA");

                GUI.Label(new Rect(5, 40, 100, 20), "Starting Gun");
                startGunDD.Draw(new Rect(95, 40, 100, 20));
                startingGun = startGunDD.Index;

                if(GUI.Button(new Rect(5, 65, 190, 20), "Set Spawn"))
                {
                    startPosition = PlayerMovement.Instance.gameObject.transform.position;
                    objects[0].aPosition = startPosition;
                }
                if (GUI.Button(new Rect(5, 90, 190, 20), "Set Spawn Orientation"))
                {
                    startOrientation = Camera.main.transform.rotation.eulerAngles.y;
                    objects[0].aRotation = new Vector3(0, startOrientation, 0);
                }

                startGunDD.Draw(new Rect(95, 40, 100, 20));

            }, "Map Settings");

            if (selObj != -1 && !objects[selObj].data.IsPrefab && !objects[selObj].internalObject)
            {
                //picker.color = objects[selObj].go.GetComponent<MeshRenderer>().material.color;
                Color oldc = picker.color;
                picker.DrawWindow();
                objects[selObj].go.GetComponent<MeshRenderer>().material.color = picker.color;
            }

            GUI.DrawTexture(new Rect(5, Screen.height - 125, 100, 100), gizmoRender);

            GUI.Box(new Rect(110, Screen.height - 75, 340, 50), "");
            GUI.Label(new Rect(115, Screen.height - 70, 35, 20), "[Pos]");
            GUI.Label(new Rect(150, Screen.height - 70, 100, 20), "X: " + PlayerMovement.Instance.gameObject.transform.position.x);
            GUI.Label(new Rect(240, Screen.height - 70, 100, 20), "Y: " + PlayerMovement.Instance.gameObject.transform.position.y);
            GUI.Label(new Rect(330, Screen.height - 70, 100, 20), "Z: " + PlayerMovement.Instance.gameObject.transform.position.z);
            GUI.Label(new Rect(115, Screen.height - 50, 35, 20), "[Rot]");
            GUI.Label(new Rect(150, Screen.height - 50, 100, 20), "X: " + Camera.main.transform.rotation.eulerAngles.x);
            GUI.Label(new Rect(240, Screen.height - 50, 100, 20), "Y: " + Camera.main.transform.rotation.eulerAngles.y);
        }

        private static Texture2D gizmoRender = new Texture2D(100, 100);
        public static void _onupdate()
        {
            if (!editorMode || Camera.main == null) return;
            
            if(dg_screenshot && Input.GetKey(KeyCode.Return))
            {
                dg_screenshot = false;
                var ss = MakeScreenshot();
                Dialog("Enter level name:", (name) =>
                {
                    Workshop_API.Core.UploadLevel(new Workshop_API.KWM_Convert.KWM(name, ss, SaveLevelData()));
                });
            }

            if (!Input.GetButton("Fire2") && Input.GetMouseButtonDown(2))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100, ~(1 << 19)))
                {
                    int i = 0;
                    for(; i < objects.Count; i++)
                    {
                        if (objects[i].go == hit.transform.gameObject)
                        {
                            selObj = i;
                            if (!objects[selObj].data.IsPrefab)
                                picker.color = objects[selObj].go.GetComponent<MeshRenderer>().material.color;
                            Coroutines.StartCoroutine(identifyObject(objects[i].go));
                            break;
                        }
                    }
                }
            }

            // update gizmo
            GameObject GObg = GameObject.Find("/Gizmo Backplane");
            if (GObg == null) return;
            GameObject GOcam = new GameObject("Gizmo Camera");
            Camera cam = GOcam.AddComponent<Camera>();
            
            GOcam.transform.position = new Vector3(5000, 5000, 5000);
            GOcam.transform.rotation = Camera.main.transform.rotation;
            GOcam.transform.position -= GOcam.transform.forward * 5;

            GObg.transform.position = GOcam.transform.position + GOcam.transform.forward * 10;
            GObg.transform.LookAt(GOcam.transform);
            GObg.transform.rotation = Quaternion.Euler(GObg.transform.rotation.eulerAngles.x + 90, GObg.transform.rotation.eulerAngles.y, GObg.transform.rotation.eulerAngles.z);

            RenderTexture rt = new RenderTexture(100, 100, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            UnityEngine.Object.Destroy(gizmoRender);
            gizmoRender = new Texture2D(100, 100);
            gizmoRender.ReadPixels(new Rect(0, 0, 100, 100), 0, 0);
            gizmoRender.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(GOcam);
            
            if (selObj != -1)
            {
                clickGizmo[0].transform.position = objects[selObj].go.transform.position;
                clickGizmo[1].transform.position = objects[selObj].go.transform.position + new Vector3(0, 0, 1);
                clickGizmo[2].transform.position = objects[selObj].go.transform.position + new Vector3(0, 1, 0);
                clickGizmo[3].transform.position = objects[selObj].go.transform.position + new Vector3(1, 0, 0);
                // check for hovering gizmo

                Ray ray = gizmoCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                bool onGizmo = false;
                if (Physics.Raycast(ray, out hit, 100, (1 << 19)))
                {
                    onGizmo = true;
                    if (clickGizmo[1] == hit.transform.gameObject)
                        clickGizmo[1].GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 0, 1, 1));
                    else
                        clickGizmo[1].GetComponent<Renderer>().material.SetColor("_Color", new Color(0.3f, 0.3f, 0.7f, 1));

                    if (clickGizmo[2] == hit.transform.gameObject)
                        clickGizmo[2].GetComponent<Renderer>().material.SetColor("_Color", new Color(0, 1, 0, 1));
                    else
                        clickGizmo[2].GetComponent<Renderer>().material.SetColor("_Color", new Color(0.3f, 0.7f, 0.3f, 1));

                    if (clickGizmo[3] == hit.transform.gameObject)
                        clickGizmo[3].GetComponent<Renderer>().material.SetColor("_Color", new Color(1, 0, 0, 1));
                    else
                        clickGizmo[3].GetComponent<Renderer>().material.SetColor("_Color", new Color(0.7f, 0.3f, 0.3f, 1));
                }
                else
                {
                    clickGizmo[1].GetComponent<Renderer>().material.SetColor("_Color", new Color(0.3f, 0.3f, 0.7f, 1));
                    clickGizmo[2].GetComponent<Renderer>().material.SetColor("_Color", new Color(0.3f, 0.7f, 0.3f, 1));
                    clickGizmo[3].GetComponent<Renderer>().material.SetColor("_Color", new Color(0.7f, 0.3f, 0.3f, 1));
                }
                if(Input.GetMouseButton(1))
                {
                    onGizmo = false;
                    gizmoMoveDirection = Vector3.zero;
                }
                if(onGizmo)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        gizmoMovePos = ray.GetPoint(hit.distance);
                        if (clickGizmo[1] == hit.transform.gameObject)
                            gizmoMoveDirection = new Vector3(0, 0, 1);
                        if (clickGizmo[2] == hit.transform.gameObject)
                            gizmoMoveDirection = new Vector3(0, 1, 0);
                        if (clickGizmo[3] == hit.transform.gameObject)
                            gizmoMoveDirection = new Vector3(1, 0, 0);
                    }
                    oldGizmoDistance = hit.distance;
                }
                if (Input.GetMouseButton(0) && gizmoMoveDirection != Vector3.zero)
                {
                    Vector3 delta = ray.GetPoint(oldGizmoDistance) - gizmoMovePos;
                    delta.Scale(gizmoMoveDirection);
                    objects[selObj].aPosition += delta;
                    gizmoMovePos = ray.GetPoint(oldGizmoDistance);
                }
                if (Input.GetMouseButtonUp(0))
                {
                    gizmoMoveDirection = Vector3.zero;
                }
            }
            else
            {
                clickGizmo[0].transform.position = new Vector3(5000, 5000, 5100);
                clickGizmo[1].transform.position = new Vector3(5000, 5000, 5100);
                clickGizmo[2].transform.position = new Vector3(5000, 5000, 5100);
                clickGizmo[3].transform.position = new Vector3(5000, 5000, 5100);
            }

            startPosition = objects[0].aPosition;
            startOrientation = objects[0].aRotation.y;

            // update grid align
            if(gridAlign != 0)
            {
                if (gridAlign < 0) gridAlign = 0;
                foreach (var obj in objects)
                {
                    Vector3 delta = obj.aPosition;
                    while (delta.x >= gridAlign)
                        delta.x -= gridAlign;
                    if (2 * delta.x / gridAlign >= 1)
                        delta.x = gridAlign - delta.x;

                    while (delta.y >= gridAlign)
                        delta.y -= gridAlign;
                    if (2 * delta.y / gridAlign >= 1)
                        delta.y = gridAlign - delta.y;

                    while (delta.z >= gridAlign)
                        delta.z -= gridAlign;
                    if (2 * delta.z / gridAlign >= 1)
                        delta.z = gridAlign - delta.z;

                    obj.aPosition -= delta;
                }
            }
        }

        private static void ExitEditor()
        {
            UnityEngine.Object.Destroy(gizmoCamera);
            dd_file = false;
            dd_level = false;
            tex_browser_enabled = false;
            editorMode = false;
            Game.Instance.MainMenu();
        }

        public static byte[] MakeScreenshot()
        {
            GameObject GOcam = new GameObject("Screenshot Camera");
            Camera cam = GOcam.AddComponent<Camera>();
            cam.fieldOfView = Camera.main.fieldOfView;
            GOcam.transform.position = Camera.main.transform.position;
            GOcam.transform.rotation = Camera.main.transform.rotation;
            RenderTexture rt = new RenderTexture(177, 100, 24);
            cam.targetTexture = rt;
            Texture2D screenShot = new Texture2D(177, 100, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, 177, 100), 0, 0);
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(GOcam);
            return screenShot.EncodeToPNG();
        }
        private static byte[] SaveLevelData()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(2);
                bw.Write(gridAlign);
                bw.Write(startingGun);
                bw.Write(startPosition);
                bw.Write(startOrientation);
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
                    if (obj.internalObject) internalCount++;
                bw.Write(objects.Count - internalCount);
                foreach (var obj in objects)
                {
                    if (obj.internalObject) continue; // don't write internal objects
                    bw.Write(obj.data.IsPrefab);
                    bw.Write(obj.go.name);
                    bw.Write(obj.data.GroupName);
                    if (obj.data.IsPrefab)
                    {
                        bw.Write(obj.data.PrefabId);
                        bw.Write(obj.aPosition);
                        bw.Write(obj.aRotation);
                        bw.Write(obj.aScale);
                        bw.Write(obj.data.PrefabData);
                    }
                    else
                    {
                        bw.Write(obj.aPosition);
                        bw.Write(obj.aRotation);
                        bw.Write(obj.aScale);
                        bw.Write(obj.data.TextureId);
                        bw.Write(obj.go.GetComponent<MeshRenderer>().material.color);
                        bw.Write(obj.data.Bounce);
                        bw.Write(obj.data.Glass);
                        bw.Write(obj.data.Lava);
                        bw.Write(obj.data.DisableTrigger);
                        bw.Write(obj.data.MarkAsObject);
                    }
                }
                bw.Flush();
                return SevenZipHelper.Compress(ms.ToArray());
            }
        }
        private static void SaveLevel()
        {
            File.WriteAllBytes(Path.Combine(Main.directory, "Levels", levelName + ".kme"), SaveLevelData());
            List<string> oldList = Main.prefs["edit_recent"].Split(';').ToList();
            if (oldList.Contains(levelName + ".kme"))
                oldList.Remove(levelName + ".kme");
            else if (oldList.Count >= 5)
                oldList.RemoveAt(0);
            oldList.Add(levelName + ".kme");
            Main.prefs["edit_recent"] = string.Join(";", oldList);
        }

        private static void LoadLevel(string path)
        {
            LevelPlayer.LevelData data = new LevelPlayer.LevelData(File.ReadAllBytes(path));
            levelName = Path.GetFileNameWithoutExtension(path);
            gridAlign = data.gridAlign;
            startingGun = data.startingGun;
            startGunDD.Index = startingGun;
            startPosition = data.startPosition;
            startOrientation = data.startOrientation;
            textures = data.Textures.ToList();
            objects.Clear();
            objects.Add(new EditorObject(data.startPosition, startOrientation));
            foreach(var obj in data.Objects)
                objects.Add(new EditorObject(obj));
            dd_level = true;
            globalPhysics = true; ToggleGlobalPhysics();
            targetGroup = "";
            selObj = -1;
        }

        private static IEnumerator identifyObject(GameObject go)
        {
            go.SetActive(false);
            yield return new WaitForSecondsRealtime(0.2f);
            go.SetActive(true);
            yield return new WaitForSecondsRealtime(0.2f);
            go.SetActive(false);
            yield return new WaitForSecondsRealtime(0.2f);
            go.SetActive(true);
            yield return new WaitForSecondsRealtime(0.2f);
            go.SetActive(false);
            yield return new WaitForSecondsRealtime(0.2f);
            go.SetActive(true);
        }

        private static bool globalPhysics = false;
        private static void ToggleGlobalPhysics()
        {
            globalPhysics = !globalPhysics;
            if (globalPhysics) Time.timeScale = 1.0f;
            else Time.timeScale = 0f;
        }

        public class EditorObject
        {
            public EditorObject(Vector3 position)
            {
                data = new LevelPlayer.LevelData.LevelObject(position, Vector3.zero, Vector3.one, 6, Color.white, "Cube", "", false, false, false, false, false);
                go = LoadsonAPI.PrefabManager.NewCube();
                go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[data.TextureId];
                go.transform.position = data.Position;
                go.transform.rotation = Quaternion.Euler(data.Rotation);
                go.transform.localScale = data.Scale;
            }

            public EditorObject(int _prefabId, Vector3 position)
            {
                data = new LevelPlayer.LevelData.LevelObject(_prefabId, position, Vector3.zero, Vector3.one, LevelPlayer.LevelData.PrefabToName(_prefabId), "", 0);
                go = LevelPlayer.LevelData.MakePrefab(_prefabId);
                go.transform.position = data.Position;
                go.transform.rotation = Quaternion.Euler(data.Rotation);
                data.Scale = go.transform.localScale;
                if(go.GetComponent<Rigidbody>() != null) go.GetComponent<Rigidbody>().isKinematic = true;
            }

            public EditorObject(LevelPlayer.LevelData.LevelObject playObj)
            {
                data = playObj;
                if(data.IsPrefab)
                {
                    go = LevelPlayer.LevelData.MakePrefab(data.PrefabId);
                    if (go.GetComponent<Rigidbody>() != null) go.GetComponent<Rigidbody>().isKinematic = true;
                }
                else
                {
                    if (playObj.Glass || playObj.Lava)
                        go = LoadsonAPI.PrefabManager.NewGlass();
                    else
                        go = LoadsonAPI.PrefabManager.NewCube();
                    if (playObj.TextureId < Main.gameTex.Length)
                        go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[playObj.TextureId];
                    else
                        go.GetComponent<MeshRenderer>().material.mainTexture = textures[playObj.TextureId - Main.gameTex.Length];
                    go.GetComponent<MeshRenderer>().material.color = playObj._Color;
                }
                go.transform.position = data.Position;
                go.transform.rotation = Quaternion.Euler(data.Rotation);
                go.transform.localScale = data.Scale;
                go.name = data.Name;
            }

            public EditorObject(Vector3 pos, float orientation)
            {
                data = new LevelPlayer.LevelData.LevelObject(pos, Vector3.zero, Vector3.one, 6, Color.white, "Player Spawn", "_internal", false, false, false, false, false);
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.localScale = new Vector3(1f, 1.5f, 1f);
                go.transform.position = pos;
                go.transform.eulerAngles = new Vector3(0f, orientation, 0f);
                go.name = "Player Spawn";
                go.GetComponent<MeshRenderer>().material.color = Color.yellow;
                internalObject = true;
            }

            public bool internalObject { get; private set; } = false;
            public LevelPlayer.LevelData.LevelObject data;
            public GameObject go;

            public Vector3 aPosition
            {
                get
                {
                    if (data.Position != go.transform.position)
                        data.Position = go.transform.position;
                    return go.transform.position;
                }
                set { go.transform.position = value; data.Position = value; }
            }
            public Vector3 aRotation
            {
                get
                {
                    if (data.Rotation != go.transform.rotation.eulerAngles)
                        data.Rotation = go.transform.rotation.eulerAngles;
                    return go.transform.rotation.eulerAngles;
                }
                set { go.transform.rotation = Quaternion.Euler(value); data.Rotation = value; }
            }
            public Vector3 aScale
            {
                get
                {
                    if (data.Scale != go.transform.localScale)
                        data.Scale = go.transform.localScale;
                    return go.transform.localScale;
                }
                set { go.transform.localScale = value; data.Scale = value; }
            }

            public bool physicsToggle = false;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
    }
}
