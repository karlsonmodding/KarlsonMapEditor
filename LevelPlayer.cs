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
            LoadLevel(Path.GetFileName(levelPath), File.ReadAllBytes(levelPath));
        }
        public static void LoadLevel(string name, byte[] data)
        {
            Loadson.Console.Log("setting up level player");
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
            Loadson.Console.Log("loading level data...");

            // remove objects with colliders
            foreach (Collider c in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (c.gameObject != PlayerMovement.Instance.gameObject && c.gameObject.GetComponent<DetectWeapons>() == null) UnityEngine.Object.Destroy(c.gameObject);
            // remove lights
            foreach (Light l in UnityEngine.Object.FindObjectsOfType<Light>())
                if (l != RenderSettings.sun) UnityEngine.Object.Destroy(l.gameObject);

            // set up materials for geometry objects
            levelData.SetupMaterials();

            // init global light
            GameObject sunGO = new GameObject();
            Light sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            levelData.SetupGlobalLight(sun);

            // bake skybox reflections
            GameObject bakerGO = new GameObject();
            bakerGO.AddComponent<EnvironmentBaker>();

            // init the player
            if (levelData.startingGun != 0)
                PlayerMovement.Instance.spawnWeapon = LevelData.MakePrefab((PrefabType)(levelData.startingGun - 1));
            PlayerMovement.Instance.transform.position = levelData.startPosition;
            PlayerMovement.Instance.playerCam.transform.localRotation = Quaternion.Euler(0f, levelData.startOrientation, 0f);
            PlayerMovement.Instance.orientation.transform.localRotation = Quaternion.Euler(0f, levelData.startOrientation, 0f);

            List<LevelData.LevelObject> enemyToFix = new List<LevelData.LevelObject>();
            void ReplicateObjectGroup(LevelData.ObjectGroup group, GameObject parentObject)
            {
                GameObject objGroup = group.LoadObject(parentObject);

                foreach (var obj in group.Objects)
                {
                    GameObject go = obj.LoadObject(objGroup, true);

                    // fix enemies
                    if ((obj.Type == ObjectType.Prefab) && (obj.PrefabId == PrefabType.Enemey))
                    {
                        needsNavMesh = true;
                        enemyToFix.Add(obj);
                        obj._enemyFixY = go.transform.position.y;
                        Enemy e = go.GetComponent<Enemy>();
                        if (obj.PrefabData != 0)
                        {
                            go.GetComponent<NavMeshAgent>().enabled = true;
                            go.GetComponent<NavMeshAgent>().enabled = false;
                            go.GetComponent<NavMeshAgent>().enabled = true;
                        }
                        obj._enemyFix = go;
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
                    if (obj.PrefabId == PrefabType.Enemey)
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
