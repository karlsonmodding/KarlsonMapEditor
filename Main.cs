using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using KarlsonMapEditor.Scripting_API;
using KarlsonMapEditor.Workshop_API;
using Loadson;
using LoadsonAPI;
using LoadsonExtensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace KarlsonMapEditor
{
    public class Main : Mod
    {
        public const string Version = "5.0.0";

        public override void OnEnable()
        {
            prefs = Preferences.GetPreferences();
            if (!prefs.ContainsKey("edit_recent"))
                prefs.Add("edit_recent", "");

            directory = GetUserFilesFolder();
            Directory.CreateDirectory(Path.Combine(directory, "Levels", "Workshop"));

            LevelTimeDB.Load();

            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("KarlsonMapEditor");
            harmony.PatchAll();

            List<Texture2D> temp = new List<Texture2D>();
            foreach(var t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                switch(t.name)
                {
                    default: break;
                    case "GridBox_Default":
                    case "prototype_512x512_grey3":
                    case "prototype_512x512_white":
                    case "prototype_512x512_yellow":
                    case "Floor":
                    case "Blue":
                    case "Red":
                    case "Barrel":
                    case "Orange":
                    case "Yellow":
                    case "UnityWhite":
                    case "UnityNormalMap":
                    case "Sunny_01B_down":
                        temp.Add(t);
                        break;
                }
            }
            gameTex = temp.ToArray();
            if(gameTex.Length != 13) Loadson.Console.Log("<color=red>Invalid game texture array. Expected 13 items, got " + gameTex.Length + "</color>");

            MenuEntry.AddMenuEntry(new List<(string, System.Action)>
            {
                ("List", () => {
                    MenuCamera cam = UnityEngine.Object.FindObjectOfType<MenuCamera>();
                    cam.ReflectionSet("desiredPos", new Vector3(-1f, 15.1f, 184.06f));
                    cam.ReflectionSet("desiredRot", Quaternion.Euler(0f, -90f, 0f));

                    GameObject.Find("/UI/Menu").SetActive(false);
                    GameObject.Find("/UI").transform.Find("Custom").gameObject.SetActive(true);
                    Hook_Lobby_Start.RenderMenuPage(1);
                }),
                ("Workshop", () => {
                    if(ModIO.Auth.ModioBearer == "") {
                        ModIO.Workshop.ShowDialog("mod.io Workshop", "You need to be logged in to access the mod.io workshop.", "Ok", "", null);
                        return;
                    }
                    MenuCamera cam = UnityEngine.Object.FindObjectOfType<MenuCamera>();
                    cam.ReflectionSet("desiredPos", new Vector3(-1f, 15.1f, 184.06f));
                    cam.ReflectionSet("desiredRot", Quaternion.Euler(0f, -90f, 0f));

                    GameObject.Find("/UI/Menu").SetActive(false);
                    ModIO.Workshop.Open();
                }),
                ("Editor", () => LevelEditor.StartEdit()),
                ("<size=25>Open Maps\nFolder</size>", () => Process.Start(Path.Combine(directory, "Levels"))),
            }, "Map Editor");

            // load im assets
            ColorPicker.imRight = LoadAsset<Texture2D>("imRight");
            ColorPicker.imLeft = LoadAsset<Texture2D>("imLeft");
            ColorPicker.imCircle = LoadAsset<Texture2D>("imCircle");

            // load material assets
            // initialize level player
            LevelLoader.Main.Init(new LoadsonPrefabProvider(), Loadson.Console.Log, gameTex);
            LevelLoader.Main.Skybox.Default = RenderSettings.skybox;
            LevelLoader.Main.Skybox.Procedural = LoadAsset<Material>("SkyboxProcedural");
            LevelLoader.Main.Skybox.SixSided = LoadAsset<Material>("SkyboxSixSided");
            LevelLoader.MaterialManager.defaultShader = LoadAsset<Shader>("StandardVariants");
            GizmoMeshBuilder.gizmoShader = LoadAsset<Shader>("GizmoShader");
            LevelLoader.MaterialManager.lightBillboardShader = LoadAsset<Shader>("LightBillboardShader");
            LevelLoader.MaterialManager.lightbulbTransparent = LoadAsset<Texture2D>("lightbulb_transparent");
            LevelLoader.MaterialManager.lightbulbTransparentColor = LoadAsset<Texture2D>("lightbulb_transparent_color");

            if (!DiscordAPI.HasDiscord)
                Loadson.Console.Log("Discord not found. You will not be able to like/upload levels to the workshop");
            else
            {
                new Thread(async () =>
                {
                    while (DiscordAPI.User.Id == 0) Thread.Sleep(200);
                    while (DiscordAPI.Bearer == "") Thread.Sleep(200);

                    ModIO.Auth.Login();
                }).Start();
            }
            loginWid = ImGUI_WID.GetWindowId();
        }

        int loginWid;
        Rect loginRect = new Rect(Screen.width - 205, Screen.height - 50, 200, 45);

        public override void OnGUI()
        {
            LevelEditor._ongui();
            ModIO.Auth._ongui();
            ModIO.Workshop._ongui();
            if(loginWid != -1 && ModIO.Auth.ModioBearer == "" && !noDiscordAck)
                GUI.Window(loginWid, loginRect, (_) => {
                    if (!DiscordAPI.HasDiscord)
                    {
                        GUI.Label(new Rect(5, 20, 200, 30), "Discord was not detected");
                        if (GUI.Button(new Rect(160, 20, 35, 20), "Ok")) noDiscordAck = true;
                    }
                    else if (DiscordAPI.User.Id == 0)
                        GUI.Label(new Rect(5, 20, 200, 30), "Awaiting Discord User");
                    else if (DiscordAPI.Bearer.Length < 2)
                        GUI.Label(new Rect(5, 20, 200, 30), "Awaiting Discord Bearer");
                    else
                        GUI.Label(new Rect(5, 20, 200, 30), "Logging into mod.io");
                }, "mod.io Workshop Login");
        }

        public override void Update(float deltaTime)
        {
            LevelEditor._onupdate();
            ModIO.Workshop._onupdate();
            if (runOnMain.Count > 0)
            { // once at a time, not to overload
                Action run = runOnMain[0];
                runOnMain.RemoveAt(0);
                run?.Invoke();
            }
            if (LevelLoader.LevelPlayer.currentLevel != "" && LevelPlayer.currentScript != null)
                LevelPlayer.currentScript.InvokeFunction("update", deltaTime);
        }
        public override void FixedUpdate(float fixedDeltaTime)
        {
            if (LevelLoader.LevelPlayer.currentLevel != "" && LevelPlayer.currentScript != null)
                LevelPlayer.currentScript.InvokeFunction("fixedupdate", fixedDeltaTime);
        }

        public static Dictionary<string, string> prefs;
        public static string directory;
        public static Texture2D[] gameTex;
        public static List<Action> runOnMain = new List<Action>();
        public static bool noDiscordAck = false;
    }

    public class LoadsonPrefabProvider : LevelLoader.IPrefabProvider
    {
        public override GameObject NewPistol() { return LoadsonAPI.PrefabManager.NewPistol(); }
        public override GameObject NewAk47() { return LoadsonAPI.PrefabManager.NewAk47(); }
        public override GameObject NewShotgun() { return LoadsonAPI.PrefabManager.NewShotgun(); }
        public override GameObject NewBoomer() { return LoadsonAPI.PrefabManager.NewBoomer(); }
        public override GameObject NewGrappler() { return LoadsonAPI.PrefabManager.NewGrappler(); }
        public override GameObject NewDummyGrappler() { return LoadsonAPI.PrefabManager.NewDummyGrappler(); }
        public override GameObject NewTable() { return LoadsonAPI.PrefabManager.NewTable(); }
        public override GameObject NewBarrel() { return LoadsonAPI.PrefabManager.NewBarrel(); }
        public override GameObject NewLocker() { return LoadsonAPI.PrefabManager.NewLocker(); }
        public override GameObject NewScreen() { return LoadsonAPI.PrefabManager.NewScreen(); }
        public override GameObject NewMilk() { return LoadsonAPI.PrefabManager.NewMilk(); }
        public override GameObject NewEnemy() { return LoadsonAPI.PrefabManager.NewEnemy(); }
        public override GameObject NewGlass() { return LoadsonAPI.PrefabManager.NewGlass(); }
        public override PhysicMaterial BounceMaterial() { return LoadsonAPI.PrefabManager.BounceMaterial(); }
    }
}
