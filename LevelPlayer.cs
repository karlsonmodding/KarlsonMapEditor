using HarmonyLib;
using KarlsonMapEditor.Scripting_API;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace KarlsonMapEditor
{
    public static class LevelPlayer
    {
        public static string currentLevel { get; private set; } = "";
        public static void ExitedLevel() => currentLevel = "";
        private static LevelData levelData;
        private static Automata.Backbone.FunctionRunner mainFunction = null;
        public static ScriptRunner currentScript { get; private set; } = null;
        public static NavMeshData navData;

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
            navData = null;
            LoadScript();
            SceneManager.sceneLoaded += LoadLevelData;
            UnityEngine.Object.FindObjectOfType<Lobby>().LoadMap("4Escape0");
        }

        // recursively get bounds of colliders under this transform
        public static void GetBounds(Transform t, ref Bounds b)
        {
            Collider coll = t.GetComponent<Collider>();
            // encapsulate the world-space bounds of the object
            if (coll)
            {
                // algorithm to bound a transformed box
                // doesn't need to check individual vertices
                Vector3 size = Vector3.Scale(coll.bounds.size, t.lossyScale);
                Vector3 xBasis = t.rotation * new Vector3(size.x, 0, 0);
                Vector3 yBasis = t.rotation * new Vector3(0, size.y, 0);
                Vector3 zBasis = t.rotation * new Vector3(0, 0, size.z);
                Vector3 boundedSize = Vector3.Max(xBasis, -xBasis) + Vector3.Max(yBasis, -yBasis) + Vector3.Max(zBasis, -zBasis);

                Bounds objectBounds = new Bounds(t.position, boundedSize);
                b.Encapsulate(objectBounds);
            }
            // recursive call for children
            for (int i = 0; i < t.childCount; i++)
                GetBounds(t.GetChild(i), ref b);
        }
        
        // generate a navigation mesh for Enemy AI
        public static void GenerateNavMesh(Transform sourceRoot)
        {
            Loadson.Console.Log("Creating navigation mesh");

            // set up navigation settings
            NavMeshBuildSettings settings = new NavMeshBuildSettings();
            settings.agentRadius = 1.25f;
            settings.agentHeight = 6f;
            settings.agentSlope = 45;
            settings.agentClimb = 0.75f;
            settings.minRegionArea = 2f;

            // apply modifiers to geometry
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
            foreach (NavMeshModifier mod in NavMeshModifier.activeModifiers)
            {
                NavMeshBuildMarkup markup = new NavMeshBuildMarkup();
                markup.area = mod.area;
                markup.overrideArea = mod.overrideArea;
                markup.ignoreFromBuild = mod.ignoreFromBuild;
                markup.root = mod.transform;
                markups.Add(markup);

            }

            // get build sources
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(
                sourceRoot,
                LayerMask.GetMask("Ground"),
                NavMeshCollectGeometry.PhysicsColliders,
                NavMesh.GetAreaFromName("Walkable"),
                markups,
                sources
                );

            // find the bounds
            Bounds navBounds = new Bounds();
            GetBounds(sourceRoot, ref navBounds);

            // create nav mesh data
            navData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                navBounds,
                Vector3.zero,
                Quaternion.identity
                );
        }

        // the scene for escape0 is loaded
        // this method removes all escape0-specific data after loading it
        public static void PrepareLevel()
        {
            NavMesh.RemoveAllNavMeshData();

            string[] removeNames = new string[] { "Table", "Locker", "Cube", "Enemy", "Barrel", "Enemy", "Door", "GroundCheck" };
            foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (go == PlayerMovement.Instance.gameObject || go.GetComponent<DetectWeapons>()) continue;
                // remove objects with colliders and lights
                if (go.GetComponent<Collider>() || go.GetComponent<Light>())
                {
                    go.SetActive(false);
                    Object.Destroy(go);
                    continue;
                }
                // remove specific named objects
                foreach (string s in removeNames)
                {
                    if (go.name.StartsWith(s))
                    {
                        go.SetActive(false);
                        Object.Destroy(go);
                        continue;
                    }
                }
            }
        }

        public static void LoadLevelData(Scene arg0, LoadSceneMode arg1)
        {
            PrepareLevel();

            Loadson.Console.Log("Loading level data");

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

            void ReplicateObjectGroup(LevelData.ObjectGroup group, GameObject parentObject)
            {
                // set up this group
                GameObject objGroup = group.LoadObject(parentObject);
                // load objects
                foreach (LevelData.LevelObject obj in group.Objects)
                    obj.LoadObject(objGroup, true);
                // load sub groups
                foreach (var grp in group.Groups)
                    ReplicateObjectGroup(grp, objGroup);
            }

            GameObject root = new GameObject("Static Root");
            ReplicateObjectGroup(levelData.GlobalObject, root);

            // set up nav mesh data
            if (navData == null)
                GenerateNavMesh(root.transform);
            NavMesh.AddNavMeshData(navData);
            
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
