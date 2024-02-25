using Loadson;
using LoadsonAPI;
using SevenZip.Compression.LZMA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace KarlsonMapEditor
{
    public static class LevelEditor
    {
        private delegate bool skipObject(EditorObject obj);

        private static bool _initd = false;
        private static void _init()
        {
            if(_initd) return;
            _initd = true;
            wid = new int[]{ ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId(), ImGUI_WID.GetWindowId() };
            wir = new Rect[6];
            wir[(int)WindowId.Startup] = new Rect((Screen.width - 600) / 2, (Screen.height - 300) / 2, 600, 300);
            wir[(int)WindowId.Prompt] = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 35, 200, 70);
            wir[(int)WindowId.TexBrowser] = new Rect((Screen.width - 800) / 2, (Screen.height - 860) / 2, 800, 860);
            wir[(int)WindowId.LevelBrowser] = new Rect(Screen.width - 305, 30, 300, 500);
            wir[(int)WindowId.ObjectManip] = new Rect(Screen.width - 305, 540, 300, 520);
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
            LevelBrowser,
            ObjectManip,
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
        const float clickGizmoScaleFactor = 0.1f;
        static List<GameObject> clickGizmo;
        static Camera gizmoCamera;
        static Vector3 gizmoMovePos;
        static Vector3 gizmoMoveDirection = Vector3.zero;
        static float oldGizmoDistance;

        static GUIex.Dropdown enemyGun;
        static GUIex.Dropdown startGunDD;
        static GUIex.Dropdown spawnPrefabDD;
        private static void InitEditor(Scene arg0, LoadSceneMode arg1)
        {
            AudioListener.volume = 0;
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
        private static ObjectGroup globalObject;
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

        private static class SelectedObject
        {
            public enum SelectedType
            {
                EditorObject = 0,
                ObjectGroup = 1
            };
            public static SelectedType Type = SelectedType.EditorObject;

            public static ObjectGroup Group { get; private set; }
            public static EditorObject Object { get; private set; }
            public static IBasicProperties Basic => (Type == SelectedType.ObjectGroup ? (IBasicProperties)Group : Object);

            public static bool Selected { get; private set; } = false;

            public static void SelectObject(EditorObject obj)
            {
                Object = obj;
                Type = SelectedType.EditorObject;
                Selected = true;

                if (!obj.data.IsPrefab)
                    picker.color = obj.go.GetComponent<MeshRenderer>().material.color;

                Identify();
            }
            public static void SelectGroup(ObjectGroup group)
            {
                Group = group;
                Type = SelectedType.ObjectGroup;
                Selected = true;
                Identify();
            }
            public static void Identify()
            {
                if(Type == SelectedType.ObjectGroup)
                    Coroutines.StartCoroutine(identifyObject(Group.go));
                else
                    Coroutines.StartCoroutine(identifyObject(Object.go));
            }
            public static void Destroy()
            {
                if (Type == SelectedType.ObjectGroup)
                {
                    UnityEngine.Object.Destroy(Group.go);
                    globalObject.DereferenceGroup(Group);
                }
                else
                {
                    UnityEngine.Object.Destroy(Object.go);
                    globalObject.DereferenceObject(Object);
                }
                SelectedObject.Deselect();
            }
            public static void Deselect()
            {
                Selected = false;
                Object = null;
                Group = null;
                Type = SelectedType.EditorObject;
            }
        }
        private static float moveStep = 0.05f;
        private static bool aligned = false;
        private static Vector2 object_browser_scroll = new Vector2(0, 0);

        public static void _ongui()
        {
            if (!editorMode) return;

            // check for windows out of bound (if the user right clicks them, they glitch
            // out because also right-clicking enables camera move, which locks cursor)

            if (wir[(int)WindowId.Startup].x < 0 || wir[(int)WindowId.Startup].y < 0)
                wir[(int)WindowId.Startup] = new Rect((Screen.width - 600) / 2, (Screen.height - 300) / 2, 600, 300);
            if (wir[(int)WindowId.Prompt].x < 0 || wir[(int)WindowId.Prompt].y < 0)
                wir[(int)WindowId.Prompt] = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 35, 200, 70);
            if (wir[(int)WindowId.TexBrowser].x < 0 || wir[(int)WindowId.TexBrowser].y < 0)
                wir[(int)WindowId.TexBrowser] = new Rect((Screen.width - 800) / 2, (Screen.height - 860) / 2, 800, 860);
            if (wir[(int)WindowId.LevelBrowser].x < 0 || wir[(int)WindowId.LevelBrowser].y < 0)
                wir[(int)WindowId.LevelBrowser] = new Rect(Screen.width - 305, 30, 300, 500);
            if (wir[(int)WindowId.ObjectManip].x < 0 || wir[(int)WindowId.ObjectManip].y < 0)
                wir[(int)WindowId.ObjectManip] = new Rect(Screen.width - 305, 540, 300, 520);
            if (wir[(int)WindowId.LevelData].x < 0 || wir[(int)WindowId.LevelData].y < 0)
                wir[(int)WindowId.LevelData] = new Rect(Screen.width - 510, 30, 200, 185);

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
                    {
                        void recurence(string name)
                        {
                            if (File.Exists(Path.Combine(Main.directory, "Levels", name + ".kme")))
                                Dialog("Enter map name:", recurence, "Map already exists");
                            else
                            {
                                levelName = name;
                                globalObject = new ObjectGroup(name);
                                globalObject.isGlobal = true;
                                globalObject.AddObject(new EditorObject(new Vector3(0f, 2f, 0f), 0f));
                                textures.Clear();
                                dd_level = true;
                                Time.timeScale = 0f;
                                SelectedObject.Deselect();
                            }
                        }

                        Dialog("Enter map name:", recurence);
                    }
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

            int _countAll = globalObject.CountAll();

            GUI.Box(new Rect(-5, 0, Screen.width + 10, 20), "");
            if (GUI.Button(new Rect(0, 0, 100, 20), "File")) { dd_file = !dd_file; dd_level = false; }
            if (GUI.Button(new Rect(100, 0, 100, 20), "Map")) { dd_level = !dd_level; dd_file = false; }
            if (GUI.Button(new Rect(200, 0, 100, 20), "Tex Browser")) tex_browser_enabled = !tex_browser_enabled;
            if (GUI.Button(new Rect(300, 0, 100, 20), "KMP Export")) KMPExporter.Export(levelName, globalObject, textures);
            GUI.Label(new Rect(405, 0, 1000, 20), $"<b>Karlson Map Editor</b> v2.0 | Current map: <b>{levelName}</b> | Object count: <b>{_countAll}</b> | Hold <b>right click</b> down to move and look around | Select an object by <b>middle clicking</b> it");

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
                GUI.Box(new Rect(100, 20, 300, 20), "");
                if(!SelectedObject.Selected || SelectedObject.Type == SelectedObject.SelectedType.EditorObject)
                {
                    GUI.Label(new Rect(105, 20, 300, 20), "Select an Object Group to spawn objects");
                }
                else
                {
                    spawnPrefabDD.Draw(new Rect(100, 20, 150, 20));
                    if (spawnPrefabDD.Index != 0)
                    {
                        SelectedObject.Group.AddObject(new EditorObject(spawnPrefabDD.Index - 1, PlayerMovement.Instance.gameObject.transform.position));
                        SelectedObject.SelectObject(SelectedObject.Group.editorObjects.Last());
                        spawnPrefabDD.Index = 0;
                    }
                    if (GUI.Button(new Rect(250, 20, 150, 20), "Spawn Cube"))
                    {
                        SelectedObject.Group.AddObject(new EditorObject(PlayerMovement.Instance.gameObject.transform.position));
                        dd_level = false;
                        SelectedObject.SelectObject(SelectedObject.Group.editorObjects.Last());
                    }
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
                    if(SelectedObject.Selected && SelectedObject.Type == SelectedObject.SelectedType.EditorObject)
                    {
                        if (!SelectedObject.Object.data.IsPrefab && !SelectedObject.Object.internalObject)
                        {
                            string color = "^";
                            if (SelectedObject.Object.data.TextureId == i) color = "<color=green>^</color>";
                            if (GUI.Button(new Rect(180 + 200 * (i % 4), 180 + 200 * (i / 4), 20, 20), color))
                            {
                                SelectedObject.Object.data.TextureId = i;
                                SelectedObject.Object.go.GetComponent<MeshRenderer>().material.mainTexture = t;
                            }
                        }
                    }
                    if(i >= Main.gameTex.Length)
                    {
                        int usecount = 0;
                        foreach(var o in globalObject.AllEditorObjects())
                            if (o.data.TextureId == i) usecount++;
                        if(usecount == 0)
                        {
                            if(GUI.Button(new Rect(200 * (i % 4) + 130, 200 * (i / 4) + 160, 70, 20), "Remove"))
                            {
                                textures.RemoveAt(i - Main.gameTex.Length);
                                foreach (var o in globalObject.AllEditorObjects())
                                    if (o.data.TextureId > i) o.data.TextureId--;
                            }
                        }
                    }
                    i++;
                }
                GUI.EndScrollView();
                GUI.DragWindow(new Rect(0, 0, 650, 20));
            }, "Texture Browser");

            // TODO: implement
            wir[(int)WindowId.LevelBrowser] = GUI.Window(wid[(int)WindowId.LevelBrowser], wir[(int)WindowId.LevelBrowser], (windowId) => {

                int renderObjectGroup(ObjectGroup group, int j, int depth)
                {
                    // render this group name and controls
                    if (GUI.Button(new Rect(depth * 20, j * 25, 20, 20), "S"))
                        SelectedObject.SelectGroup(group);
                    GUI.Label(new Rect(depth * 20 + 20, j * 25, 200, 20), group.go.name);
                    ++j;
                    int firstj = j;
                    // render child groups
                    foreach (var g in group.objectGroups) {
                        j = renderObjectGroup(g, j, depth + 1);
                    }
                    // render child editor objects
                    foreach (var obj in group.editorObjects)
                    {
                        if (GUI.Button(new Rect(depth * 20 + 20, j * 25, 20, 20), "S"))
                            SelectedObject.SelectObject(obj);
                        GUI.Label(new Rect(depth * 20 + 40, j * 25, 200, 20), (obj.data.IsPrefab ? LevelPlayer.LevelData.PrefabToName(obj.data.PrefabId) : "Cube") + " | " + obj.go.name);
                        ++j;
                    }
                    // close group
                    GUI.Box(new Rect(depth * 20 + 7, firstj * 25 - 5, 5, (j - firstj) * 25 + 5), "");
                    GUI.Box(new Rect(depth * 20 + 7, j * 25, 200, 5), "");
                    ++j;
                    return j;
                }
                
                GUI.DragWindow(new Rect(0, 0, 1000, 20));
                if(SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup)
                    if(GUI.Button(new Rect(280, 20, 20, 20), "+"))
                    {
                        SelectedObject.Group.AddGroup(new ObjectGroup());
                        SelectedObject.SelectGroup(SelectedObject.Group.objectGroups.Last());
                        return;
                    }

                object_browser_scroll = GUI.BeginScrollView(new Rect(0, 20, 300, 480), object_browser_scroll, new Rect(0, 0, 280, _countAll * 25));
                
                renderObjectGroup(globalObject, 0, 0);

                GUI.EndScrollView();
            }, "Map Object Browser");

            wir[(int)WindowId.ObjectManip] = GUI.Window(wid[(int)WindowId.ObjectManip], wir[(int)WindowId.ObjectManip], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 1000, 20));
                if (!SelectedObject.Selected)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "No object selected");
                    return;
                }
                if(SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && SelectedObject.Group.isGlobal)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "You can't modify the global object.");
                    GUI.Label(new Rect(5, 35, 300, 20), "You can only add objects (Prefabs / Cube) and groups (+ in the top right)");
                    return;
                }
                SelectedObject.Basic.aName = GUI.TextField(new Rect(5f, 20f, 290f, 20f), SelectedObject.Basic.aName);

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.internalObject)
                    GUI.Label(new Rect(5, 40, 290, 60), "This is an internal object.\nProperties are limited.");
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup || SelectedObject.Type == SelectedObject.SelectedType.EditorObject && !SelectedObject.Object.internalObject)
                {
                    if (GUI.Button(new Rect(5, 40, 75, 20), "Duplicate"))
                    {
                        if(SelectedObject.Type == SelectedObject.SelectedType.EditorObject)
                        {
                            ObjectGroup parent = globalObject.FindParentOfChild(SelectedObject.Object);
                            SelectedObject.Object.Clone(parent);
                            SelectedObject.SelectObject(parent.editorObjects.Last());
                            return;
                        }
                        else
                        {
                            ObjectGroup parent = globalObject.FindParentOfChild(SelectedObject.Group);
                            SelectedObject.Group.Clone(parent);
                            SelectedObject.SelectGroup(parent.objectGroups.Last());
                            return;
                        }
                    }
                    if (GUI.Button(new Rect(85, 40, 75, 20), "Delete"))
                    {
                        SelectedObject.Destroy();
                        return;
                    }
                }
                if (GUI.Button(new Rect(165, 40, 75, 20), "Identify")) SelectedObject.Identify();
                if (GUI.Button(new Rect(245, 40, 50, 20), "Find")) { PlayerMovement.Instance.gameObject.transform.position = SelectedObject.Basic.worldPos + Camera.main.transform.forward * -5f; SelectedObject.Identify(); }

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.data.PrefabId == 11)
                {
                    GUI.Label(new Rect(5, 60, 40, 20), "Gun");
                    enemyGun.Draw(new Rect(35, 60, 100, 20));
                    SelectedObject.Object.data.PrefabData = enemyGun.Index;
                }

                GUI.BeginGroup(new Rect(0, 100, 300, 80));
                GUI.Label(new Rect(5, 0, 290, 20), "[Pos] X: " + SelectedObject.Basic.aPosition.x + " Y: " + SelectedObject.Basic.aPosition.y + " Z: " + SelectedObject.Basic.aPosition.z);
                GUI.Label(new Rect(5, 20, 20, 20), "X:");
                if (GUI.Button(new Rect(20, 20, 20, 20), "-")) SelectedObject.Basic.aPosition += new Vector3(-moveStep, 0, 0);
                if (GUI.RepeatButton(new Rect(40, 20, 225, 20), "<----------------|---------------->"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                    SelectedObject.Basic.aPosition += new Vector3(x / 450, 0, 0);
                }
                if (GUI.Button(new Rect(265, 20, 20, 20), "+")) SelectedObject.Basic.aPosition += new Vector3(moveStep, 0, 0);
                GUI.Label(new Rect(5, 40, 20, 20), "Y:");
                if (GUI.Button(new Rect(20, 40, 20, 20), "-")) SelectedObject.Basic.aPosition += new Vector3(0, -moveStep, 0);
                if (GUI.RepeatButton(new Rect(40, 40, 225, 20), "<----------------|---------------->"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                    SelectedObject.Basic.aPosition += new Vector3(0, x / 450, 0);
                }
                if (GUI.Button(new Rect(265, 40, 20, 20), "+")) SelectedObject.Basic.aPosition += new Vector3(0, moveStep, 0);
                GUI.Label(new Rect(5, 60, 20, 20), "Z:");
                if (GUI.Button(new Rect(20, 60, 20, 20), "-")) SelectedObject.Basic.aPosition += new Vector3(0, 0, -moveStep);
                if (GUI.RepeatButton(new Rect(40, 60, 225, 20), "<----------------|---------------->"))
                {
                    float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                    SelectedObject.Basic.aPosition += new Vector3(0, 0, x / 450);
                }
                if (GUI.Button(new Rect(265, 60, 20, 20), "+")) SelectedObject.Basic.aPosition += new Vector3(0, 0, moveStep);
                GUI.EndGroup();
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup || !SelectedObject.Object.internalObject)
                {
                    GUI.BeginGroup(new Rect(0, 190, 300, 80));
                    GUI.Label(new Rect(5, 0, 290, 20), "[Rot] X: " + SelectedObject.Basic.aRotation.x + " Y: " + SelectedObject.Basic.aRotation.y + " Z: " + SelectedObject.Basic.aRotation.z);
                    GUI.Label(new Rect(5, 20, 20, 20), "X:");
                    if (GUI.Button(new Rect(20, 20, 20, 20), "-")) SelectedObject.Basic.aRotation += new Vector3(-moveStep, 0f, 0f);
                    if (GUI.RepeatButton(new Rect(40, 20, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        SelectedObject.Basic.aRotation += new Vector3(x / 450, 0f, 0f);
                    }
                    if (GUI.Button(new Rect(265, 20, 20, 20), "+")) SelectedObject.Basic.aRotation += new Vector3(moveStep, 0f, 0f);
                    GUI.Label(new Rect(5, 40, 20, 20), "Y:");
                    if (GUI.Button(new Rect(20, 40, 20, 20), "-")) SelectedObject.Basic.aRotation += new Vector3(0f, -moveStep, 0f);
                    if (GUI.RepeatButton(new Rect(40, 40, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        SelectedObject.Basic.aRotation += new Vector3(0f, x / 450, 0f);
                    }
                    if (GUI.Button(new Rect(265, 40, 20, 20), "+")) SelectedObject.Basic.aRotation += new Vector3(0f, moveStep, 0f);
                    GUI.Label(new Rect(5, 60, 20, 20), "Z:");
                    if (GUI.Button(new Rect(20, 60, 20, 20), "-")) SelectedObject.Basic.aRotation += new Vector3(0f, 0f, -moveStep);
                    if (GUI.RepeatButton(new Rect(40, 60, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        SelectedObject.Basic.aRotation += new Vector3(0f, 0f, x / 450);
                    }
                    if (GUI.Button(new Rect(265, 60, 20, 20), "+")) SelectedObject.Basic.aRotation += new Vector3(0f, 0f, moveStep);
                    GUI.EndGroup();

                    GUI.BeginGroup(new Rect(0, 280, 300, 80));
                    GUI.Label(new Rect(5, 0, 290, 20), "[Scale] X: " + SelectedObject.Basic.aScale.x + " Y: " + SelectedObject.Basic.aScale.y + " Z: " + SelectedObject.Basic.aScale.z);
                    GUI.Label(new Rect(5, 20, 20, 20), "X:");
                    if (GUI.Button(new Rect(20, 20, 20, 20), "-")) SelectedObject.Basic.aScale += new Vector3(-moveStep, 0f, 0f);
                    if (GUI.RepeatButton(new Rect(40, 20, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        SelectedObject.Basic.aScale += new Vector3(x / 450, 0, 0);
                    }
                    if (GUI.Button(new Rect(265, 20, 20, 20), "+")) SelectedObject.Basic.aScale += new Vector3(moveStep, 0f, 0f);
                    GUI.Label(new Rect(5, 40, 20, 20), "Y:");
                    if (GUI.Button(new Rect(20, 40, 20, 20), "-")) SelectedObject.Basic.aScale += new Vector3(0f, -moveStep, 0f);
                    if (GUI.RepeatButton(new Rect(40, 40, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        SelectedObject.Basic.aScale += new Vector3(0, x / 450, 0);
                    }
                    if (GUI.Button(new Rect(265, 40, 20, 20), "+")) SelectedObject.Basic.aScale += new Vector3(0f, moveStep, 0f);
                    GUI.Label(new Rect(5, 60, 20, 20), "Z:");
                    if (GUI.Button(new Rect(20, 60, 20, 20), "-")) SelectedObject.Basic.aScale += new Vector3(0f, 0f, -moveStep);
                    if (GUI.RepeatButton(new Rect(40, 60, 225, 20), "<----------------|---------------->"))
                    {
                        float x = Input.mousePosition.x - wir[(int)WindowId.ObjectManip].x - 152;
                        SelectedObject.Basic.aScale += new Vector3(0, 0, x / 450);
                    }
                    if (GUI.Button(new Rect(265, 60, 20, 20), "+")) SelectedObject.Basic.aScale += new Vector3(0f, 0f, moveStep);
                    GUI.EndGroup();
                }

                GUI.BeginGroup(new Rect(5, 370, 300, 80));
                GUI.Label(new Rect(0, 0, 300, 20), "Numerical values:");
                GUI.Label(new Rect(0, 20, 50, 20), "Pos:");
                {
                    float x = SelectedObject.Basic.aPosition.x, y = SelectedObject.Basic.aPosition.y, z = SelectedObject.Basic.aPosition.z;
                    x = float.Parse(GUI.TextField(new Rect(50, 20, 70, 20), x.ToString("0.00")));
                    y = float.Parse(GUI.TextField(new Rect(125, 20, 70, 20), y.ToString("0.00")));
                    z = float.Parse(GUI.TextField(new Rect(200, 20, 70, 20), z.ToString("0.00")));
                    SelectedObject.Basic.aPosition = new Vector3(x, y, z);
                }
                if(SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup || !SelectedObject.Object.internalObject)
                {
                    GUI.Label(new Rect(0, 40, 50, 20), "Rot:");
                    {
                        float x = SelectedObject.Basic.aRotation.x, y = SelectedObject.Basic.aRotation.y, z = SelectedObject.Basic.aRotation.z;
                        x = float.Parse(GUI.TextField(new Rect(50, 40, 70, 20), x.ToString("0.00")));
                        y = float.Parse(GUI.TextField(new Rect(125, 40, 70, 20), y.ToString("0.00")));
                        z = float.Parse(GUI.TextField(new Rect(200, 40, 70, 20), z.ToString("0.00")));
                        SelectedObject.Basic.aRotation = new Vector3(x, y, z);
                    }

                    GUI.Label(new Rect(0, 60, 50, 20), "Scale:");
                    {
                        float x = SelectedObject.Basic.aScale.x, y = SelectedObject.Basic.aScale.y, z = SelectedObject.Basic.aScale.z;
                        x = float.Parse(GUI.TextField(new Rect(50, 60, 70, 20), x.ToString("0.00")));
                        y = float.Parse(GUI.TextField(new Rect(125, 60, 70, 20), y.ToString("0.00")));
                        z = float.Parse(GUI.TextField(new Rect(200, 60, 70, 20), z.ToString("0.00")));
                        SelectedObject.Basic.aScale = new Vector3(x, y, z);
                    }
                }
                else
                {
                    GUI.Label(new Rect(0, 40, 50, 20), "Rot:");
                    {
                        float y = SelectedObject.Basic.aRotation.y;
                        y = float.Parse(GUI.TextField(new Rect(125, 40, 70, 20), y.ToString("0.00")));
                        SelectedObject.Basic.aRotation = new Vector3(0, y, 0);
                    }
                }
                
                GUI.EndGroup();

                GUI.Label(new Rect(5, 470, 100, 20), "Editor Step: ");
                moveStep = float.Parse(GUI.TextField(new Rect(80, 470, 70, 20), moveStep.ToString("0.00")));
                if (GUI.Button(new Rect(155, 470, 50, 20), "Reset")) moveStep = 0.05f;
                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && !SelectedObject.Object.data.IsPrefab && !SelectedObject.Object.internalObject)
                {
                    if (GUI.Button(new Rect(5, 490, 200, 20), "Update texture scailing"))
                    {
                        SelectedObject.Object.go.GetComponent<TextureScaling>().Calculate();
                    }
                    SelectedObject.Object.data.Bounce = GUI.Toggle(new Rect(5, 60, 75, 20), SelectedObject.Object.data.Bounce, "Bounce");
                    SelectedObject.Object.data.Glass = GUI.Toggle(new Rect(85, 60, 75, 20), SelectedObject.Object.data.Glass, "Glass");
                    SelectedObject.Object.data.MarkAsObject = GUI.Toggle(new Rect(5, 80, 120, 20), SelectedObject.Object.data.MarkAsObject, "Mark as Object");
                    if(SelectedObject.Object.data.Glass)
                        SelectedObject.Object.data.DisableTrigger = GUI.Toggle(new Rect(165, 60, 120, 20), SelectedObject.Object.data.DisableTrigger, "Disable Trigger");
                    else
                        SelectedObject.Object.data.Lava = GUI.Toggle(new Rect(165, 60, 75, 20), SelectedObject.Object.data.Lava, "Lava");
                    if((SelectedObject.Object.data.Glass || SelectedObject.Object.data.Lava) && SelectedObject.Object.go.GetComponent<Glass>() == null)
                    {
                        string name = SelectedObject.Object.go.name;
                        Color c = SelectedObject.Object.go.GetComponent<MeshRenderer>().material.color;
                        UnityEngine.Object.Destroy(SelectedObject.Object.go);
                        SelectedObject.Object.go = LoadsonAPI.PrefabManager.NewGlass();
                        if (SelectedObject.Object.data.TextureId < Main.gameTex.Length)
                            SelectedObject.Object.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[SelectedObject.Object.data.TextureId];
                        else
                            SelectedObject.Object.go.GetComponent<MeshRenderer>().material.mainTexture = textures[SelectedObject.Object.data.TextureId - Main.gameTex.Length];
                        SelectedObject.Object.go.GetComponent<MeshRenderer>().material.color = c;
                        SelectedObject.Object.go.transform.position = SelectedObject.Object.data.Position;
                        SelectedObject.Object.go.transform.rotation = Quaternion.Euler(SelectedObject.Object.data.Rotation);
                        SelectedObject.Object.go.transform.localScale = SelectedObject.Object.data.Scale;
                        SelectedObject.Object.go.name = name;
                    }
                    if(!SelectedObject.Object.data.Glass && !SelectedObject.Object.data.Lava && SelectedObject.Object.go.GetComponent<Glass>() != null)
                    {
                        string name = SelectedObject.Object.go.name;
                        Color c = SelectedObject.Object.go.GetComponent<MeshRenderer>().material.color;
                        UnityEngine.Object.Destroy(SelectedObject.Object.go);
                        SelectedObject.Object.go = LoadsonAPI.PrefabManager.NewCube();
                        if (SelectedObject.Object.data.TextureId < Main.gameTex.Length)
                            SelectedObject.Object.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[SelectedObject.Object.data.TextureId];
                        else
                            SelectedObject.Object.go.GetComponent<MeshRenderer>().material.mainTexture = textures[SelectedObject.Object.data.TextureId - Main.gameTex.Length];
                        SelectedObject.Object.go.GetComponent<MeshRenderer>().material.color = c;
                        SelectedObject.Object.go.transform.position = SelectedObject.Object.data.Position;
                        SelectedObject.Object.go.transform.rotation = Quaternion.Euler(SelectedObject.Object.data.Rotation);
                        SelectedObject.Object.go.transform.localScale = SelectedObject.Object.data.Scale;
                        SelectedObject.Object.go.name = name;
                    }
                    // TODO: add hint labels to every option
                }
                else
                {
                    if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.data.IsPrefab && SelectedObject.Object.data.PrefabId == 11) // only draw, so it appears on top
                        enemyGun.Draw(new Rect(35, 60, 100, 20));
                    // for some reason, the first button rendered takes priority over the mouse click
                    // even if it is below .. idk
                }
                GUI.DragWindow(new Rect(0, 0, 300, 20));
            }, "Object Properties");

            wir[(int)WindowId.LevelData] = GUI.Window(wid[(int)WindowId.LevelData], wir[(int)WindowId.LevelData], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 200, 20));
                GUI.Label(new Rect(5, 20, 100, 20), "Grid Align");
                gridAlign = float.Parse(GUI.TextField(new Rect(95, 20, 50, 20), gridAlign.ToString("0.00")));

                GUI.Label(new Rect(5, 40, 100, 20), "Starting Gun");
                startGunDD.Draw(new Rect(95, 40, 100, 20));
                startingGun = startGunDD.Index;

                EditorObject spawnObject = globalObject.editorObjects.First(x => x.internalObject);
                if (GUI.Button(new Rect(5, 65, 190, 20), "Set Spawn"))
                {
                    startPosition = PlayerMovement.Instance.gameObject.transform.position;
                    spawnObject.aPosition = startPosition;
                }
                if (GUI.Button(new Rect(5, 90, 190, 20), "Set Spawn Orientation"))
                {
                    startOrientation = Camera.main.transform.rotation.eulerAngles.y;
                    spawnObject.aRotation = new Vector3(0, startOrientation, 0);
                }

                startGunDD.Draw(new Rect(95, 40, 100, 20));

            }, "Map Settings");

            if (SelectedObject.Selected && SelectedObject.Type == SelectedObject.SelectedType.EditorObject && !SelectedObject.Object.data.IsPrefab && !SelectedObject.Object.internalObject)
            {
                picker.DrawWindow();
                SelectedObject.Object.go.GetComponent<MeshRenderer>().material.color = picker.color;
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
            if (levelName == "") return;
            
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
                    foreach(var obj in globalObject.AllEditorObjects())
                    {
                        if(obj.go == hit.transform.gameObject)
                        {
                            SelectedObject.SelectObject(obj);
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
            
            // update gizmo
            if (SelectedObject.Selected && !(SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && SelectedObject.Group.isGlobal))
            {
                Vector3 pos = SelectedObject.Basic.worldPos;

                clickGizmo[0].transform.position = pos;
                clickGizmo[1].transform.position = pos + new Vector3(0, 0, 1);
                clickGizmo[1].transform.rotation = Quaternion.Euler(90, 0, 0);
                clickGizmo[2].transform.position = pos + new Vector3(0, 1, 0);
                clickGizmo[2].transform.rotation = Quaternion.Euler(0, 0, 0);
                clickGizmo[3].transform.position = pos + new Vector3(1, 0, 0);
                clickGizmo[3].transform.rotation = Quaternion.Euler(0, 0, 90);
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    if (clickGizmo[1].gameObject.activeSelf)
                        clickGizmo[1].gameObject.SetActive(false);
                    clickGizmo[2].transform.position = pos + SelectedObject.Basic.transformUp;
                    clickGizmo[2].transform.rotation = SelectedObject.Basic.transformRotation;
                    clickGizmo[3].transform.position = pos + SelectedObject.Basic.transformRight;
                    clickGizmo[3].transform.rotation = Quaternion.Euler(SelectedObject.Basic.transformRotation.eulerAngles + new Vector3(0f, 0f, 90f));
                }
                else
                {
                    if (!clickGizmo[1].gameObject.activeSelf)
                        clickGizmo[1].gameObject.SetActive(true);
                }

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
                            gizmoMoveDirection = clickGizmo[1].transform.up;
                        if (clickGizmo[2] == hit.transform.gameObject)
                            gizmoMoveDirection = clickGizmo[2].transform.up;
                        if (clickGizmo[3] == hit.transform.gameObject)
                            gizmoMoveDirection = -clickGizmo[3].transform.up;
                    }
                    oldGizmoDistance = hit.distance;
                }
                if (Input.GetMouseButton(0) && gizmoMoveDirection != Vector3.zero)
                {
                    Vector3 delta = gizmoMoveDirection * Vector3Extensions.DistanceOnDirection(gizmoMovePos, ray.GetPoint(oldGizmoDistance), gizmoMoveDirection);

                    SelectedObject.Basic.MoveByGizmo(delta);
                    gizmoMovePos = ray.GetPoint(oldGizmoDistance);
                }
                if (Input.GetMouseButtonUp(0))
                {
                    gizmoMoveDirection = Vector3.zero;
                    aligned = false;
                }
            }
            else
            {
                clickGizmo[0].transform.position = new Vector3(5000, 5000, 5100);
                clickGizmo[1].transform.position = new Vector3(5000, 5000, 5100);
                clickGizmo[2].transform.position = new Vector3(5000, 5000, 5100);
                clickGizmo[3].transform.position = new Vector3(5000, 5000, 5100);
            }

            EditorObject spawnObject = globalObject.editorObjects.First(x => x.internalObject);
            startPosition = spawnObject.aPosition;
            startOrientation = spawnObject.aRotation.y;
            
            // grid align
            if(gridAlign != 0)
            {
                if(gridAlign < 0)
                {
                    gridAlign = 0;
                }
                else
                {
                    if (!aligned)
                    {
                        Loadson.Console.Log("Aligning objects");
                        foreach (var obj in globalObject.AllBasicOperableObjects())
                        {
                            obj.aPosition += Vector3Extensions.Snap(obj.aPosition, gridAlign) - obj.aPosition;
                            obj.aRotation += Vector3Extensions.Snap(obj.aRotation, gridAlign) - obj.aRotation;
                            obj.aScale += Vector3Extensions.Snap(obj.aScale, gridAlign) - obj.aScale;
                        }
                        aligned = true;
                    }
                }
            }
        }

        private static void ExitEditor()
        {
            AudioListener.volume = SaveManager.Instance.state.volume;
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

        private static byte[] SaveObjectGroup(ObjectGroup objGroup)
        {
            using(MemoryStream ms = new MemoryStream())
            using(BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(objGroup.go.name);
                bw.Write(objGroup.aPosition);
                bw.Write(objGroup.aRotation);
                bw.Write(objGroup.aScale);
                if(objGroup.isGlobal)
                    bw.Write(objGroup.editorObjects.Count - 1);
                else
                    bw.Write(objGroup.editorObjects.Count);
                foreach (var obj in objGroup.editorObjects)
                {
                    if (obj.internalObject) continue;
                    bw.Write(obj.data.IsPrefab);
                    bw.Write(obj.go.name);
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
                bw.Write(objGroup.objectGroups.Count);
                foreach (var group in objGroup.objectGroups)
                    bw.WriteByteArray(SaveObjectGroup(group));
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] SaveLevelData()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(3);
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
                bw.WriteByteArray(SaveObjectGroup(globalObject));
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
            ObjectGroup ReplicateObjectGroup(LevelPlayer.LevelData.ObjectGroup group, ObjectGroup parentGroup)
            {
                ObjectGroup objGroup = new ObjectGroup(group.Name);
                if (parentGroup != null)
                {
                    objGroup.go.transform.parent = parentGroup.go.transform;
                    parentGroup.objectGroups.Add(objGroup);
                }
                objGroup.go.transform.localPosition = group.Position;
                objGroup.go.transform.localRotation = Quaternion.Euler(group.Rotation);
                objGroup.go.transform.localScale = group.Scale;
                foreach(var obj in group.Objects)
                {
                    var eo = new EditorObject(obj);
                    objGroup.editorObjects.Add(eo);
                    eo.go.transform.parent = objGroup.go.transform;
                    eo.go.transform.localPosition = obj.Position;
                    eo.go.transform.localRotation = Quaternion.Euler(obj.Rotation);
                    eo.go.transform.localScale = obj.Scale;
                }
                foreach(var grp in group.Groups)
                    ReplicateObjectGroup(grp, objGroup);
                return objGroup;
            }

            LevelPlayer.LevelData data = new LevelPlayer.LevelData(File.ReadAllBytes(path));
            levelName = Path.GetFileNameWithoutExtension(path);
            gridAlign = data.gridAlign;
            startingGun = data.startingGun;
            startGunDD.Index = startingGun;
            startPosition = data.startPosition;
            startOrientation = data.startOrientation;
            textures = data.Textures.ToList();
            // kme v1 compatibility
            if (!data.isKMEv2)
            {
                globalObject = new ObjectGroup(levelName);
                globalObject.isGlobal = true;
                foreach(var obj in data.Objects)
                    globalObject.AddObject(new EditorObject(obj));
            }
            else
            {
                globalObject = ReplicateObjectGroup(data.GlobalObject, null);
                globalObject.isGlobal = true;
            }
            var spawn = new EditorObject(startPosition, startOrientation);
            globalObject.editorObjects.Insert(0, spawn);
            spawn.go.transform.parent = globalObject.go.transform;
            dd_level = true;
            Time.timeScale = 0f;
            SelectedObject.Deselect();
            aligned = false;
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

        public interface IBasicProperties
        {
            Vector3 aPosition { get; set; }
            Vector3 aRotation { get; set; }
            Vector3 aScale { get; set; }
            string aName { get; set; }
            Quaternion transformRotation { get; }
            Vector3 transformUp { get; }
            Vector3 transformRight { get; }
            Vector3 worldPos { get; }
            void MoveByGizmo(Vector3 delta);
        }

        public class EditorObject : IBasicProperties
        {
            public EditorObject(Vector3 position)
            {
                go = LoadsonAPI.PrefabManager.NewCube();
                go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[6];
                go.transform.position = position;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                data = new LevelPlayer.LevelData.LevelObject(go.transform.localPosition, go.transform.localRotation.eulerAngles, go.transform.localScale, 6, Color.white, go.name, false, false, false, false, false);
            }

            public EditorObject(int _prefabId, Vector3 position)
            {
                go = LevelPlayer.LevelData.MakePrefab(_prefabId);
                go.transform.position = position;
                go.transform.rotation = Quaternion.identity;
                if (go.GetComponent<Rigidbody>() != null) go.GetComponent<Rigidbody>().isKinematic = true;
                data = new LevelPlayer.LevelData.LevelObject(_prefabId, go.transform.localPosition, go.transform.localRotation.eulerAngles, go.transform.localScale, go.name, 0);
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

            public void Clone(ObjectGroup parent)
            {
                EditorObject toAdd;
                if (data.IsPrefab)
                    toAdd = new EditorObject(data.PrefabId, aPosition);
                else
                {
                    toAdd = new EditorObject(aPosition);
                    toAdd.data.TextureId = data.TextureId;
                    if (data.TextureId < Main.gameTex.Length)
                        toAdd.go.GetComponent<MeshRenderer>().material.mainTexture = Main.gameTex[data.TextureId];
                    else
                        toAdd.go.GetComponent<MeshRenderer>().material.mainTexture = textures[data.TextureId - Main.gameTex.Length];
                    toAdd.go.GetComponent<MeshRenderer>().material.color = go.GetComponent<MeshRenderer>().material.color;

                    toAdd.data.Bounce = data.Bounce;
                    toAdd.data.Glass = data.Glass;
                    toAdd.data.Lava = data.Lava;
                    toAdd.data.DisableTrigger = data.DisableTrigger;
                    toAdd.data.MarkAsObject = data.MarkAsObject;
                }
                parent.editorObjects.Add(toAdd);
                toAdd.go.transform.parent = parent.go.transform;
                toAdd.aPosition = aPosition;
                toAdd.aRotation = aRotation;
                toAdd.aScale = aScale;
                toAdd.go.name = go.name + " (Clone)";
            }

            public Vector3 aPosition
            {
                get
                {
                    if (data.Position != go.transform.localPosition)
                        data.Position = go.transform.localPosition;
                    return go.transform.localPosition;
                }
                set { go.transform.localPosition = value; data.Position = value; }
            }
            public Vector3 aRotation
            {
                get
                {
                    if (data.Rotation != go.transform.localRotation.eulerAngles)
                        data.Rotation = go.transform.localRotation.eulerAngles;
                    return go.transform.localRotation.eulerAngles;
                }
                set { go.transform.localRotation = Quaternion.Euler(value); data.Rotation = value; }
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
            public string aName
            {
                get => go.name;
                set => go.name = value;
            }
            public Quaternion transformRotation => go.transform.rotation;
            public Vector3 transformUp => go.transform.up;
            public Vector3 transformRight => go.transform.right;
            public Vector3 worldPos => go.transform.position;
            public void MoveByGizmo(Vector3 delta)
            {
                go.transform.position += delta;
                data.Position = go.transform.localPosition;
            }
        }

        public class ObjectGroup : IBasicProperties
        {
            public bool isGlobal = false;
            public List<EditorObject> editorObjects;
            public List<ObjectGroup> objectGroups;
            public GameObject go;

            public void AddObject(EditorObject obj)
            {
                editorObjects.Add(obj);
                obj.go.transform.parent = go.transform;
            }
            public void AddGroup(ObjectGroup group)
            {
                objectGroups.Add(group);
                group.go.transform.parent = go.transform;
            }

            public int CountAll()
            {
                if (objectGroups.Count == 0) return editorObjects.Count;
                int objg = 0;
                foreach (var g in objectGroups)
                    objg += g.CountAll();
                return objg + editorObjects.Count;
            }

            public void DereferenceGroup(ObjectGroup group)
            {
                if (objectGroups.Contains(group))
                {
                    objectGroups.Remove(group);
                    return;
                }
                foreach (var g in objectGroups)
                    g.DereferenceGroup(group);
            }
            public void DereferenceObject(EditorObject obj)
            {
                if (editorObjects.Contains(obj))
                {
                    editorObjects.Remove(obj);
                    return;
                }
                foreach (var g in objectGroups)
                    g.DereferenceObject(obj);
            }

            public ObjectGroup FindParentOfChild(ObjectGroup target)
            {
                if (objectGroups.Contains(target))
                    return this;
                foreach (var g in objectGroups)
                {
                    var result = g.FindParentOfChild(target);
                    if (result != null) return result;
                }
                return null;
            }
            public ObjectGroup FindParentOfChild(EditorObject target)
            {
                if (editorObjects.Contains(target))
                    return this;
                foreach (var g in objectGroups)
                {
                    var result = g.FindParentOfChild(target);
                    if (result != null) return result;
                }
                return null;
            }

            public ObjectGroup Clone(ObjectGroup parent)
            {
                ObjectGroup ret = new ObjectGroup(go.name);
                if (parent != null)
                    ret.go.transform.parent = parent.go.transform;
                ret.aPosition = aPosition;
                ret.aRotation = aRotation;
                ret.aScale = aScale;
                if (parent != null)
                    parent.objectGroups.Add(ret);
                foreach (var g in objectGroups)
                    g.Clone(ret);
                foreach (var o in editorObjects)
                    o.Clone(ret);
                return ret;
            }

            public List<EditorObject> AllEditorObjects()
            {
                List<EditorObject> ret = new List<EditorObject>(editorObjects);
                foreach(var g in objectGroups)
                    ret.Concat(g.AllEditorObjects());
                return ret;
            }

            public List<IBasicProperties> AllBasicOperableObjects()
            {
                List<IBasicProperties> ret = new List<IBasicProperties>();
                ret.Add(this);
                ret.AddRange(editorObjects);
                foreach (var g in objectGroups)
                    ret.AddRange(g.AllBasicOperableObjects());
                return ret;
            }

            public ObjectGroup(string name = null)
            {
                if (name == null)
                    name = "Object Group #" + UnityEngine.Random.Range(0, 32768);
                go = new GameObject(name);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                editorObjects = new List<EditorObject>();
                objectGroups = new List<ObjectGroup>(); 
            }

            public Vector3 aPosition
            {
                get => go.transform.localPosition;
                set => go.transform.localPosition = value;
            }
            public Vector3 aRotation
            {
                get => go.transform.localRotation.eulerAngles;
                set => go.transform.localRotation = Quaternion.Euler(value);
            }
            public Vector3 aScale
            {
                get => go.transform.localScale;
                set => go.transform.localScale = value;
            }
            public string aName
            {
                get => go.name;
                set => go.name = value;
            }
            public Quaternion transformRotation => go.transform.rotation;
            public Vector3 transformUp => go.transform.up;
            public Vector3 transformRight => go.transform.right;
            public Vector3 worldPos => go.transform.position;
            public void MoveByGizmo(Vector3 delta)
            {
                go.transform.position += delta;
            }
        }
    }
}
