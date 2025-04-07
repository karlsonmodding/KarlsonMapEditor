using Loadson;
using LoadsonAPI;
using Microsoft.SqlServer.Server;
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
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Google.Protobuf;
using System.Buffers;
using static KarlsonMapEditor.MapGeometry.Types;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.Remoting.Messaging;
using UnityEngine.Networking.Types;

namespace KarlsonMapEditor
{
    public static class LevelEditor
    {
        private delegate bool skipObject(EditorObject obj);

        private static bool _initd = false;
        private static void _init()
        {
            if (_initd) return;
            _initd = true;

            const int WindowCount = 7;
            wid = new int[WindowCount];
            wirInitial = new Rect[WindowCount];
            wir = new Rect[WindowCount];

            for (int i = 0; i < WindowCount; i++)
                wid[i] = ImGUI_WID.GetWindowId();

            wirInitial[(int)WindowId.Startup] = new Rect((Screen.width - 600) / 2, (Screen.height - 300) / 2, 600, 300);
            wirInitial[(int)WindowId.Prompt] = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 35, 200, 70);
            wirInitial[(int)WindowId.TexBrowser] = new Rect((Screen.width - 800) / 2, (Screen.height - 860) / 2, 800, 860);
            wirInitial[(int)WindowId.LevelBrowser] = new Rect(Screen.width - 305, 30, 300, 480);
            wirInitial[(int)WindowId.ObjectManip] = new Rect(Screen.width - 305, 520, 300, 550);
            wirInitial[(int)WindowId.LevelData] = new Rect(Screen.width - 510, 30, 200, 185);
            wirInitial[(int)WindowId.SkyboxEdit] = new Rect(Screen.width - 610, 220, 300, 650);

            wir = wirInitial.ToArray();

            multiPick = new GUIStyle();
            Texture2D orange = new Texture2D(1, 1);
            orange.SetPixel(0, 0, new Color(1f, 0.64f, 0f));
            orange.Apply();
            multiPick.active.background = orange;
            multiPick.active.textColor = Color.white;
            multiPick.normal.background = orange;
            multiPick.normal.textColor = Color.white;
            multiPick.focused.background = orange;
            multiPick.hover.background = orange;

            picker = new ColorPicker(Color.white, Screen.width - 475, 540);

            enemyGun = new GUIex.Dropdown(new string[] { "None", "Pistol", "Ak47 / Uzi", "Shotgun", "Boomer" }, 0);
            startGunDD = new GUIex.Dropdown(new string[] { "None", "Pistol", "Ak47 / Uzi", "Shotgun", "Boomer", "Grappler" }, 0);
            spawnPrefabDD = new GUIex.Dropdown(Enum.GetNames(typeof(PrefabType)).Prepend("Spawn Prefab").ToArray(), 0);
            materialMode = new GUIex.Dropdown(Enum.GetNames(typeof(MaterialManager.ShaderBlendMode)), 0);
            skyboxMode = new GUIex.Dropdown(Enum.GetNames(typeof(SkyboxMode)), 0);
            spawnGeometry = new GUIex.Dropdown(Enum.GetNames(typeof(GeometryShape)).Prepend("Spawn Geometry").ToArray(), 0);
        }
        private enum WindowId
        {
            Startup = 0,
            Prompt,
            TexBrowser,
            LevelBrowser,
            ObjectManip,
            LevelData,
            SkyboxEdit,
        }

        private static GUIStyle multiPick;
        private static ColorPicker picker;

        private static int[] wid;
        private static Rect[] wir;
        private static Rect[] wirInitial;

        public static bool editorMode { get; private set; } = false;
        static Coroutine fileWatcher = null;
        public static void StartEdit()
        {
            _init();
            editorMode = true;
            levelName = "";
            SceneManager.sceneLoaded += InitEditor;
            SceneManager.LoadScene(6);
            if (File.Exists(Path.Combine(Main.directory, "_temp.amta")))
                File.Delete(Path.Combine(Main.directory, "_temp.amta"));
            if(fileWatcher != null)
                Coroutines.StopCoroutine(fileWatcher);
        }

        // clickable gizmo
        private static GameObject clickGizmo;
        private static GameObject GizmoXAxis;
        private static GameObject GizmoYAxis;
        private static GameObject GizmoZAxis;
        // gizmo colors
        private static Color xColor = new Color(0.7f, 0.3f, 0.3f, 1);
        private static Color yColor = new Color(0.3f, 0.7f, 0.3f, 1);
        private static Color zColor = new Color(0.3f, 0.3f, 0.7f, 1);
        private static Color xHighlightColor = new Color(1, 0, 0, 1);
        private static Color yHighlightColor = new Color(0, 1, 0, 1);
        private static Color zHighlightColor = new Color(0, 0, 1, 1);
        // gizmo constants
        const int gizmoLayer = 19;
        const int gizmoLayerMask = 1 << gizmoLayer;
        const float clickGizmoScaleFactor = 0.25f;
        // gizmo handling
        static bool onGizmo = false;
        static bool holdingGizmo = false;
        static Vector3 gizmoPos;
        static Vector3 initialGizmoPoint;
        static Vector3 gizmoMoveDirection;
        public enum GizmoMode
        {
            Translate,
            Scale,
            Rotate
        }
        private static GizmoMode _gizmoMode;
        static GizmoMode gizmoMode
        {
            get { return _gizmoMode; }
            set
            {
                if (_gizmoMode == value) { return; }
                Mesh mesh = MeshBuilder.AxisMeshes[(int)value];
                GizmoXAxis.GetComponent<MeshFilter>().sharedMesh = mesh;
                GizmoXAxis.GetComponent<MeshCollider>().sharedMesh = mesh;
                GizmoYAxis.GetComponent<MeshFilter>().sharedMesh = mesh;
                GizmoYAxis.GetComponent<MeshCollider>().sharedMesh = mesh;
                GizmoZAxis.GetComponent<MeshFilter>().sharedMesh = mesh;
                GizmoZAxis.GetComponent<MeshCollider>().sharedMesh = mesh;
                _gizmoMode = value;
            }
        }
        // controls
        const KeyCode scaleGizmoKey = KeyCode.LeftControl;
        const KeyCode rotateGizmoKey = KeyCode.LeftShift;
        // this is what unity uses for Input.GetMouseButton()
        const int LeftMouseButton = 0;
        const int RightMouseButton = 1;
        const int MiddleMouseButton = 2;

        // grid alignmnet
        const float positionSnap = 0.5f;
        const float scaleSnap = 1f;
        const float rotationSnap = 15f;

        // non-clickable gizmo
        static Camera gizmoCamera;

        static GUIex.Dropdown enemyGun;
        static GUIex.Dropdown startGunDD;
        static GUIex.Dropdown spawnPrefabDD;
        static GUIex.Dropdown materialMode;
        static GUIex.Dropdown skyboxMode;
        static GUIex.Dropdown spawnGeometry;
        private static void InitEditor(Scene arg0, LoadSceneMode arg1)
        {
            AudioListener.volume = 0;
            SceneManager.sceneLoaded -= InitEditor;
            // remove objects with colliders
            foreach (Collider c in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (c.gameObject != PlayerMovement.Instance.gameObject & c.gameObject.GetComponent<DetectWeapons>() == null) UnityEngine.Object.Destroy(c.gameObject);
            // remove lights
            foreach (Light l in UnityEngine.Object.FindObjectsOfType<Light>())
                if (l != RenderSettings.sun) UnityEngine.Object.Destroy(l.gameObject);

            PlayerMovement.Instance.gameObject.GetComponent<Rigidbody>().isKinematic = false;
            PlayerMovement.Instance.gameObject.GetComponent<Rigidbody>().useGravity = false; //eh
            PlayerMovement.Instance.gameObject.GetComponent<Collider>().enabled = false;

            // lighting and reflections
            GameObject bakerGO = new GameObject();
            baker = bakerGO.AddComponent<EnvironmentBaker>();
            GameObject sunGO = new GameObject();
            sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.enabled = false;

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
            clickGizmo = new GameObject();
            clickGizmo.layer = gizmoLayer;
            clickGizmo.SetActive(false);

            // create the axis for the gizmo
            GizmoXAxis = MeshBuilder.GetAxisGO(GizmoMode.Translate);
            GizmoXAxis.transform.parent = clickGizmo.transform;
            GizmoXAxis.transform.rotation = Quaternion.Euler(0, 90, 0);
            GizmoXAxis.layer = gizmoLayer;
            
            GizmoYAxis = MeshBuilder.GetAxisGO(GizmoMode.Translate);
            GizmoYAxis.transform.parent = clickGizmo.transform;
            GizmoYAxis.transform.rotation = Quaternion.Euler(-90, 0, 0);
            GizmoYAxis.layer = gizmoLayer;

            GizmoZAxis = MeshBuilder.GetAxisGO(GizmoMode.Translate);
            GizmoZAxis.transform.parent = clickGizmo.transform;
            GizmoZAxis.transform.rotation = Quaternion.Euler(0, 0, 0);
            GizmoZAxis.layer = gizmoLayer;

            gizmoCamera = UnityEngine.Object.Instantiate(Camera.main);
            UnityEngine.Object.Destroy(gizmoCamera.transform.Find("GunCam").gameObject);
            UnityEngine.Object.Destroy(gizmoCamera.transform.Find("Particle System").gameObject);
            gizmoCamera.transform.parent = Camera.main.transform;
            gizmoCamera.clearFlags = CameraClearFlags.Depth;
            gizmoCamera.cullingMask = gizmoLayerMask;
            gizmoCamera.fieldOfView = GameState.Instance.GetFov();

            enemyGun.Index = 0;
            startGunDD.Index = 0;
            spawnPrefabDD.Index = 0;
        }

        static EditorObject clipboardObj;
        public static string levelName { get; private set; } = "";
        private static ObjectGroup globalObject;
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

        // skybox window
        private enum SkyboxMode
        {
            Default,
            Procedural,
            SixSided,
        }
        private static readonly string[] ordinalDirecitons = new string[] { "Front", "Back", "Left", "Right", "Up", "Down" };

        public static Material ProceduralSkybox;
        public static Material SixSidedSkybox;
        private static bool skyboxEditorEnabled = false;

        // lighting
        private static Light sun;
        private static EnvironmentBaker baker;

        // texture browser window
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
                    picker.color = obj.go.GetComponent<MeshRenderer>().sharedMaterial.color;

                Identify();

                // select parent group to be able to add objects easier
                Group = globalObject.FindParentOfChild(Object);
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
        private static Vector2 object_browser_scroll = new Vector2(0, 0);

        private static bool unsaved = false;
        private static void MarkAsModified()
        { // function because i might add stuff later here (such as ctrl+z)
            unsaved = true;
        }
        private static void MarkAsSaved()
        {
            unsaved = false;
        }

        private static bool EnablePicker = false;
        private static Action<Color> UpdateSourceColor;
        private static void ColorButton(Color source, Action<Color> updateSource, int width = 100, int height = 15)
        {
            if (picker.DrawPreviewButton(source, width, height))
            {
                picker.color = source;
                EnablePicker = true;

                UpdateSourceColor = updateSource;
            }
        }

        public static void _ongui()
        {
            if (!editorMode) return;

            // check for windows out of bound (if the user right clicks them, they glitch
            // out because also right-clicking enables camera move, which locks cursor)

            foreach (WindowId windowId in Enum.GetValues(typeof(WindowId)))
            {
                if (wir[(int)windowId].x < 0 || wir[(int)windowId].y < 0)
                    wir[(int)windowId] = wirInitial[(int)windowId];
            }

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

                                MaterialManager.InitInternalTextures();
                                sun.Reset();
                                sun.enabled = false;
                                sun.gameObject.transform.rotation = Quaternion.Euler(70, 0, 0);
                                skyboxMode.Index = (int)SkyboxMode.Default;
                                dd_level = true;
                                Time.timeScale = 0f;
                                SelectedObject.Deselect();

                                File.WriteAllText(Path.Combine(Main.directory, "_temp.amta"), "# Any code written below will be executed every time the level is (re)started.\n# Be sure to use LF instead of CRLF line endings.\n# Any modifications to this file will be reflected by marking the level as modified (unsaved).\n# Saving the level will also save this script.\n# Automata documentation: https://github.com/devilExE3/automataV2/blob/master/automata.md\n# KME API: https://github.com/karlsonmodding/KarlsonMapEditor/wiki/Scripting-API\n$:print(\"Hello, world!\")");
                                fileWatcher = Coroutines.StartCoroutine(FileWatcher());
                                //Process.Start(Path.Combine(Main.directory, "_temp.amta"));
                            }
                        }

                        Dialog("Enter map name:", recurence);
                    }
                    if (GUI.Button(new Rect(208f, 29f, 184f, 40f), "Load existing map"))
                    {
                        void recurence(string file)
                        {
                            if (!File.Exists(Path.Combine(Main.directory, "Levels", file + ".kme")))
                                Dialog("Enter map name:", recurence, "File doesn't exist");
                            else
                                LoadLevel(Path.Combine(Main.directory, "Levels", file + ".kme"));
                        }

                        Dialog("Enter map name:", recurence);
                    }

                    if (GUI.Button(new Rect(404f, 29f, 184f, 40f), "Exit editor")) ExitEditor();
                    GUI.Label(new Rect(5f, 75f, 500f, 20f), "Recent Maps:");
                    List<string> oldList = Main.prefs["edit_recent"].Split(';').ToList();
                    while (oldList.Count < 5) oldList.Insert(0, "");
                    recent_select = GUI.SelectionGrid(new Rect(5f, 95f, 590f, 175f), recent_select, new string[] { oldList[4], oldList[3], oldList[2], oldList[1], oldList[0] }, 1);
                    if (GUI.Button(new Rect(5f, 275f, 590f, 20f), "Load Map"))
                    {
                        if (oldList[4 - recent_select] != "" && File.Exists(Path.Combine(Main.directory, "Levels", oldList[4 - recent_select])))
                            LoadLevel(Path.Combine(Main.directory, "Levels", oldList[4 - recent_select]));
                    }
                    GUI.DragWindow();
                }, "Welcome to the Karlson Map Editor!");
                return;
            }

            var _countAll = globalObject.CountAll();

            GUI.Box(new Rect(-5, 0, Screen.width + 10, 20), "");
            if (GUI.Button(new Rect(0, 0, 100, 20), "File")) { dd_file = !dd_file; dd_level = false; }
            if (GUI.Button(new Rect(100, 0, 100, 20), "Map")) { dd_level = !dd_level; dd_file = false; }
            if (GUI.Button(new Rect(200, 0, 100, 20), "Skybox Editor")) skyboxEditorEnabled = !skyboxEditorEnabled;
            if (GUI.Button(new Rect(300, 0, 100, 20), "KMP Export")) KMPExporter.Export(levelName, globalObject, MaterialManager.GetExternalTextures());
            GUI.Label(new Rect(405, 0, 1000, 20), $"<b>Karlson Map Editor</b> v3.1 | Current map: <b>{(unsaved ? (levelName + '*') : levelName)}</b> | Object count: <b>{_countAll.Item1 + _countAll.Item2}</b> | Hold <b>right click</b> down to move and look around | Select an object by <b>middle clicking</b> it");
            if (GUI.Button(new Rect(Screen.width - 100, 0, 100, 20), "Open Script")) Process.Start(Path.Combine(Main.directory, "_temp.amta"));

            if (dd_file)
            {
                GUI.Box(new Rect(0, 20, 150, 80), "");
                if (GUI.Button(new Rect(0, 20, 150, 20), "Save Map")) { SaveLevel(); dd_file = false; }
                if (GUI.Button(new Rect(0, 40, 150, 20), "Close Map"))
                {
                    if (unsaved)
                    {
                        Dialog("Unsaved edits!", (_) => StartEdit(), "Close map anyway?");
                    }
                    else
                    {
                        StartEdit();
                    }
                    dd_file = false;
                }
                if (GUI.Button(new Rect(0, 60, 150, 20), "Upload to Workshop")) dg_screenshot = true;
                if (GUI.Button(new Rect(0, 80, 150, 20), "Exit Editor"))
                {
                    if (unsaved)
                    {
                        Dialog("Unsaved edits!", (_) => ExitEditor(), "Close map anyway?");
                    }
                    else
                    {
                        ExitEditor();
                    }
                    dd_file = false;
                }
            }

            if (dd_level)
            {
                GUI.Box(new Rect(100, 20, 300, 20), "");
                if (!SelectedObject.Selected)
                {
                    GUI.Label(new Rect(105, 20, 300, 20), "Select an Object / Object Group to spawn objects");
                }
                else
                {
                    spawnPrefabDD.Draw(new Rect(100, 20, 150, 20));

                    Vector3 spawnPos = PlayerMovement.Instance.gameObject.transform.position;
                    if (gridAlign != 0) { spawnPos = Vector3Extensions.Snap(spawnPos, 1); }
                    if (spawnPrefabDD.Index != 0)
                    {
                        if ((PrefabType)(spawnPrefabDD.Index - 1) == PrefabType.Milk)
                        {
                            ObjectGroup container = new ObjectGroup("Prefab Container");
                            SelectedObject.Group.AddGroup(container);
                            container.AddObject(new EditorObject(PrefabType.Milk, Vector3.zero));
                            container.go.transform.position = spawnPos;
                            SelectedObject.SelectGroup(container);
                        }
                        else
                        {
                            SelectedObject.Group.AddObject(new EditorObject((PrefabType)(spawnPrefabDD.Index - 1), spawnPos));
                            SelectedObject.SelectObject(SelectedObject.Group.editorObjects.Last());
                        }
                        MarkAsModified();
                        spawnPrefabDD.Index = 0;
                    }
                    
                    spawnGeometry.Draw(new Rect(250, 20, 150, 20));

                    if (spawnGeometry.Index > 0)
                    {
                        GeometryShape shape = (GeometryShape)(spawnGeometry.Index - 1);
                        spawnGeometry.Index = 0;
                        MarkAsModified();
                        SelectedObject.Group.AddObject(new EditorObject(spawnPos, shape));
                        SelectedObject.SelectObject(SelectedObject.Group.editorObjects.Last());
                    }
                }
            }

            if (EnablePicker)
            {
                picker.DrawWindow(
                    delegate { EnablePicker = false; },
                    delegate (Color c) { MarkAsModified(); UpdateSourceColor(c); }
                    );
            }

            if (tex_browser_enabled) wir[(int)WindowId.TexBrowser] = GUI.Window(wid[(int)WindowId.TexBrowser], wir[(int)WindowId.TexBrowser], (windowId) => {
                if (GUI.Button(new Rect(750, 0, 50, 20), "Close"))
                    tex_browser_enabled = false;
                if (GUI.Button(new Rect(650, 0, 100, 20), "Load Texture"))
                {
                    string picked = FilePicker.PickFile("Select the texture you wish to import", "Images\0*.png;*.jpg;*.jpeg;*.bmp\0All Files\0*.*\0\0");
                    if (picked != "null")
                    {
                        Texture2D tex = new Texture2D(1, 1);
                        tex.LoadImage(File.ReadAllBytes(picked));
                        tex.name = Path.GetFileName(picked);
                        MaterialManager.AddTexture(tex);

                        MarkAsModified();
                    }
                }
                tex_browser_scroll = GUI.BeginScrollView(new Rect(0, 20, 900, 800), tex_browser_scroll, new Rect(0, 0, 800, 10000));
                int i = 0;
                bool[] texturesUsed = MaterialManager.TexturesInUse();
                foreach (Texture2D t in MaterialManager.Textures)
                {
                    // creating the display of textures to choose from
                    GUI.DrawTexture(new Rect(200 * (i % 4), 200 * (i / 4), 200, 200), t);
                    GUI.Box(new Rect(200 * (i % 4), 180 + 200 * (i / 4), 200, 20), "");
                    GUI.Label(new Rect(5 + 200 * (i % 4), 180 + 200 * (i / 4), 200, 20), "<size=9>" + t.name + "</size>");
                    // if the selected texture is this one, make the text color green
                    string color = "^";
                    if (MaterialManager.SelectedTexture == t)
                        color = "<color=green>^</color>";
                    // select a texture
                    if (GUI.Button(new Rect(180 + 200 * (i % 4), 180 + 200 * (i / 4), 20, 20), color))
                    {
                        MarkAsModified();
                        MaterialManager.SelectedTexture = t;
                        MaterialManager.UpdateSelectedTexture(t);
                    }

                    if (!texturesUsed[i]) // texture is not used in any materials
                    {
                        if (GUI.Button(new Rect(200 * (i % 4) + 130, 200 * (i / 4) + 160, 70, 20), "Remove"))
                        {
                            MarkAsModified();
                            MaterialManager.RemoveTexture(t);
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
                        GUI.Label(new Rect(depth * 20 + 40, j * 25, 200, 20), (obj.data.IsPrefab ? obj.data.PrefabId.ToString() : obj.data.ShapeId.ToString()) + " | " + obj.go.name);
                        ++j;
                    }
                    // close group
                    GUI.Box(new Rect(depth * 20 + 7, firstj * 25 - 5, 5, (j - firstj) * 25 + 5), "");
                    GUI.Box(new Rect(depth * 20 + 7, j * 25, 200, 5), "");
                    ++j;
                    return j;
                }

                GUI.DragWindow(new Rect(0, 0, 1000, 20));
                if (SelectedObject.Selected)
                    if (GUI.Button(new Rect(260, 20, 20, 20), "+"))
                    {
                        MarkAsModified();
                        SelectedObject.Group.AddGroup(new ObjectGroup());
                        SelectedObject.SelectGroup(SelectedObject.Group.objectGroups.Last());
                        return;
                    }

                object_browser_scroll = GUI.BeginScrollView(new Rect(0, 20, 300, 480), object_browser_scroll, new Rect(0, 0, 280, _countAll.Item1 * 50 + _countAll.Item2 * 25));

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
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && SelectedObject.Group.isGlobal)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "You can't modify the global object.");
                    GUI.Label(new Rect(5, 35, 300, 20), "You can only add objects (Prefabs / Cube) and groups (+ in the top right)");
                    return;
                }
                string newName = GUI.TextField(new Rect(5f, 20f, 290f, 20f), SelectedObject.Basic.aName);
                if (newName != SelectedObject.Basic.aName)
                {
                    MarkAsModified();
                    SelectedObject.Basic.aName = newName;
                }

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.internalObject)
                    GUI.Label(new Rect(5, 40, 290, 60), "This is an internal object.\nProperties are limited.");
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup || SelectedObject.Type == SelectedObject.SelectedType.EditorObject && !SelectedObject.Object.internalObject)
                {
                    if (GUI.Button(new Rect(5, 40, 75, 20), "Duplicate"))
                    {
                        MarkAsModified();
                        ObjectGroup parent = globalObject.FindParentOfChild(SelectedObject.Object);
                        if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject)
                        {
                            SelectedObject.Object.Clone(parent);
                            SelectedObject.SelectObject(parent.editorObjects.Last());
                            return;
                        }
                        else
                        {
                            parent = globalObject.FindParentOfChild(SelectedObject.Group);
                            SelectedObject.Group.Clone(parent);
                            SelectedObject.SelectGroup(parent.objectGroups.Last());
                            return;
                        }
                    }
                    if (GUI.Button(new Rect(85, 40, 75, 20), "Delete"))
                    {
                        MarkAsModified();
                        SelectedObject.Destroy();
                        return;
                    }
                }
                if (GUI.Button(new Rect(165, 40, 75, 20), "Identify"))
                    SelectedObject.Identify();
                if (GUI.Button(new Rect(245, 40, 50, 20), "Find")) { PlayerMovement.Instance.gameObject.transform.position = SelectedObject.Basic.worldPos + Camera.main.transform.forward * -5f; SelectedObject.Identify(); }

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.data.PrefabId == PrefabType.Enemey)
                {
                    GUI.Label(new Rect(5, 60, 40, 20), "Gun");
                    enemyGun.Draw(new Rect(35, 60, 100, 20));
                    if (SelectedObject.Object.data.PrefabData != enemyGun.Index)
                    {
                        MarkAsModified();
                        SelectedObject.Object.data.PrefabData = enemyGun.Index;
                    }
                }

                GUI.BeginGroup(new Rect(5, 80, 300, 80));
                GUI.Label(new Rect(0, 20, 50, 20), "Pos:");
                {
                    float x = SelectedObject.Basic.aPosition.x, y = SelectedObject.Basic.aPosition.y, z = SelectedObject.Basic.aPosition.z;
                    x = float.Parse(GUI.TextField(new Rect(50, 20, 70, 20), x.ToString("0.00")));
                    y = float.Parse(GUI.TextField(new Rect(125, 20, 70, 20), y.ToString("0.00")));
                    z = float.Parse(GUI.TextField(new Rect(200, 20, 70, 20), z.ToString("0.00")));
                    var newPos = new Vector3(x, y, z);
                    if (newPos != SelectedObject.Basic.aPosition)
                    {
                        MarkAsModified();
                        SelectedObject.Basic.aPosition = newPos;
                    }
                }
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup || !SelectedObject.Object.internalObject)
                {
                    GUI.Label(new Rect(0, 40, 50, 20), "Rot:");
                    {
                        float x = SelectedObject.Basic.aRotation.x, y = SelectedObject.Basic.aRotation.y, z = SelectedObject.Basic.aRotation.z;
                        x = float.Parse(GUI.TextField(new Rect(50, 40, 70, 20), x.ToString("0.00")));
                        y = float.Parse(GUI.TextField(new Rect(125, 40, 70, 20), y.ToString("0.00")));
                        z = float.Parse(GUI.TextField(new Rect(200, 40, 70, 20), z.ToString("0.00")));
                        var newRot = new Vector3(x, y, z);
                        if (SelectedObject.Basic.aRotation != newRot)
                        {
                            MarkAsModified();
                            SelectedObject.Basic.aRotation = newRot;
                        }
                    }

                    GUI.Label(new Rect(0, 60, 50, 20), "Scale:");
                    {
                        float x = SelectedObject.Basic.aScale.x, y = SelectedObject.Basic.aScale.y, z = SelectedObject.Basic.aScale.z;
                        x = float.Parse(GUI.TextField(new Rect(50, 60, 70, 20), x.ToString("0.00")));
                        y = float.Parse(GUI.TextField(new Rect(125, 60, 70, 20), y.ToString("0.00")));
                        z = float.Parse(GUI.TextField(new Rect(200, 60, 70, 20), z.ToString("0.00")));
                        var newScale = new Vector3(x, y, z);
                        if (SelectedObject.Basic.aScale != newScale)
                        {
                            MarkAsModified();
                            SelectedObject.Basic.aScale = newScale;
                        }
                    }
                }
                else
                {
                    GUI.Label(new Rect(0, 40, 50, 20), "Rot:");
                    {
                        float y = SelectedObject.Basic.aRotation.y;
                        y = float.Parse(GUI.TextField(new Rect(125, 40, 70, 20), y.ToString("0.00")));
                        if (SelectedObject.Basic.aRotation.y != y)
                        {
                            MarkAsModified();
                            SelectedObject.Basic.aRotation = new Vector3(0, y, 0);
                        }
                    }
                }
                GUI.EndGroup();

                GUILayout.BeginArea(new Rect(5, 165, 300, 400));
                bool hasMat = SelectedObject.Type == SelectedObject.SelectedType.EditorObject && !SelectedObject.Object.internalObject && !SelectedObject.Object.data.IsPrefab;
                if (hasMat)
                {
                    GUILayout.BeginHorizontal();

                    KMETextureScaling ts = SelectedObject.Object.go.GetComponent<KMETextureScaling>();

                    ts.Enabled = GUILayout.Toggle(ts.Enabled, "UV Normalize");
                    if (ts.Enabled)
                    {
                        ts.Scale = float.Parse(GUILayout.TextField(ts.Scale.ToString("0.00")));
                    }
                    SelectedObject.Object.data.UVNormalizedScale = ts.Scale;
                    
                    GUILayout.EndHorizontal();

                    GUIStyle matStyle = new GUIStyle();
                    matStyle.padding.left = 0;
                    matStyle.padding.right = 0;
                    matStyle.padding.top = 2;
                    matStyle.padding.bottom = 0;
                    matStyle.normal.textColor = Color.white;

                    GUILayout.BeginVertical(matStyle);

                    GUILayout.Label("Material", matStyle);

                    // material label and buttons
                    GUILayout.BeginHorizontal(matStyle);

                    if (GUILayout.Button("New", GUILayout.Width(50)) && hasMat)
                    {
                        MarkAsModified();
                        SelectedObject.Object.data.MaterialId = MaterialManager.Materials.Count();
                        SelectedObject.Object.go.GetComponent<MeshRenderer>().sharedMaterial = MaterialManager.InstanceMaterial();
                    }
                    if (GUILayout.Button("Copy", GUILayout.Width(50)) && hasMat)
                    {
                        clipboardObj = SelectedObject.Object;
                    }
                    if (clipboardObj != null)
                    {
                        if (GUILayout.Button("Paste") && hasMat)
                        {
                            MarkAsModified();
                            SelectedObject.Object.go.GetComponent<MeshRenderer>().sharedMaterial.CopyPropertiesFromMaterial(clipboardObj.go.GetComponent<MeshRenderer>().sharedMaterial);
                        }
                        if (GUILayout.Button("Reference") && hasMat)
                        {
                            MarkAsModified();
                            MaterialManager.ClearMaterial(SelectedObject.Object.go.GetComponent<MeshRenderer>().sharedMaterial);
                            SelectedObject.Object.go.GetComponent<MeshRenderer>().sharedMaterial = clipboardObj.go.GetComponent<MeshRenderer>().sharedMaterial;
                            SelectedObject.Object.data.MaterialId = clipboardObj.data.MaterialId;
                        }
                    }
                    Material selectedMat = SelectedObject.Object.go.GetComponent<MeshRenderer>().sharedMaterial;

                    GUILayout.EndHorizontal();

                    // material properties
                    GUILayout.BeginHorizontal(matStyle);

                    // color and mat sliders
                    GUILayout.BeginVertical(matStyle);

                    GUILayout.BeginHorizontal(matStyle);
                    GUILayout.Label("Color", matStyle);
                    ColorButton(selectedMat.color, delegate (Color c) { selectedMat.color = c; }, 140);
                    GUILayout.EndHorizontal();

                    /* maybe later
                    GUILayout.BeginHorizontal(matStyle);
                    GUILayout.Label("Emission", matStyle);
                    ColorButton(selectedMat.GetColor("_EmissionColor"), delegate (Color c) { selectedMat.SetColor("_EmissionColor", c); });
                    GUILayout.EndHorizontal();
                    */

                    GUILayout.Space(4);

                    int lastMode = (int)selectedMat.GetFloat("_Mode");
                    materialMode.Index = lastMode;
                    Rect matModeRect = GUILayoutUtility.GetRect(120, 20, matStyle);
                    materialMode.Draw(matModeRect);
                    if (lastMode != materialMode.Index)
                    {
                        MarkAsModified();
                        MaterialManager.UpdateMode(selectedMat, (MaterialManager.ShaderBlendMode)materialMode.Index);
                    }
                    Texture2D normalTex = (Texture2D)selectedMat.GetTexture("_BumpMap");
                    Texture2D metalGlossTex = (Texture2D)selectedMat.GetTexture("_MetallicGlossMap");
                    if (metalGlossTex == null)
                    {
                        GUILayout.Label("Smoothness", matStyle);
                        selectedMat.SetFloat("_Glossiness", GUILayout.HorizontalSlider(selectedMat.GetFloat("_Glossiness"), 0, 1));

                        GUILayout.Label("Metallic", matStyle);
                        selectedMat.SetFloat("_Metallic", GUILayout.HorizontalSlider(selectedMat.GetFloat("_Metallic"), 0, 1));
                    }
                    if (normalTex != null)
                    {
                        GUILayout.Label("Normal Scale", matStyle);
                        selectedMat.SetFloat("_BumpScale", GUILayout.HorizontalSlider(selectedMat.GetFloat("_BumpScale"), 0, 5));
                    }

                    if (GUILayout.Toggle(selectedMat.GetFloat("_SpecularHighlights") != 0, "Specular Highlights"))
                    {
                        selectedMat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        selectedMat.SetFloat("_SpecularHighlights", 1f);
                    }
                    else
                    {
                        selectedMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        selectedMat.SetFloat("_SpecularHighlights", 0f);
                    }
                    if (GUILayout.Toggle(selectedMat.GetFloat("_GlossyReflections") != 0, "Glossy Reflections"))
                    {
                        selectedMat.DisableKeyword("_GLOSSYREFLECTIONS_OFF");
                        selectedMat.SetFloat("_GlossyReflections", 1f);
                    }
                    else
                    {
                        selectedMat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                        selectedMat.SetFloat("_GlossyReflections", 0f);
                    }

                    // scale and offset
                    GUILayout.Label("Texture Mapping", matStyle);

                    Vector2 textureScale = selectedMat.GetTextureScale("_MainTex");
                    Vector2 textureOffset = selectedMat.GetTextureOffset("_MainTex");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Scale");
                    textureScale.x = float.Parse(GUILayout.TextField(textureScale.x.ToString("0.00")));
                    textureScale.y = float.Parse(GUILayout.TextField(textureScale.y.ToString("0.00")));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Offset");
                    textureOffset.x = float.Parse(GUILayout.TextField(textureOffset.x.ToString("0.00")));
                    textureOffset.y = float.Parse(GUILayout.TextField(textureOffset.y.ToString("0.00")));
                    GUILayout.EndHorizontal();

                    selectedMat.SetTextureScale("_MainTex", textureScale);
                    selectedMat.SetTextureScale("_BumpMap", textureScale);
                    selectedMat.SetTextureScale("_MetallicGlossMap", textureScale);
                    selectedMat.SetTextureOffset("_MainTex", textureOffset);
                    selectedMat.SetTextureOffset("_BumpMap", textureOffset);
                    selectedMat.SetTextureOffset("_MetallicGlossMap", textureOffset);

                    GUILayout.EndVertical();
                    GUILayout.Space(4);

                    // textures
                    GUILayout.BeginVertical(matStyle);

                    // image
                    const int imageButtonSize = 75;
                    GUILayout.Label("Main Texture", matStyle);
                    if (GUILayout.Button(selectedMat.mainTexture, matStyle, GUILayout.Width(imageButtonSize), GUILayout.Height(imageButtonSize)))
                    {
                        tex_browser_enabled = true;
                        MaterialManager.SelectedTexture = (Texture2D)selectedMat.mainTexture;
                        MaterialManager.UpdateSelectedTexture = delegate (Texture2D tex) { selectedMat.mainTexture = tex; MarkAsModified(); };
                    }
                    
                    if (GUILayout.Toggle(normalTex != null, "Normal Map"))
                    {
                        if (normalTex == null || GUILayout.Button(normalTex, matStyle, GUILayout.Width(imageButtonSize), GUILayout.Height(imageButtonSize)))
                        {
                            tex_browser_enabled = true;
                            MaterialManager.SelectedTexture = normalTex;
                            MaterialManager.UpdateSelectedTexture = delegate (Texture2D tex) {
                                selectedMat.EnableKeyword("_NORMALMAP");
                                selectedMat.SetTexture("_BumpMap", tex);
                                MarkAsModified();
                            };
                        }
                    }
                    else if (normalTex != null) {
                        selectedMat.DisableKeyword("_NORMALMAP");
                        selectedMat.SetTexture("_BumpMap", null);
                        MarkAsModified();
                    }
                    
                    if (GUILayout.Toggle(metalGlossTex != null, "Metal & Gloss"))
                    {
                        if (metalGlossTex == null || GUILayout.Button(metalGlossTex, matStyle, GUILayout.Width(imageButtonSize), GUILayout.Height(imageButtonSize)))
                        {
                            tex_browser_enabled = true;
                            MaterialManager.SelectedTexture = metalGlossTex;
                            MaterialManager.UpdateSelectedTexture = delegate (Texture2D tex) {
                                selectedMat.EnableKeyword("_METALLICGLOSSMAP");
                                selectedMat.SetTexture("_MetallicGlossMap", tex);
                                MarkAsModified();
                            };
                        }
                    }
                    else if (metalGlossTex != null) {
                        selectedMat.DisableKeyword("_METALLICGLOSSMAP");
                        selectedMat.SetTexture("_MetallicGlossMap", null);
                        MarkAsModified();
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                    // draw over
                    materialMode.Draw(matModeRect);
                }
                GUILayout.EndArea();

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && !SelectedObject.Object.data.IsPrefab && !SelectedObject.Object.internalObject)
                {
                    bool bRes;
                    bRes = GUI.Toggle(new Rect(5, 60, 75, 20), SelectedObject.Object.data.Bounce, "Bounce");
                    if (SelectedObject.Object.data.Bounce != bRes) { MarkAsModified(); SelectedObject.Object.data.Bounce = bRes; }
                    
                    // only convex objects can be used as triggers (whyyyy???)
                    MeshCollider mColl = SelectedObject.Object.go.GetComponent<MeshCollider>();
                    if (mColl == null || mColl.convex)
                    {
                        bRes = GUI.Toggle(new Rect(85, 60, 75, 20), SelectedObject.Object.data.Glass, "Glass");
                        if (SelectedObject.Object.data.Glass != bRes) { MarkAsModified(); SelectedObject.Object.data.Glass = bRes; }
                        
                        if (!SelectedObject.Object.data.Glass)
                        {
                            bRes = GUI.Toggle(new Rect(165, 60, 75, 20), SelectedObject.Object.data.Lava, "Lava");
                            if (SelectedObject.Object.data.Lava != bRes) { MarkAsModified(); SelectedObject.Object.data.Lava = bRes; }
                        }
                    }
                    bRes = GUI.Toggle(new Rect(5, 80, 120, 20), SelectedObject.Object.data.MarkAsObject, "Mark as Object");
                    if (SelectedObject.Object.data.MarkAsObject != bRes) { MarkAsModified(); SelectedObject.Object.data.MarkAsObject = bRes; }
                }
                else
                {
                    if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.data.IsPrefab && SelectedObject.Object.data.PrefabId == PrefabType.Enemey) // only draw, so it appears on top
                        enemyGun.Draw(new Rect(35, 60, 100, 20));
                    // for some reason, the first button rendered takes priority over the mouse click
                    // even if it is below .. idk
                }
                GUI.DragWindow(new Rect(0, 0, 300, 20));
            }, "Object Properties");

            wir[(int)WindowId.LevelData] = GUI.Window(wid[(int)WindowId.LevelData], wir[(int)WindowId.LevelData], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 200, 20));
                gridAlign = GUI.Toggle(new Rect(5, 20, 190, 20), gridAlign == 1, "Grid Align") ? 1 : 0;

                GUI.Label(new Rect(5, 40, 100, 20), "Starting Gun");
                startGunDD.Draw(new Rect(95, 40, 100, 20));
                if (startingGun != startGunDD.Index)
                {
                    MarkAsModified();
                    startingGun = startGunDD.Index;

                }

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

            if (skyboxEditorEnabled)
            {
                wir[(int)WindowId.SkyboxEdit] = GUI.Window(wid[(int)WindowId.SkyboxEdit], wir[(int)WindowId.SkyboxEdit], (windowId) =>
                {
                    GUI.DragWindow(new Rect(0, 0, 1000, 20));

                    GUILayout.BeginVertical();

                    if (GUILayout.Toggle(sun.enabled, "Sun"))
                    {
                        if (RenderSettings.sun == null)
                        {
                            sun.enabled = true;
                            RenderSettings.sun = sun;
                        }

                        ColorButton(sun.color, delegate (Color c) { sun.color = c; });

                        GUILayout.Label("Intensity");
                        sun.intensity = GUILayout.HorizontalSlider(sun.intensity, 0, 5);

                        GUILayout.Label("Angle");
                        Vector2 sunAngle = (Vector2)sun.gameObject.transform.rotation.eulerAngles;
                        sun.gameObject.transform.rotation = Quaternion.Euler(GUILayout.HorizontalSlider(((sunAngle.x + 90) % 360) - 90, -90, 90), GUILayout.HorizontalSlider(sunAngle.y, 0, 360), 0);
                    }
                    else
                    {
                        sun.enabled = false;
                        RenderSettings.sun = null;
                    }

                    Rect skyboxModeRect = GUILayoutUtility.GetRect(200, 20);
                    float oldSkyboxIndex = skyboxMode.Index;
                    skyboxMode.Draw(skyboxModeRect);
                    if (skyboxMode.Index != oldSkyboxIndex)
                    {
                        MarkAsModified();
                        baker.UpdateEnvironment();
                    }

                    switch ((SkyboxMode)skyboxMode.Index)
                    {
                        case SkyboxMode.Procedural:
                            RenderSettings.skybox = ProceduralSkybox;

                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Sun Size");
                            ProceduralSkybox.SetFloat("_SunSize", GUILayout.HorizontalSlider(ProceduralSkybox.GetFloat("_SunSize"), 0, 0.5f, GUILayout.Width(120)));

                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Sun Size Convergence");
                            ProceduralSkybox.SetFloat("_SunSizeConvergence", GUILayout.HorizontalSlider(ProceduralSkybox.GetFloat("_SunSizeConvergence"), 1, 10, GUILayout.Width(120)));

                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Atmosphere Thickness");
                            float oldThickness = ProceduralSkybox.GetFloat("_AtmosphereThickness");
                            float newThickness = GUILayout.HorizontalSlider(oldThickness, 0, 5, GUILayout.Width(120));
                            if (oldThickness != newThickness)
                            {
                                MarkAsModified();
                                ProceduralSkybox.SetFloat("_AtmosphereThickness", newThickness);
                                baker.UpdateEnvironment();
                            }

                            GUILayout.EndHorizontal();

                            GUILayout.Label("Sky Tint");
                            ColorButton(ProceduralSkybox.GetColor("_SkyTint"), delegate (Color c) { ProceduralSkybox.SetColor("_SkyTint", c); baker.UpdateEnvironment(); });


                            GUILayout.Label("Ground Color");
                            ColorButton(ProceduralSkybox.GetColor("_GroundColor"), delegate (Color c) { ProceduralSkybox.SetColor("_GroundColor", c); baker.UpdateEnvironment(); });

                            GUILayout.Label("Exposure");
                            float oldExposure = ProceduralSkybox.GetFloat("_Exposure");
                            float newExposure = GUILayout.HorizontalSlider(oldExposure, 0, 5);
                            if (oldExposure != newExposure)
                            {
                                MarkAsModified();
                                ProceduralSkybox.SetFloat("_Exposure", newExposure);
                                baker.UpdateEnvironment();
                            }
                            break;
                        case SkyboxMode.SixSided:
                            RenderSettings.skybox = SixSidedSkybox;

                            for (int i = 0; i < 6; i++)
                            {
                                string direction = ordinalDirecitons[i];
                                string shaderKey = "_" + direction + "Tex";

                                if (i == 0) GUILayout.BeginHorizontal();
                                else if (i % 2 == 0)
                                {
                                    GUILayout.EndHorizontal();
                                    GUILayout.BeginHorizontal();
                                }

                                GUILayout.BeginVertical();

                                // texture label and button
                                GUILayout.Label(direction);
                                if (GUILayout.Button(SixSidedSkybox.GetTexture(shaderKey), GUILayout.Width(100), GUILayout.Height(100)))
                                {
                                    tex_browser_enabled = true;
                                    MaterialManager.SelectedTexture = (Texture2D)SixSidedSkybox.GetTexture(shaderKey);
                                    MaterialManager.UpdateSelectedTexture = delegate (Texture2D tex) { SixSidedSkybox.SetTexture(shaderKey, tex); baker.UpdateEnvironment(); };
                                }

                                GUILayout.EndVertical();
                            }
                            GUILayout.EndHorizontal();

                            GUILayout.Label("Exposure");
                            oldExposure = SixSidedSkybox.GetFloat("_Exposure");
                            newExposure = GUILayout.HorizontalSlider(oldExposure, 0, 5);
                            if (oldExposure != newExposure)
                            {
                                MarkAsModified();
                                SixSidedSkybox.SetFloat("_Exposure", newExposure);
                                baker.UpdateEnvironment();
                            }

                            GUILayout.Label("Rotation");
                            float oldRotation = SixSidedSkybox.GetFloat("_Rotation");
                            float newRotation = GUILayout.HorizontalSlider(oldRotation, 0, 360);
                            if (oldRotation != newRotation)
                            {
                                MarkAsModified();
                                SixSidedSkybox.SetFloat("_Rotation", newRotation);
                                baker.UpdateEnvironment();
                            }

                            break;

                        case SkyboxMode.Default:
                            RenderSettings.skybox = Main.defaultSkybox;
                            break;
                    }
                    GUILayout.EndVertical();

                    skyboxMode.Draw(skyboxModeRect);
                }, "Skybox Editor");
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
            
            if (!Input.GetMouseButton(RightMouseButton) && Input.GetMouseButtonDown(MiddleMouseButton))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 1000, ~gizmoLayerMask))
                {
                    // get kme object
                    var go = hit.collider.gameObject;
                    while (go.GetComponent<KME_Object>() == null && go.transform.parent != null)
                        go = go.transform.parent.gameObject;
                    foreach(var obj in globalObject.AllEditorObjects())
                    {
                        if(obj.go == go) // .collider returns child gameobject, .transform returns parent gameobject (so weird, right?)
                        {
                            SelectedObject.SelectObject(obj);
                        }
                    }
                }
                else
                {
                    SelectedObject.Deselect();
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

            // update clickable gizmo
            if (SelectedObject.Selected && !(SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && SelectedObject.Group.isGlobal))
            {
                clickGizmo.SetActive(true);

                if (!holdingGizmo)
                {
                    // transform the gizmo so its on the slected object
                    gizmoPos = SelectedObject.Basic.worldPos;
                    clickGizmo.transform.position = gizmoPos;
                    if (gizmoMode == GizmoMode.Translate) { clickGizmo.transform.rotation = Quaternion.identity; }
                    else { clickGizmo.transform.rotation = SelectedObject.Basic.transformRotation; }
                    // switch to the correct mode
                    if (Input.GetKey(scaleGizmoKey)) { gizmoMode = GizmoMode.Scale; }
                    else if (Input.GetKey(rotateGizmoKey)) { gizmoMode = GizmoMode.Rotate; }
                    else { gizmoMode = GizmoMode.Translate; }
                }

                // scale up the gizmo so it stays the same size even when the camera gets further away
                float zDistance = Vector3.Dot(gizmoPos - Camera.main.transform.position, Camera.main.transform.forward);
                float gizmoScale = clickGizmoScaleFactor * zDistance * Mathf.Tan(0.5f * Mathf.Deg2Rad * Camera.main.fieldOfView);
                clickGizmo.transform.localScale = Vector3.one * gizmoScale;

                // check for hovering gizmo
                Ray ray = gizmoCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                onGizmo = Physics.Raycast(ray, out hit, float.PositiveInfinity, gizmoLayerMask) && !Input.GetMouseButton(RightMouseButton); // cant manipulate the gizmo while panning and moving

                GameObject hitObject = null;
                if (onGizmo) { hitObject = hit.transform.gameObject; }

                // set the colors of the axis
                GizmoXAxis.GetComponent<Renderer>().material.SetColor("_Color", onGizmo && (hitObject == GizmoXAxis) ? xHighlightColor : xColor);
                GizmoYAxis.GetComponent<Renderer>().material.SetColor("_Color", onGizmo && (hitObject == GizmoYAxis) ? yHighlightColor : yColor);
                GizmoZAxis.GetComponent<Renderer>().material.SetColor("_Color", onGizmo && (hitObject == GizmoZAxis) ? zHighlightColor : zColor);

                // gizmo controller
                if (Input.GetMouseButtonDown(LeftMouseButton) && onGizmo)
                {
                    // start holding
                    holdingGizmo = true;
                    SelectedObject.Basic.PreEdit();
                    
                    if (hit.transform.gameObject == GizmoXAxis)
                    {
                        gizmoMoveDirection = new Vector3(1, 0, 0);
                        GizmoYAxis.SetActive(false);
                        GizmoZAxis.SetActive(false);
                    }
                    else if (hit.transform.gameObject == GizmoYAxis)
                    {
                        gizmoMoveDirection = new Vector3(0, 1, 0);
                        GizmoXAxis.SetActive(false);
                        GizmoZAxis.SetActive(false);
                    }
                    else if (hit.transform.gameObject == GizmoZAxis)
                    {
                        gizmoMoveDirection = new Vector3(0, 0, 1);
                        GizmoXAxis.SetActive(false);
                        GizmoYAxis.SetActive(false);
                    }

                    if (gizmoMode == GizmoMode.Rotate)
                    {
                        initialGizmoPoint = hit.point;
                    }
                    else { initialGizmoPoint = hit.point; }

                    MarkAsModified();
                }
                if (Input.GetMouseButton(LeftMouseButton) && holdingGizmo)
                {
                    // find the true direction of the gizmo
                    Vector3 gizmoDir = clickGizmo.transform.rotation * gizmoMoveDirection;

                    // while holding
                    if (gizmoMode == GizmoMode.Rotate)
                    {
                        // find the intersection of the ray and the plane defined by the axis normal
                        float denom = Vector3.Dot(gizmoDir, ray.direction);
                        float dist = Vector3.Dot(gizmoPos - ray.origin, gizmoDir) / denom;
                        Vector3 intersect = ray.origin + (ray.direction * dist);

                        // find the angle of the intersection on the plane
                        float offset = Vector3.SignedAngle(initialGizmoPoint - gizmoPos, intersect - gizmoPos, gizmoDir);

                        SelectedObject.Basic.RotateByGizmo(Quaternion.AngleAxis(offset, gizmoMoveDirection));
                    }
                    else
                    {
                        // find the plane that contains the desired axis and is facing towards the camera
                        Vector3 camPlaneNormal = Vector3.ProjectOnPlane(Camera.main.transform.forward, gizmoDir);

                        // find where the ray intersects this plane
                        float denom = Vector3.Dot(camPlaneNormal, ray.direction);
                        float dist = Vector3.Dot(clickGizmo.transform.position - ray.origin, camPlaneNormal) / denom;
                        Vector3 intersect = ray.origin + (ray.direction * dist);

                        // find the offset along the move direction that is closest to the intersection
                        float offset = Vector3.Dot(intersect - initialGizmoPoint, gizmoDir);

                        if (gizmoMode == GizmoMode.Translate)
                        {
                            // move the object by the relative difference in the current and last recorded offset
                            SelectedObject.Basic.MoveByGizmo(gizmoMoveDirection * offset);
                            clickGizmo.transform.position = gizmoPos + gizmoMoveDirection * offset;
                        }
                        else
                        {
                            // scale the object by the relative difference in the current and last recorded offset
                            SelectedObject.Basic.ScaleByGizmo(gizmoMoveDirection * offset);
                        }
                    }
                }
                if (Input.GetMouseButtonUp(LeftMouseButton) && holdingGizmo)
                {
                    // stop holding
                    holdingGizmo = false;

                    GizmoXAxis.SetActive(true);
                    GizmoYAxis.SetActive(true);
                    GizmoZAxis.SetActive(true);
                }
                
            }
            else
            {
                clickGizmo.SetActive(false);
            }

            EditorObject spawnObject = globalObject.editorObjects.First(x => x.internalObject);
            startPosition = spawnObject.aPosition;
            startOrientation = spawnObject.aRotation.y;
        }

        private static void ExitEditor()
        {
            AudioListener.volume = SaveManager.Instance.state.volume;
            UnityEngine.Object.Destroy(gizmoCamera);
            dd_file = false;
            dd_level = false;
            tex_browser_enabled = false;
            skyboxEditorEnabled = false;
            editorMode = false;
            Game.Instance.MainMenu();
            if(fileWatcher != null)
                Coroutines.StopCoroutine(fileWatcher);
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

        // saver
        private static byte[] SaveLevelData()
        {
            Map map = new Map
            {
                // editor data
                GridAlign = gridAlign,

                // starting state
                StartingGun = startingGun,
                StartPosition = startPosition,
                StartOrientation = startOrientation,

                // level data
                AutomataScript = File.ReadAllText(Path.Combine(Main.directory, "_temp.amta"))
            };
            map.SaveGlobalLight(sun);
            map.SaveTree(globalObject);
            map.SaveMaterials();
            
            // export
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes(LevelSerializer.SaveVersion), 0, 4);
                map.WriteTo(ms);
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
            IEnumerator markAsSaved()
            {
                yield return new WaitForSecondsRealtime(0.1f); // because Time.timeScale is 0 in editor
                MarkAsSaved();
            }
            Coroutines.StartCoroutine(markAsSaved());
        }

        private static void LoadLevel(string path)
        {
            Loadson.Console.Log("setting up level editor");
            ObjectGroup ReplicateObjectGroup(LevelData.ObjectGroup group, ObjectGroup parentGroup)
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
            
            LevelData data = new LevelData(File.ReadAllBytes(path));
            levelName = Path.GetFileNameWithoutExtension(path);

            data.SetupMaterials();
            data.SetupGlobalLight(sun);
            gridAlign = data.gridAlign;
            startingGun = data.startingGun;
            startGunDD.Index = startingGun;
            startPosition = data.startPosition;
            startOrientation = data.startOrientation;

            
            globalObject = ReplicateObjectGroup(data.GlobalObject, null);
            globalObject.isGlobal = true;
            var spawn = new EditorObject(startPosition, startOrientation);
            globalObject.editorObjects.Insert(0, spawn);
            spawn.go.transform.parent = globalObject.go.transform;

            // select the correct skybox
            if (RenderSettings.skybox == Main.defaultSkybox) skyboxMode.Index = (int)SkyboxMode.Default;
            else if (RenderSettings.skybox == ProceduralSkybox) skyboxMode.Index = (int)SkyboxMode.Procedural;
            else if (RenderSettings.skybox == SixSidedSkybox) skyboxMode.Index = (int)SkyboxMode.SixSided;
            // update environmental lighting and reflections
            baker.UpdateEnvironment();

            dd_level = true;
            Time.timeScale = 0f;
            SelectedObject.Deselect();
            IEnumerator markAsSaved()
            {
                yield return new WaitForSecondsRealtime(0.1f); // because Time.timeScale is 0 in editor
                unsaved = false;
            }
            Coroutines.StartCoroutine(markAsSaved());

            // scripting
            File.WriteAllText(Path.Combine(Main.directory, "_temp.amta"), data.AutomataScript.Trim().Length > 0 ? data.AutomataScript : "# Any code written below will be executed every time the level is (re)started.\n# Be sure to use LF instead of CRLF line endings.\n# Any modifications to this file will be reflected by marking the level as modified (unsaved).\n# Saving the level will also save this script.\n# Automata documentation: https://github.com/devilExE3/automataV2/blob/master/automata.md\n# KME API: https://github.com/karlsonmodding/KarlsonMapEditor/wiki/Scripting-API\n$:print(\"Hello, world!\")");
            fileWatcher = Coroutines.StartCoroutine(FileWatcher());
        }

        static IEnumerator FileWatcher()
        {
            DateTime lastMod = new FileInfo(Path.Combine(Main.directory, "_temp.amta")).LastWriteTime;
            while(true)
            {
                yield return new WaitForSecondsRealtime(1f);
                var modNow = new FileInfo(Path.Combine(Main.directory, "_temp.amta")).LastWriteTime;
                if (modNow != lastMod)
                {
                    lastMod = modNow;
                    MarkAsModified();
                }
            }
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
            void PreEdit();
            void MoveByGizmo(Vector3 delta);
            void ScaleByGizmo(Vector3 delta);
            void RotateByGizmo(Quaternion delta);
        }

        public class EditorObject : IBasicProperties
        {
            public EditorObject(Vector3 position, GeometryShape shape = GeometryShape.Cube) : this(new LevelData.LevelObject(position, Vector3.zero, Vector3.one, 6, Color.white, "Geometry Object", false, false, false, false, false, shape)) { }

            public EditorObject(PrefabType _prefabId, Vector3 position)
            {
                go = LevelData.MakePrefab(_prefabId);
                go.AddComponent<KME_Object>();
                go.transform.position = position;
                go.transform.rotation = Quaternion.identity;
                if (go.GetComponent<Rigidbody>() != null) go.GetComponent<Rigidbody>().isKinematic = true;
                data = new LevelData.LevelObject(_prefabId, go.transform.localPosition, go.transform.localRotation.eulerAngles, go.transform.localScale, go.name, 0);
            }

            public EditorObject(LevelData.LevelObject playObj)
            {
                data = playObj;
                if(data.IsPrefab)
                {
                    go = LevelData.MakePrefab(data.PrefabId);
                    if (go.GetComponent<Rigidbody>() != null) go.GetComponent<Rigidbody>().isKinematic = true;
                }
                else
                {
                    go = MeshBuilder.GetGeometryGO(playObj.ShapeId);
                    go.GetComponent<KMETextureScaling>().Scale = playObj.UVNormalizedScale;
                    go.GetComponent<MeshRenderer>().sharedMaterial = MaterialManager.Materials[playObj.MaterialId];
                    if (playObj.Glass)
                        go.AddComponent<Glass>();
                    if (playObj.Lava)
                        go.AddComponent<Lava>();
                }
                go.AddComponent<KME_Object>();
                go.transform.position = data.Position;
                go.transform.rotation = Quaternion.Euler(data.Rotation);
                go.transform.localScale = data.Scale;
                go.name = data.Name;
            }

            // player spawn constructor
            public EditorObject(Vector3 pos, float orientation)
            {
                data = new LevelData.LevelObject
                {
                    Name = "Player Spawn",
                    GroupName = "_internal",
                    Position = pos,
                    Rotation = Vector3.zero,
                    Scale = Vector3.one,
                    IsPrefab = false,
                };
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.AddComponent<KME_Object>();
                go.transform.localScale = new Vector3(1f, 1.5f, 1f);
                go.transform.position = pos;
                go.transform.eulerAngles = new Vector3(0f, orientation, 0f);
                go.name = "Player Spawn";
                go.GetComponent<MeshRenderer>().material.color = Color.yellow;
                internalObject = true;
            }

            public bool internalObject { get; private set; } = false;
            public LevelData.LevelObject data;
            public GameObject go;

            public void Clone(ObjectGroup parent)
            {
                EditorObject toAdd;
                if (data.IsPrefab)
                    toAdd = new EditorObject(data.PrefabId, aPosition);
                else
                {
                    toAdd = new EditorObject(aPosition, data.ShapeId);
                    toAdd.data.MaterialId = data.MaterialId;
                    toAdd.go.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
                    toAdd.go.GetComponent<KMETextureScaling>().Scale = go.GetComponent<KMETextureScaling>().Scale;
                    toAdd.data.Bounce = data.Bounce;
                    toAdd.data.Glass = data.Glass;
                    toAdd.data.Lava = data.Lava;
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
                set {
                    go.transform.localScale = value;
                    data.Scale = value;
                    if (!data.IsPrefab) { go.GetComponent<KMETextureScaling>().UpdateScale(); }
                }
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

            private Vector3 preMove;
            private Vector3 preScale;
            private Quaternion preRotate;
            public void PreEdit()
            {
                preMove = go.transform.position;
                preScale = go.transform.localScale;
                preRotate = go.transform.localRotation;
            }

            public void MoveByGizmo(Vector3 delta)
            {
                go.transform.position = preMove + delta;
                if (gridAlign != 0) { aPosition = Vector3Extensions.SnapPos(aPosition, positionSnap, aRotation); }
                data.Position = go.transform.localPosition;
            }
            public void ScaleByGizmo(Vector3 delta)
            {
                go.transform.localScale = preScale + delta;
                // only snap scale if it's not a prefab
                if (gridAlign != 0 && !data.IsPrefab) { aScale = Vector3Extensions.SnapScale(aScale, scaleSnap); }
                data.Scale = go.transform.localScale;
            }
            public void RotateByGizmo(Quaternion delta)
            {
                go.transform.localRotation = preRotate * delta;
                if (gridAlign != 0) { aRotation = Vector3Extensions.Snap(aRotation, rotationSnap); }
                data.Rotation = go.transform.localRotation.eulerAngles;
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

            public (int,int) CountAll()
            {
                int objg = 1;
                int levelobjs = editorObjects.Count;
                foreach (var g in objectGroups)
                {
                    var countResult = g.CountAll();
                    objg += countResult.Item1;
                    levelobjs += countResult.Item2;
                }
                return (objg, levelobjs);
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
                    ret.AddRange(g.AllEditorObjects());
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

            private Vector3 preMove;
            private Vector3 preScale;
            private Quaternion preRotate;
            public void PreEdit()
            {
                preMove = go.transform.position;
                preScale = go.transform.localScale;
                preRotate = go.transform.localRotation;
            }

            public void MoveByGizmo(Vector3 delta)
            {
                go.transform.position = preMove + delta;
                if (gridAlign != 0) { aPosition = Vector3Extensions.SnapPos(aPosition, positionSnap, aRotation); }
            }
            public void ScaleByGizmo(Vector3 delta)
            {
                go.transform.localScale = preScale + delta;
                if (gridAlign != 0) { aScale = Vector3Extensions.SnapScale(aScale, scaleSnap); }
            }
            public void RotateByGizmo(Quaternion delta)
            {
                go.transform.localRotation = preRotate * delta;
                if (gridAlign != 0) { aRotation = Vector3Extensions.Snap(aRotation, rotationSnap); }
            }
        }

        public static class MaterialManager
        {
            public static List<Texture2D> Textures { get { return textures; } }
            private static List<Texture2D> textures = new List<Texture2D>();
            public static List<Material> Materials { get { return materials; } }
            private static List<Material> materials = new List<Material>();

            public static Texture2D SelectedTexture; // for choosing textures in a context menu
            public static Action<Texture2D> UpdateSelectedTexture;
            
            public static Shader defaultShader;

            public enum ShaderBlendMode
            {
                Opaque,
                Cutout,
                Fade,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
                Transparent // Physically plausible transparency mode, implemented as alpha pre-multiply
            };

            public static void InitInternalTextures()
            {
                Clear();
                foreach (Texture2D tex in Main.gameTex)
                {
                    AddTexture(tex);
                }
            }

            public static void Clear()
            {
                textures.Clear();
                materials.Clear();
            }

            public static void AddTexture(Texture2D tex)
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                textures.Add(tex);
            }

            public static bool[] TexturesInUse()
            {
                bool[] used = new bool[textures.Count];
                for (int i = 0; i < Main.gameTex.Length; i++)
                {
                    used[i] = true;
                }
                foreach (Material mat in materials)
                {
                    if (mat.mainTexture)
                        used[textures.IndexOf((Texture2D)mat.mainTexture)] = true;
                    if (mat.GetTexture("_BumpMap"))
                        used[textures.IndexOf((Texture2D)mat.GetTexture("_BumpMap"))] = true;
                    if (mat.GetTexture("_MetallicGlossMap"))
                        used[textures.IndexOf((Texture2D)mat.GetTexture("_MetallicGlossMap"))] = true;
                }
                foreach (string direction in ordinalDirecitons)
                {
                    Texture t = SixSidedSkybox.GetTexture("_" + direction + "Tex");
                    if (t)
                        used[textures.IndexOf((Texture2D)t)] = true;
                }
                return used;
            }
            public static void RemoveTexture(Texture2D tex)
            {
                textures.Remove(tex);
            }

            public static Material InstanceMaterial()
            {
                Material mat = new Material(defaultShader)
                {
                    mainTexture = textures[6]
                };
                materials.Add(mat);
                return mat;
            }
            // instancing materials for save versions without material data
            public static int InstanceMaterial(int TextureId, Color color, bool transparent)
            {
                Material mat = new Material(defaultShader)
                {
                    mainTexture = textures[TextureId],
                    color = color
                };
                if (transparent)
                {
                    UpdateMode(mat, ShaderBlendMode.Transparent);
                    // values used glass in the prefab
                    mat.SetFloat("_Metallic", 0.171f);
                    mat.SetFloat("_Glossiness", 0.453f);
                }

                materials.Add(mat);
                return materials.Count - 1;
            }
            public static void ClearMaterial(Material mat)
            {
                int index = materials.IndexOf(mat);
                List<EditorObject> allObjects = globalObject.AllEditorObjects();

                // check if the mat is used by any object
                foreach (EditorObject eo in allObjects)
                {
                    if (eo.data.MaterialId == index) return;
                }

                // if not, remove the mat
                materials.RemoveAt(index);
                foreach (EditorObject eo in allObjects)
                    if (eo.data.MaterialId > index) eo.data.MaterialId--;
            }
            public static int GetMainTextureIndex(int materialId)
            {
                return textures.IndexOf((Texture2D)materials[materialId].mainTexture);
            }
            public static List<Texture2D> GetExternalTextures()
            {
                return textures.Skip(Main.gameTex.Length).ToList();
            }

            public static void UpdateMode(Material mat, ShaderBlendMode mode)
            {
                mat.SetFloat("_Mode", (int)mode);
                switch (mode)
                {
                    case ShaderBlendMode.Opaque:
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetInt("_ZWrite", 1);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = -1;
                        break;
                    case ShaderBlendMode.Cutout:
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetInt("_ZWrite", 1);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                        break;
                    case ShaderBlendMode.Fade:
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        break;
                    case ShaderBlendMode.Transparent:
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        break;
                }
            }
        }
    }

    public class KME_Object : MonoBehaviour { } // mark object as being kme, to be able to select any object with middle click
}
