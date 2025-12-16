using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace KarlsonMapEditor.LevelLoader
{
    public static class LevelPlayer
    {
        public static string currentLevel { get; private set; } = "";
        public static void ExitedLevel()
        {
            SceneManager.sceneLoaded -= LoadLevelData;
            currentLevel = "";
        }
        public static LevelData levelData { get; private set; }
        public static NavMeshData navData;
        static Action postLoad;

        public static void LoadLevel(string name, byte[] data, Action pre_load = null, Action post_load = null)
        {
            Main.Logger("setting up level player");
            currentLevel = name;
            levelData = new LevelData(data);
            navData = null;
            pre_load?.Invoke();
            postLoad = post_load;
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
            Main.Logger("Creating navigation mesh");

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

            Main.Logger("Loading level data");

            // set up materials for geometry objects
            levelData.SetupMaterials();

            // init global light
            Light sun = new GameObject().AddComponent<Light>();
            sun.type = LightType.Directional;
            levelData.SetupGlobalLight(sun);

            // bake skybox reflections
            EnvironmentBaker baker = new GameObject().AddComponent<EnvironmentBaker>();

            // init the player
            if (levelData.startingGun != 0)
                PlayerMovement.Instance.spawnWeapon = LevelData.MakePrefab((PrefabType)(levelData.startingGun - 1));
            PlayerMovement.Instance.transform.position = levelData.startPosition;
            PlayerMovement.Instance.playerCam.transform.localRotation = Quaternion.Euler(0f, levelData.startOrientation, 0f);
            PlayerMovement.Instance.orientation.transform.localRotation = Quaternion.Euler(0f, levelData.startOrientation, 0f);

            bool needsNavMesh = false;
            void ReplicateObjectGroup(LevelData.ObjectGroup group, GameObject parentObject)
            {
                // set up this group
                GameObject objGroup = group.LoadObject(parentObject);
                // load objects
                foreach (LevelData.LevelObject obj in group.Objects)
                {
                    if (obj.Type == ObjectType.Prefab && obj.PrefabId == PrefabType.Enemy)
                        needsNavMesh = true;
                    obj.LoadObject(objGroup, true);
                }
                // load sub groups
                foreach (var grp in group.Groups)
                    ReplicateObjectGroup(grp, objGroup);
            }

            GameObject root = new GameObject("Static Root");
            ReplicateObjectGroup(levelData.GlobalObject, root);

            // set up nav mesh data
            if(needsNavMesh)
            {
                if (navData == null)
                    GenerateNavMesh(root.transform);
                NavMesh.AddNavMeshData(navData);
            }
            
            postLoad?.Invoke();
        }
    }
}
