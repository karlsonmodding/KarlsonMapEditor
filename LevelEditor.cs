using LoadsonAPI;
using SevenZip.Compression.LZMA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Google.Protobuf;
using System.Buffers;
using TMPro;
using KarlsonMapEditor.LevelLoader;
using System.Threading;

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

        private static readonly GUIStyle noSpace = new GUIStyle() { border = new RectOffset(0, 0, 0, 0), normal = new GUIStyleState() { textColor = Color.white } };
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
                Mesh mesh = GizmoMeshBuilder.AxisMeshes[(int)value];
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
        const float scaleSnapExp = 1.148698354997035f;
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
                UnityEngine.Object.Destroy(l.gameObject);

            PlayerMovement.Instance.gameObject.GetComponent<Rigidbody>().isKinematic = false;
            PlayerMovement.Instance.gameObject.GetComponent<Rigidbody>().useGravity = false; //eh
            PlayerMovement.Instance.gameObject.GetComponent<Collider>().enabled = false;

            // lighting and reflections
            baker = new GameObject().AddComponent<EnvironmentBaker>();
            sun = new GameObject().AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.enabled = false;

            // create clickable gizmo
            clickGizmo = new GameObject();
            clickGizmo.layer = gizmoLayer;
            clickGizmo.SetActive(false);

            // create the axis for the gizmo
            GizmoXAxis = GizmoMeshBuilder.GetAxisGO(GizmoMode.Translate);
            GizmoXAxis.transform.parent = clickGizmo.transform;
            GizmoXAxis.transform.rotation = Quaternion.Euler(0, 90, 0);
            GizmoXAxis.layer = gizmoLayer;
            
            GizmoYAxis = GizmoMeshBuilder.GetAxisGO(GizmoMode.Translate);
            GizmoYAxis.transform.parent = clickGizmo.transform;
            GizmoYAxis.transform.rotation = Quaternion.Euler(-90, 0, 0);
            GizmoYAxis.layer = gizmoLayer;

            GizmoZAxis = GizmoMeshBuilder.GetAxisGO(GizmoMode.Translate);
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
                None,
                EditorObject,
                ObjectGroup,
            };
            public static SelectedType Type = SelectedType.None;

            public static ObjectGroup Group { get; private set; }
            public static EditorObject Object { get; private set; }
            public static IBasicProperties Basic => Type == SelectedType.ObjectGroup ? (IBasicProperties)Group : Object;

            public static void SelectObject(EditorObject obj)
            {
                Object = obj;
                Type = SelectedType.EditorObject;

                Identify();

                // select parent group to be able to add objects easier
                Group = globalObject.FindParentOfChild(Object);
            }
            public static void SelectGroup(ObjectGroup group)
            {
                Group = group;
                Type = SelectedType.ObjectGroup;
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
                Object = null;
                Group = null;
                Type = SelectedType.None;
            }

            public static bool DoFullRotation => Type != SelectedType.EditorObject || Object.data.Type != ObjectType.Internal;
            public static bool DoScale => Type != SelectedType.EditorObject || Object.data.Type != ObjectType.Internal && Object.data.Type != ObjectType.Light;
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
        private static readonly Dictionary<string, string> textFieldsTexts = new Dictionary<string, string>();
        private static readonly Dictionary<string, float> textFieldsValues = new Dictionary<string, float>();
        private static void FloatField(string key, ref float source, int width = 50, int height = 20)
        {
            if ((!textFieldsValues.ContainsKey(key)) || textFieldsValues[key] != source)
            {
                textFieldsValues[key] = source;
                textFieldsTexts[key] = source.ToString();
            }
            string oldText = textFieldsTexts[key];
            string newText = GUILayout.TextField(oldText, GUILayout.Width(width), GUILayout.Height(height));
            if (oldText != newText)
            {
                textFieldsTexts[key] = newText;
                if (float.TryParse(newText, out float newValue) && newValue != source)
                {
                    textFieldsValues[key] = newValue;
                    source = newValue;
                    MarkAsModified();
                }
            }
        }
        private static void FloatField(string key, float source, Action<float> updateSource, int width = 50, int height = 20)
        {
            if (!textFieldsValues.ContainsKey(key) || textFieldsValues[key] != source)
            {
                textFieldsValues[key] = source;
                textFieldsTexts[key] = source.ToString();
            }
            string oldText = textFieldsTexts[key];
            string newText = GUILayout.TextField(oldText, GUILayout.Width(width), GUILayout.Height(height));
            if (oldText != newText)
            {
                textFieldsTexts[key] = newText;
                if (float.TryParse(newText, out float newValue) && newValue != source)
                {
                    textFieldsValues[key] = newValue;
                    updateSource(newValue);
                    MarkAsModified();
                }
            }
        }

        static Texture2D blackTx, greenTx;
        static bool guiCtor = false;
        public static void _ongui()
        {
            if (!editorMode) return;

            if(!guiCtor)
            {
                guiCtor = true;
                blackTx = new Texture2D(1, 1);
                blackTx.SetPixel(0, 0, Color.black);
                blackTx.Apply();
                greenTx = new Texture2D(1, 1);
                greenTx.SetPixel(0, 0, new Color(0, 1, 0));
                greenTx.Apply();
            }

            // check for windows out of bound (if the user right clicks them, they glitch
            // out because also right-clicking enables camera move, which locks cursor)

            foreach (WindowId windowId in Enum.GetValues(typeof(WindowId)))
            {
                if (wir[(int)windowId].x < 0)
                    wir[(int)windowId].x = 0;
                if (wir[(int)windowId].y < 0)
                    wir[(int)windowId].y = 0;
                if (wir[(int)windowId].x + wir[(int)windowId].width > Screen.width)
                    wir[(int)windowId].x = Screen.width - wir[(int)windowId].width;
                if (wir[(int)windowId].y + wir[(int)windowId].height > Screen.height)
                    wir[(int)windowId].y = Screen.height - wir[(int)windowId].height;
            }

            if (dg_screenshot)
            {
                GUI.Box(new Rect(Screen.width / 2 - 200, 10, 400, 25), "Point your camera to set the Thumbnail and press <b>ENTER</b>");
                return;
            }

            if (dg_enabled) wir[(int)WindowId.Prompt] = GUI.ModalWindow(wid[(int)WindowId.Prompt], wir[(int)WindowId.Prompt], (windowId) => {
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

                                File.WriteAllText(Path.Combine(Main.directory, "_temp.amta"), "# Any code written below will be executed every time the level is (re)started.\n# Be sure to use LF instead of CRLF line endings.\n# Any modifications to this file will be reflected by marking the level as modified (unsaved).\n# Saving the level will also save this script.\n# Automata documentation: https://github.com/devilExE3/automataV2/blob/master/automata.md\n# KME API: https://github.com/karlsonmodding/KarlsonMapEditor/wiki/Scripting-API\n#$:print(\"Hello, world!\")");
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
            GUI.Label(new Rect(305, 0, 1000, 20), $"<b>Karlson Map Editor</b> v{Main.Version} | Current map: <b>{(unsaved ? (levelName + '*') : levelName)}</b> | Object count: <b>{_countAll.Item1 + _countAll.Item2}</b> | Hold <b>right click</b> down to move and look around | Select an object by <b>middle clicking</b> it");
            if (GUI.Button(new Rect(Screen.width - 100, 0, 100, 20), "Open Script")) Process.Start(Path.Combine(Main.directory, "_temp.amta"));

            if (dd_file)
            {
                GUI.Box(new Rect(0, 20, 150, 100), "");
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
                if (ModIO.Auth.ModioBearer != "" && GUI.Button(new Rect(0, 60, 150, 20), "Upload to mod.io")) dg_screenshot = true;
                if (ModIO.Auth.ModioBearer == "") GUIex.DisabledButton(new Rect(0, 60, 150, 20), "Not logged in");
                if (GUI.Button(new Rect(0, 80, 150, 20), "KMP Export"))
                {
                    // kmp now uses the LevelLoader (which i split KME into that specifically for this reason)
                    // but we still want to track '!KMP' objects
                    Directory.CreateDirectory(Path.Combine(Main.directory, "KMP_Export"));
                    File.WriteAllBytes(Path.Combine(Main.directory, "KMP_Export", levelName + ".kme"), SaveLevelData(true));

                    Loadson.Console.Log("Writing kmp data");

                    Dictionary<string, List<(string, Vector3, Vector3, Vector3)>> kmp_data = new Dictionary<string, List<(string, Vector3, Vector3, Vector3)>>();
                    void dfs(ObjectGroup group)
                    {
                        foreach(var obj in group.editorObjects)
                        {
                            if (obj.go.name.StartsWith("!KMP"))
                            {
                                string key = obj.go.name.Split('.')[1];
                                string value = obj.go.name.Split('.')[2];
                                if (!kmp_data.ContainsKey(key))
                                    kmp_data.Add(key, new List<(string, Vector3, Vector3, Vector3)>());
                                kmp_data[key].Add((value, obj.go.transform.position, obj.go.transform.rotation.eulerAngles, obj.go.transform.lossyScale));
                            }
                        }
                        foreach (var g in group.objectGroups)
                            dfs(g);
                    }
                    dfs(globalObject);

                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write(kmp_data.Count);
                        foreach (var x in kmp_data)
                        {
                            bw.Write(x.Key);
                            bw.Write(x.Value.Count);
                            foreach (var val in x.Value)
                            {
                                bw.Write(val.Item1);
                                bw.Write(val.Item2);
                                bw.Write(val.Item3);
                                bw.Write(val.Item4);
                            }
                        }
                        bw.Flush();
                        File.WriteAllBytes(Path.Combine(Main.directory, "KMP_Export", levelName + ".kme_data"), ms.ToArray());
                    }

                    Process.Start(Path.Combine(Main.directory, "KMP_Export"));
                    dd_file = false;
                }
                if (GUI.Button(new Rect(0, 100, 150, 20), "Exit Editor"))
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
                GUI.Box(new Rect(100, 20, SelectedObject.Type != SelectedObject.SelectedType.None ? 600 : 300, 20), "");
                if (SelectedObject.Type == SelectedObject.SelectedType.None)
                {
                    GUI.Label(new Rect(105, 20, 300, 20), "Select an Object / Object Group to spawn objects");
                }
                else
                {
                    spawnPrefabDD.Draw(new Rect(100, 20, 150, 20));

                    // local spawn Position
                    Vector3 spawnPos = SelectedObject.Group.go.transform.worldToLocalMatrix.MultiplyPoint3x4(PlayerMovement.Instance.gameObject.transform.position);
                    if (gridAlign != 0) { spawnPos = Vector3Extensions.Snap(spawnPos, positionSnap); }
                    if (spawnPrefabDD.Index != 0)
                    {
                        if ((PrefabType)(spawnPrefabDD.Index - 1) == PrefabType.Milk)
                        {
                            ObjectGroup container = new ObjectGroup("Prefab Container");
                            SelectedObject.Group.AddGroup(container);
                            new EditorObject(Vector3.zero, container, PrefabType.Milk);
                            container.aPosition = spawnPos;
                            SelectedObject.SelectGroup(container);
                        }
                        else
                        {
                            SelectedObject.SelectObject(new EditorObject(spawnPos, SelectedObject.Group, (PrefabType)(spawnPrefabDD.Index - 1)));
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
                        SelectedObject.SelectObject(new EditorObject(spawnPos, SelectedObject.Group, shape));
                    }

                    if (GUI.Button(new Rect(400, 20, 150, 20), "Spawn Light"))
                    {
                        LevelData.LevelObject data = new LevelData.LevelObject()
                        {
                            Type = ObjectType.Light,
                            LightType = LightType.Point,
                            Intensity = 1,
                            Range = 10,
                            SpotAngle = 30,
                            Color = Color.white,
                            Name = "Light Object",
                            Position = spawnPos,
                            Rotation = new Vector3(90, 0, 0),
                        };
                        SelectedObject.SelectObject(new EditorObject(data, SelectedObject.Group));
                    }

                    if (GUI.Button(new Rect(550, 20, 150, 20), "Spawn Text Display"))
                    {
                        LevelData.LevelObject data = new LevelData.LevelObject()
                        {
                            Type = ObjectType.Text,
                            Text = "Text",
                            Color = Color.black,
                            Name = "Text Display Object",
                            Position = spawnPos,
                        };

                        SelectedObject.SelectObject(new EditorObject(data, SelectedObject.Group));
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
                    FilePicker.PickFile("Select the texture you wish to import", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), new List<(string, string)> { ("Images", "*.png|*.jpg|*.jpeg|*.bmp"), ("All Files", "*.*") }, (path)=> {
                        if(File.Exists(path))
                        {
                            Texture2D tex = new Texture2D(0, 0);
                            tex.LoadImage(File.ReadAllBytes(path));
                            tex.name = Path.GetFileName(path);
                            MaterialManager.AddTexture(tex);

                            MarkAsModified();
                        }
                    }, () => { });
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
                        color = "<color=#00FF00>^</color>";
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
                GUI.DragWindow();
            }, "Texture Browser");

            wir[(int)WindowId.LevelBrowser] = GUI.Window(wid[(int)WindowId.LevelBrowser], wir[(int)WindowId.LevelBrowser], (windowId) => {

                float renderObjectGroup(ObjectGroup group, float j, int depth)
                {
                    // render this group name and controls
                    bool isGroupSelected = SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && SelectedObject.Group.go == group.go;
                    if (GUI.Button(new Rect(depth - 5, j * 20, 285 - depth, 20), isGroupSelected ? ("<color=#00FF00>" + group.go.name + "</color>") : group.go.name, GUIex.Dropdown.dropdownButton))
                        SelectedObject.SelectGroup(group);
                    j += 1;
                    float firstj = j;
                    // render child groups
                    foreach (var g in group.objectGroups) {
                        j = renderObjectGroup(g, j, depth + 6);
                    }
                    // render child editor objects
                    foreach (var obj in group.editorObjects)
                    {
                        bool isSelected = SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.go == obj.go;
                        string label;
                        switch (obj.data.Type)
                        {
                            case ObjectType.Prefab:
                                label = obj.data.PrefabId.ToString() + " | " + obj.go.name;
                                break;
                            case ObjectType.Geometry:
                                label = obj.data.ShapeId.ToString() + " | " + obj.go.name;
                                break;
                            case ObjectType.Light:
                                label = "Light | " + obj.go.name;
                                break;
                            case ObjectType.Text:
                                label = "Text | " + obj.go.name;
                                break;
                            case ObjectType.Internal:
                                label = "Internal | " + obj.go.name;
                                break;
                            default:
                                label = "Unknown | " + obj.go.name;
                                break;
                        }
                        if(GUI.Button(new Rect(depth + 1, j * 20, 279 - depth, 20), isSelected ? ("<color=#00FF00>" + label + "</color>") : label, GUIex.Dropdown.dropdownButton))
                            SelectedObject.SelectObject(obj);
                        j += 1;
                    }
                    // close group
                    GUI.DrawTexture(new Rect(depth + 2, firstj * 20 - 2, 2, (j - firstj) * 20 + 2), isGroupSelected ? greenTx : blackTx);
                    GUI.DrawTexture(new Rect(depth + 2, j * 20, 280 - (depth + 2), 2), isGroupSelected ? greenTx : blackTx);
                    j += 0.2f;
                    return j;
                }

                if (SelectedObject.Type != SelectedObject.SelectedType.None)
                    if (GUI.Button(new Rect(280, 0, 20, 20), "+"))
                    {
                        Vector3 spawnPos = SelectedObject.Group.go.transform.worldToLocalMatrix.MultiplyPoint3x4(PlayerMovement.Instance.gameObject.transform.position);
                        if (gridAlign != 0) { spawnPos = Vector3Extensions.Snap(spawnPos, positionSnap); }

                        MarkAsModified();
                        ObjectGroup g = new ObjectGroup();
                        SelectedObject.Group.AddGroup(g);
                        g.aPosition = spawnPos;
                        SelectedObject.SelectGroup(SelectedObject.Group.objectGroups.Last());
                        return;
                    }

                object_browser_scroll = GUI.BeginScrollView(new Rect(0, 20, 300, 455), object_browser_scroll, new Rect(0, 0, 280, _countAll.Item1 * 24 + _countAll.Item2 * 20));

                renderObjectGroup(globalObject, 0, 5);
                
                GUI.EndScrollView();
                GUI.DragWindow();
            }, "Map Object Browser");

            wir[(int)WindowId.ObjectManip] = GUI.Window(wid[(int)WindowId.ObjectManip], wir[(int)WindowId.ObjectManip], (windowId) => {
                GUI.DragWindow(new Rect(0, 0, 1000, 20));
                if (SelectedObject.Type == SelectedObject.SelectedType.None)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "No object selected");
                    return;
                }
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && SelectedObject.Group.isGlobal)
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "You can't modify the global object.");
                    GUI.Label(new Rect(5, 35, 300, 40), "You can only add objects (Prefabs / Cube) and groups (+ in the top right)");
                    return;
                }
                string newName = GUI.TextField(new Rect(5f, 20f, 290f, 20f), SelectedObject.Basic.aName);
                if (newName != SelectedObject.Basic.aName)
                {
                    MarkAsModified();
                    SelectedObject.Basic.aName = newName;
                }

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && (SelectedObject.Object.data.Type == ObjectType.Internal))
                    GUI.Label(new Rect(5, 40, 290, 60), "This is an internal object.\nProperties are limited.");
                if (SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup || SelectedObject.Type == SelectedObject.SelectedType.EditorObject && (SelectedObject.Object.data.Type != ObjectType.Internal))
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

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && SelectedObject.Object.data.PrefabId == PrefabType.Enemy)
                {
                    GUI.Label(new Rect(5, 60, 40, 20), "Gun");
                    enemyGun.Index = SelectedObject.Object.data.PrefabData;
                    enemyGun.Draw(new Rect(35, 60, 100, 20));
                    if (SelectedObject.Object.data.PrefabData != enemyGun.Index)
                    {
                        MarkAsModified();
                        SelectedObject.Object.data.PrefabData = enemyGun.Index;
                        SelectedObject.Object.data.setGun(SelectedObject.Object.go);
                    }
                }
                
                GUILayout.BeginArea(new Rect(5, 100, 300, 80));
                GUILayout.BeginVertical(noSpace);

                float x, y, z;

                GUILayout.BeginHorizontal(noSpace, GUILayout.Height(20));
                GUILayout.Label("Pos:", noSpace, GUILayout.Width(80));
                x = SelectedObject.Basic.aPosition.x;
                y = SelectedObject.Basic.aPosition.y;
                z = SelectedObject.Basic.aPosition.z;
                FloatField("PosX", ref x);
                FloatField("PosY", ref y);
                FloatField("PosZ", ref z);
                SelectedObject.Basic.aPosition = new Vector3(x, y, z);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(noSpace, GUILayout.Height(20));
                GUILayout.Label("Rot:", noSpace, GUILayout.Width(80));
                x = SelectedObject.Basic.aRotation.x;
                y = SelectedObject.Basic.aRotation.y;
                z = SelectedObject.Basic.aRotation.z;
                if (SelectedObject.DoFullRotation) FloatField("RotX", ref x);
                FloatField("RotY", ref y);
                if (SelectedObject.DoFullRotation) FloatField("RotZ", ref z);
                SelectedObject.Basic.aRotation = new Vector3(x, y, z);
                GUILayout.EndHorizontal();

                if (SelectedObject.DoScale)
                {
                    GUILayout.BeginHorizontal(noSpace, GUILayout.Height(20));
                    GUILayout.Label("Scale:", noSpace, GUILayout.Width(80));
                    x = SelectedObject.Basic.aScale.x;
                    y = SelectedObject.Basic.aScale.y;
                    z = SelectedObject.Basic.aScale.z;
                    FloatField("ScaleX", ref x);
                    FloatField("ScaleY", ref y);
                    FloatField("ScaleZ", ref z);
                    SelectedObject.Basic.aScale = new Vector3(x, y, z);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.EndArea();
                
                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject)
                {
                    EditorObject selected = SelectedObject.Object;
                    if (selected.data.Type == ObjectType.Geometry)
                    {
                        GUILayout.BeginArea(new Rect(5, 165, 300, 400));

                        GUILayout.BeginHorizontal();

                        KMETextureScaling ts = selected.go.GetComponent<KMETextureScaling>();

                        ts.Enabled = GUILayout.Toggle(ts.Enabled, "UV Normalize", GUILayout.Width(150));
                        if (ts.Enabled)
                        {
                            FloatField("UVNormalizedScale", ts.Scale, delegate (float v) { ts.Scale = v; selected.data.UVNormalizedScale = ts.Scale; }, 100);
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginVertical(noSpace);

                        GUILayout.Label("Material", noSpace);

                        // material label and buttons
                        GUILayout.BeginHorizontal(noSpace);

                        if (GUILayout.Button("New", GUILayout.Width(50)))
                        {
                            MarkAsModified();
                            selected.data.MaterialId = MaterialManager.Materials.Count();
                            selected.go.GetComponent<MeshRenderer>().sharedMaterial = MaterialManager.InstanceMaterial();
                        }
                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                        {
                            clipboardObj = selected;
                        }
                        if (clipboardObj != null)
                        {
                            if (GUILayout.Button("Paste"))
                            {
                                MarkAsModified();
                                selected.go.GetComponent<MeshRenderer>().sharedMaterial.CopyPropertiesFromMaterial(clipboardObj.go.GetComponent<MeshRenderer>().sharedMaterial);
                            }
                            if (GUILayout.Button("Reference"))
                            {
                                MarkAsModified();
                                ClearMaterial(selected.go.GetComponent<MeshRenderer>().sharedMaterial);
                                selected.go.GetComponent<MeshRenderer>().sharedMaterial = clipboardObj.go.GetComponent<MeshRenderer>().sharedMaterial;
                                selected.data.MaterialId = clipboardObj.data.MaterialId;
                            }
                        }
                        Material selectedMat = selected.go.GetComponent<MeshRenderer>().sharedMaterial;

                        GUILayout.EndHorizontal();

                        // material properties
                        GUILayout.BeginHorizontal(noSpace);

                        // color and mat sliders
                        GUILayout.BeginVertical(noSpace);

                        GUILayout.BeginHorizontal(noSpace);
                        GUILayout.Label("Color", noSpace);
                        ColorButton(selectedMat.color, delegate (Color c) { selectedMat.color = c; }, 140);
                        GUILayout.EndHorizontal();

                        /* maybe later
                        GUILayout.BeginHorizontal(noSpace);
                        GUILayout.Label("Emission", noSpace);
                        ColorButton(selectedMat.GetColor("_EmissionColor"), delegate (Color c) { selectedMat.SetColor("_EmissionColor", c); });
                        GUILayout.EndHorizontal();
                        */

                        GUILayout.Space(4);

                        int lastMode = (int)selectedMat.GetFloat("_Mode");
                        materialMode.Index = lastMode;
                        Rect matModeRect = GUILayoutUtility.GetRect(120, 20, noSpace);
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
                            GUILayout.Label("Smoothness", noSpace);
                            selectedMat.SetFloat("_Glossiness", GUILayout.HorizontalSlider(selectedMat.GetFloat("_Glossiness"), 0, 1));

                            GUILayout.Label("Metallic", noSpace);
                            selectedMat.SetFloat("_Metallic", GUILayout.HorizontalSlider(selectedMat.GetFloat("_Metallic"), 0, 1));
                        }
                        if (normalTex != null)
                        {
                            GUILayout.Label("Normal Scale", noSpace);
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
                        GUILayout.Label("Texture Mapping", noSpace);

                        Vector2 textureScale = selectedMat.GetTextureScale("_MainTex");
                        Vector2 textureOffset = selectedMat.GetTextureOffset("_MainTex");

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Scale");
                        FloatField("TextureScaleX", ref textureScale.x);
                        FloatField("TextureScaleY", ref textureScale.y);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Offset");
                        FloatField("TextureOffsetX", ref textureOffset.x);
                        FloatField("TextureOffsetY", ref textureOffset.y);
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
                        GUILayout.BeginVertical(noSpace);

                        // image
                        const int imageButtonSize = 75;
                        GUILayout.Label("Main Texture", noSpace);
                        if (GUILayout.Button(selectedMat.mainTexture, noSpace, GUILayout.Width(imageButtonSize), GUILayout.Height(imageButtonSize)))
                        {
                            tex_browser_enabled = true;
                            MaterialManager.SelectedTexture = (Texture2D)selectedMat.mainTexture;
                            MaterialManager.UpdateSelectedTexture = delegate (Texture2D tex) { selectedMat.mainTexture = tex; MarkAsModified(); };
                        }

                        if (GUILayout.Toggle(normalTex != null, "Normal Map"))
                        {
                            if (normalTex == null || GUILayout.Button(normalTex, noSpace, GUILayout.Width(imageButtonSize), GUILayout.Height(imageButtonSize)))
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
                        else if (normalTex != null)
                        {
                            selectedMat.DisableKeyword("_NORMALMAP");
                            selectedMat.SetTexture("_BumpMap", null);
                            MarkAsModified();
                        }

                        if (GUILayout.Toggle(metalGlossTex != null, "Metal & Gloss"))
                        {
                            if (metalGlossTex == null || GUILayout.Button(metalGlossTex, noSpace, GUILayout.Width(imageButtonSize), GUILayout.Height(imageButtonSize)))
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
                        else if (metalGlossTex != null)
                        {
                            selectedMat.DisableKeyword("_METALLICGLOSSMAP");
                            selectedMat.SetTexture("_MetallicGlossMap", null);
                            MarkAsModified();
                        }

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();

                        // draw over
                        materialMode.Draw(matModeRect);
                        GUILayout.EndArea();

                        bool bRes;
                        bRes = GUI.Toggle(new Rect(5, 60, 75, 20), selected.data.Bounce, "Bounce");
                        if (selected.data.Bounce != bRes) { MarkAsModified(); selected.data.Bounce = bRes; }

                        // only convex objects can be used as triggers (whyyyy???)
                        MeshCollider mColl = selected.go.GetComponent<MeshCollider>();
                        if (mColl == null || mColl.convex)
                        {
                            bRes = GUI.Toggle(new Rect(85, 60, 75, 20), selected.data.Glass, "Glass");
                            if (selected.data.Glass != bRes) { MarkAsModified(); selected.data.Glass = bRes; }

                            if (!selected.data.Glass)
                            {
                                bRes = GUI.Toggle(new Rect(165, 60, 75, 20), selected.data.Lava, "Lava");
                                if (selected.data.Lava != bRes) { MarkAsModified(); selected.data.Lava = bRes; }
                            }
                        }
                        bRes = GUI.Toggle(new Rect(5, 80, 120, 20), selected.data.MarkAsObject, "Mark as Object");
                        if (selected.data.MarkAsObject != bRes) { MarkAsModified(); selected.data.MarkAsObject = bRes; }
                    }

                    if (selected.data.Type == ObjectType.Light)
                    {
                        GUILayout.BeginArea(new Rect(5, 165, 300, 400));
                        GUILayout.BeginVertical();

                        Light light = selected.go.GetComponent<Light>();

                        light.type = GUILayout.Toggle(light.type == LightType.Spot, "Spot Light") ? LightType.Spot : LightType.Point;
                        selected.data.LightType = light.type;

                        ColorButton(light.color, delegate (Color c) { light.color = selected.data.Color = selected.go.GetComponentInChildren<MeshRenderer>().material.color = c; }, 140);
                        
                        GUILayout.Label("Intensity");
                        FloatField("LightIntensity", light.intensity, delegate (float v) { light.intensity = v; selected.data.Intensity = light.intensity; });
                        
                        GUILayout.Label("Range");
                        FloatField("LightRange", light.range, delegate (float v) { light.range = v; selected.data.Range = light.range; });
                        
                        if (light.type == LightType.Spot)
                        {
                            GUILayout.Label("Spot Angle");
                            light.spotAngle = GUILayout.HorizontalSlider(light.spotAngle, 0, 180);
                        }

                        GUILayout.EndVertical();
                        GUILayout.EndArea();
                    }

                    if (selected.data.Type == ObjectType.Text)
                    {
                        GUILayout.BeginArea(new Rect(5, 165, 300, 400));
                        GUILayout.BeginVertical();
                        GUILayout.Space(5);

                        TextMeshPro tmp = selected.go.GetComponent<TextMeshPro>();

                        ColorButton(tmp.color, delegate (Color c) { tmp.color = selected.data.Color = c; }, 140);

                        tmp.text = GUILayout.TextArea(tmp.text);
                        selected.data.Text = tmp.text;

                        GUILayout.EndVertical();
                        GUILayout.EndArea();
                    }

                }
                

                if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && (SelectedObject.Object.data.Type == ObjectType.Geometry))
                {
                    
                }
                else
                {
                    if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject && (SelectedObject.Object.data.Type == ObjectType.Prefab) && SelectedObject.Object.data.PrefabId == PrefabType.Enemy) // only draw, so it appears on top
                        enemyGun.Draw(new Rect(35, 60, 100, 20));
                    // for some reason, the first button rendered takes priority over the mouse click
                    // even if it is below .. idk
                }
                GUI.DragWindow();
            }, "Object Properties");

            wir[(int)WindowId.LevelData] = GUI.Window(wid[(int)WindowId.LevelData], wir[(int)WindowId.LevelData], (windowId) => {
                gridAlign = GUI.Toggle(new Rect(5, 20, 190, 20), gridAlign == 1, "Grid Align") ? 1 : 0;

                GUI.Label(new Rect(5, 40, 100, 20), "Starting Gun");
                startGunDD.Draw(new Rect(95, 40, 100, 20));
                if (startingGun != startGunDD.Index)
                {
                    MarkAsModified();
                    startingGun = startGunDD.Index;

                }

                EditorObject spawnObject = globalObject.editorObjects.First(x => x.data.Type == ObjectType.Internal);
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

                GUI.DragWindow();
            }, "Map Settings");

            if (skyboxEditorEnabled)
            {
                wir[(int)WindowId.SkyboxEdit] = GUI.Window(wid[(int)WindowId.SkyboxEdit], wir[(int)WindowId.SkyboxEdit], (windowId) =>
                {
                    GUILayout.BeginVertical();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Global Sun Settings");
                    GUILayout.BeginVertical();
                    GUILayout.Space(7);
                    ColorButton(sun.color, delegate (Color c) { sun.color = c; });
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    GUILayout.Label("Intensity");
                    sun.intensity = GUILayout.HorizontalSlider(sun.intensity, 0, 5);

                    GUILayout.Label("Angle");
                    Vector2 sunAngle = (Vector2)sun.gameObject.transform.rotation.eulerAngles;
                    sunAngle.x = ((sunAngle.x + 90) % 360) - 90;
                    Vector2 newSunAngle = new Vector2(GUILayout.HorizontalSlider(sunAngle.x, -90, 90), GUILayout.HorizontalSlider(sunAngle.y, 0, 360));
                    if (sunAngle != newSunAngle)
                    {
                        sun.gameObject.transform.rotation = Quaternion.Euler(newSunAngle.x, newSunAngle.y, 0);
                        baker.UpdateEnvironment();
                        MarkAsModified();
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
                            RenderSettings.skybox = LevelLoader.Main.Skybox.Procedural;

                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Sun Size");
                            LevelLoader.Main.Skybox.Procedural.SetFloat("_SunSize", GUILayout.HorizontalSlider(LevelLoader.Main.Skybox.Procedural.GetFloat("_SunSize"), 0, 0.5f, GUILayout.Width(120)));

                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Sun Size Convergence");
                            LevelLoader.Main.Skybox.Procedural.SetFloat("_SunSizeConvergence", GUILayout.HorizontalSlider(LevelLoader.Main.Skybox.Procedural.GetFloat("_SunSizeConvergence"), 1, 10, GUILayout.Width(120)));

                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Atmosphere Thickness");
                            float oldThickness = LevelLoader.Main.Skybox.Procedural.GetFloat("_AtmosphereThickness");
                            float newThickness = GUILayout.HorizontalSlider(oldThickness, 0, 5, GUILayout.Width(120));
                            if (oldThickness != newThickness)
                            {
                                MarkAsModified();
                                LevelLoader.Main.Skybox.Procedural.SetFloat("_AtmosphereThickness", newThickness);
                                baker.UpdateEnvironment();
                            }

                            GUILayout.EndHorizontal();

                            GUILayout.Label("Sky Tint");
                            ColorButton(LevelLoader.Main.Skybox.Procedural.GetColor("_SkyTint"), delegate (Color c) { LevelLoader.Main.Skybox.Procedural.SetColor("_SkyTint", c); baker.UpdateEnvironment(); });

                            GUILayout.Label("Ground Color");
                            ColorButton(LevelLoader.Main.Skybox.Procedural.GetColor("_GroundColor"), delegate (Color c) { LevelLoader.Main.Skybox.Procedural.SetColor("_GroundColor", c); baker.UpdateEnvironment(); });

                            GUILayout.Label("Exposure");
                            float oldExposure = LevelLoader.Main.Skybox.Procedural.GetFloat("_Exposure");
                            float newExposure = GUILayout.HorizontalSlider(oldExposure, 0, 5);
                            if (oldExposure != newExposure)
                            {
                                MarkAsModified();
                                LevelLoader.Main.Skybox.Procedural.SetFloat("_Exposure", newExposure);
                                baker.UpdateEnvironment();
                            }
                            break;
                        case SkyboxMode.SixSided:
                            RenderSettings.skybox = LevelLoader.Main.Skybox.SixSided;

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
                                if (GUILayout.Button(LevelLoader.Main.Skybox.SixSided.GetTexture(shaderKey), GUILayout.Width(100), GUILayout.Height(100)))
                                {
                                    tex_browser_enabled = true;
                                    MaterialManager.SelectedTexture = (Texture2D)LevelLoader.Main.Skybox.SixSided.GetTexture(shaderKey);
                                    MaterialManager.UpdateSelectedTexture = delegate (Texture2D tex) { LevelLoader.Main.Skybox.SixSided.SetTexture(shaderKey, tex); baker.UpdateEnvironment(); };
                                }

                                GUILayout.EndVertical();
                            }
                            GUILayout.EndHorizontal();

                            GUILayout.Label("Exposure");
                            oldExposure = LevelLoader.Main.Skybox.SixSided.GetFloat("_Exposure");
                            newExposure = GUILayout.HorizontalSlider(oldExposure, 0, 5);
                            if (oldExposure != newExposure)
                            {
                                MarkAsModified();
                                LevelLoader.Main.Skybox.SixSided.SetFloat("_Exposure", newExposure);
                                baker.UpdateEnvironment();
                            }

                            GUILayout.Label("Rotation");
                            float oldRotation = LevelLoader.Main.Skybox.SixSided.GetFloat("_Rotation");
                            float newRotation = GUILayout.HorizontalSlider(oldRotation, 0, 360);
                            if (oldRotation != newRotation)
                            {
                                MarkAsModified();
                                LevelLoader.Main.Skybox.SixSided.SetFloat("_Rotation", newRotation);
                                baker.UpdateEnvironment();
                            }

                            break;

                        case SkyboxMode.Default:
                            RenderSettings.skybox = LevelLoader.Main.Skybox.Default;
                            break;
                    }
                    GUILayout.EndVertical();

                    skyboxMode.Draw(skyboxModeRect);
                    GUI.DragWindow();
                }, "Skybox Editor");
            }

            if (thumbnail_preview != null)
                thumbnail_preview_rect = GUI.Window(wid[(int)WindowId.Prompt], thumbnail_preview_rect, _ =>
                {
                    GUI.DrawTexture(new Rect(5, 25, Screen.width / 2, Screen.width * 9 / 32), thumbnail_preview);
                    GUI.DragWindow();
                }, "Preview Thumbnail");
            if (uploadDialog != null && !uploadDialog.DrawWindow())
            {
                thumbnail_preview = null;
                uploadDialog = null;
            }
            if (is_uploading)
                GUI.ModalWindow(wid[(int)WindowId.Prompt], new Rect(Screen.width / 2 - 125, Screen.height / 2 - 25, 250, 50), _ =>
                {
                    GUI.Label(new Rect(5, 25, 250, 25), "Please wait. Uploading map...");
                }, "mod.io Workshop");
        }
        static Texture2D thumbnail_preview = null;
        static ModIO.Workshop.EditMod uploadDialog = null;
        static Rect thumbnail_preview_rect;
        static bool is_uploading = false;
        public static void _onupdate()
        {
            if (!editorMode || Camera.main == null) return;
            if (levelName == "") return;
            
            if(dg_screenshot && Input.GetKey(KeyCode.Return))
            {
                dg_screenshot = false;
                var ss = MakeScreenshot();
                thumbnail_preview = new Texture2D(0, 0);
                thumbnail_preview.LoadImage(ss);
                thumbnail_preview_rect = new Rect(Screen.width / 4 - 5, (Screen.height - (Screen.width * 9 / 32 + 30)) / 2, Screen.width / 2 + 10, Screen.width * 9 / 32 + 30);
                uploadDialog = new ModIO.Workshop.EditMod((name, description, comments) => new Thread(() =>
                {
                    is_uploading = true;
                    ModIO.API.AddMod(name, description, ss, SaveLevelData(), comments);
                    is_uploading = false;
                }).Start());
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
                        if(obj.go == go)
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

            // update clickable gizmo
            if (SelectedObject.Type == SelectedObject.SelectedType.EditorObject || SelectedObject.Type == SelectedObject.SelectedType.ObjectGroup && !SelectedObject.Group.isGlobal)
            {
                if (!holdingGizmo)
                {
                    // transform the gizmo so its on the slected object
                    gizmoPos = SelectedObject.Basic.worldPos;
                    clickGizmo.transform.position = gizmoPos;
                    // switch to the correct mode
                    if (Input.GetKey(scaleGizmoKey)) { gizmoMode = GizmoMode.Scale; }
                    else if (Input.GetKey(rotateGizmoKey)) { gizmoMode = GizmoMode.Rotate; }
                    else { gizmoMode = GizmoMode.Translate; }

                    // if the object doesn't support scaling, don't show this gizmo
                    clickGizmo.SetActive(gizmoMode != GizmoMode.Scale || SelectedObject.DoScale);
                    // if the object has partial rotation, only show Y axis
                    GizmoXAxis.SetActive(gizmoMode != GizmoMode.Rotate || SelectedObject.DoFullRotation);
                    GizmoZAxis.SetActive(gizmoMode != GizmoMode.Rotate || SelectedObject.DoFullRotation);

                    // allow ortogonal translation
                    if (Input.GetKey(KeyCode.LeftAlt) && gizmoMode == GizmoMode.Translate)
                        clickGizmo.transform.rotation = Quaternion.identity;
                    else
                        clickGizmo.transform.rotation = SelectedObject.Basic.transformRotation;
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
                            SelectedObject.Basic.MoveByGizmo(gizmoDir * offset);
                            clickGizmo.transform.position = gizmoPos + gizmoDir * offset;
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

            EditorObject spawnObject = globalObject.editorObjects.First(x => x.data.Type == ObjectType.Internal);
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
            cam.cullingMask &= ~gizmoLayerMask; // don't show gizmo in screenshot
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Texture2D screenShot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(GOcam);
            return screenShot.EncodeToPNG();
        }

        // saver
        private static byte[] SaveLevelData(bool kmp_export = false)
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
            if(kmp_export)
            {
                // strip/hide unused metadata
                map.GridAlign = 0;
                map.StartingGun = 0;
                map.StartPosition = Vector3.zero;
                map.StartOrientation = 0;
                map.AutomataScript = "";
            }
            map.SaveGlobalLight(sun);
            map.SaveTree(globalObject, kmp_export);
            map.SaveMaterials();
            
            // export
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes(LevelLoader.LevelSerializer.SaveVersion), 0, 4);
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
        public static void ClearMaterial(Material mat)
        {
            int index = MaterialManager.Materials.IndexOf(mat);
            List<EditorObject> allObjects = globalObject.AllEditorObjects();

            // check if the mat is used by any object
            foreach (EditorObject eo in allObjects)
            {
                if (eo.data.MaterialId == index) return;
            }

            // if not, remove the mat
            MaterialManager.Materials.RemoveAt(index);
            foreach (EditorObject eo in allObjects)
                if (eo.data.MaterialId > index) eo.data.MaterialId--;
        }

        private static void LoadLevel(string path)
        {
            Loadson.Console.Log("setting up level editor");
            ObjectGroup ReplicateObjectGroup(LevelData.ObjectGroup group, ObjectGroup parentGroup)
            {
                // set up this group
                ObjectGroup objGroup = new ObjectGroup(group, parentGroup);
                // load objects
                foreach (var obj in group.Objects)
                    new EditorObject(obj, objGroup);
                // load sub groups
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
            if (RenderSettings.skybox == LevelLoader.Main.Skybox.Default) skyboxMode.Index = (int)SkyboxMode.Default;
            else if (RenderSettings.skybox == LevelLoader.Main.Skybox.Procedural) skyboxMode.Index = (int)SkyboxMode.Procedural;
            else if (RenderSettings.skybox == LevelLoader.Main.Skybox.SixSided) skyboxMode.Index = (int)SkyboxMode.SixSided;
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
            // main constructor
            public EditorObject(LevelData.LevelObject playObj, ObjectGroup group, bool newObject = false)
            {
                group.editorObjects.Add(this);
                data = playObj;
                go = data.LoadObject(group.go, false, newObject);
                go.AddComponent<KME_Object>();
            }

            // create new geometry
            public EditorObject(Vector3 position, ObjectGroup group, GeometryShape shape = GeometryShape.Cube) : this(new LevelData.LevelObject(position, Vector3.zero, Vector3.one, "Geometry Object", 6, Color.white, false, false, false, false, false, shape), group, true) { }
            // create new prefab
            public EditorObject(Vector3 position, ObjectGroup group, PrefabType _prefabId) : this(new LevelData.LevelObject(position, Vector3.zero, Vector3.one, _prefabId.ToString(), _prefabId, 0), group, true) { }
            
            // create player spawn
            public EditorObject(Vector3 pos, float orientation)
            {
                data = new LevelData.LevelObject
                {
                    Name = "Player Spawn",
                    Position = pos,
                    Rotation = Vector3.zero,
                    Scale = Vector3.one,
                    Type = ObjectType.Internal,
                };
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.AddComponent<KME_Object>();
                go.transform.localScale = new Vector3(1f, 1.5f, 1f);
                go.transform.position = pos;

                // add a visor so you can tell what direction it is lul
                var visorGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visorGO.transform.parent = go.transform;
                visorGO.transform.localPosition = new Vector3(0, .6f, .25f);
                visorGO.transform.localScale = new Vector3(.85f, .23f, .5f);
                visorGO.GetComponent<MeshRenderer>().material.color = Color.black;

                go.transform.eulerAngles = new Vector3(0f, orientation, 0f);
                go.name = "Player Spawn";
                go.GetComponent<MeshRenderer>().material.color = Color.yellow;
            }

            public LevelData.LevelObject data;
            public GameObject go;

            public void Clone(ObjectGroup parent)
            {
                EditorObject obj = new EditorObject(new LevelData.LevelObject(data), parent);
                obj.go.name = go.name + " (Clone)";
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
                    if (data.Type == ObjectType.Geometry) { go.GetComponent<KMETextureScaling>().UpdateScale(); }
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
                // global position
                go.transform.position = preMove + delta;
                if (gridAlign != 0)
                {
                    aPosition = Vector3Extensions.SnapPos(aPosition, positionSnap, aRotation);
                    if (data.Type == ObjectType.Text)
                    {
                        // bring slightly forward to prevent z fighting
                        aPosition -= go.transform.forward * 0.001f;
                    }
                }
            }
            public void ScaleByGizmo(Vector3 delta)
            {
                if (data.Type == ObjectType.Internal) return;
                
                else if (data.Type == ObjectType.Text)
                    // scale all dimensions by the same amount, so text isn't stretched
                    aScale = preScale + (Vector3.one * (delta.x + delta.y + delta.z));
                else
                    aScale = preScale + delta;

                // only snap scale for geometry and text objects
                if (gridAlign != 0)
                    if (data.Type == ObjectType.Geometry)
                        aScale = Vector3Extensions.SnapScale(aScale, scaleSnap);
                    else if (data.Type == ObjectType.Text)
                        aScale = Vector3Extensions.SnapScaleExp(aScale.x, scaleSnapExp);
            }
            public void RotateByGizmo(Quaternion delta)
            {
                go.transform.localRotation = preRotate * delta;
                if (gridAlign != 0) { aRotation = Vector3Extensions.Snap(aRotation, rotationSnap); }
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
                go = new GameObject(name ?? "Object Group");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                editorObjects = new List<EditorObject>();
                objectGroups = new List<ObjectGroup>();
            }
            public ObjectGroup(LevelData.ObjectGroup group, ObjectGroup parentGroup)
            {
                editorObjects = new List<EditorObject>();
                objectGroups = new List<ObjectGroup>();

                if (parentGroup != null)
                {
                    go = group.LoadObject(parentGroup.go);
                    parentGroup.objectGroups.Add(this);
                }
                else go = group.LoadObject(null);
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
    }

    public class KME_Object : MonoBehaviour { } // mark object as being kme, to be able to select any object with middle click
}
