using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using static KarlsonMapEditor.CustomDescriptors;
using KarlsonMapEditor.Scripting_API;

namespace KarlsonMapEditor
{
    public class LuaScriptRunner : MonoBehaviour
    {
        public static readonly string ScriptPath = Path.Combine(Main.directory, "_temp.lua");

        public const string DefaultCode = @"
-- Any code written below will be executed every time the level is (re)started.
-- Lua documentation: https://www.lua.org/pil/contents.html
-- Moonsharp Lua Differences: https://www.moonsharp.org/moonluadifferences.html
-- Scripting API: https://github.com/SnazzGass/KarlsonMapEditor/wiki/Scripting-API

print('Hello, world!');
";

        public string Code;

        private static bool running = false;

        private readonly Script script = new Script(CoreModules.Preset_SoftSandbox);

        private void Awake()
        {
            script.Options.DebugPrint = LuaDebug;

            RegisterTypes();
        }
        // logging
        private void LuaDebug(string msg)
        {
            Loadson.Console.Log("[Lua] " + msg);
        }

        private StandardUserDataDescriptor RegisterStaticWithConstructor<T>()
        {
            StandardUserDataDescriptor descriptor = (StandardUserDataDescriptor)UserData.RegisterType<T>();
            
            // remove the default constructors
            descriptor.RemoveMember("__new");

            // add the desired constructors
            foreach (ConstructorInfo constructor in typeof(T).GetConstructors())
                descriptor.AddMember("new", new MethodMemberDescriptor(constructor));

            return descriptor;
        }

        private IUserDataDescriptor RegisterDestructableProxyType<TProxy, TTarget>(Func<TTarget, TProxy> wrapDelegate) where TProxy : class where TTarget : class
        {
            IUserDataDescriptor proxyDescriptor = UserData.RegisterType<TProxy>();
            DestructableProxyUserDataDescriptor targetDescriptor = new DestructableProxyUserDataDescriptor(new DelegateProxyFactory<TProxy, TTarget>(wrapDelegate), proxyDescriptor, null);
            return UserData.RegisterType<TTarget>(targetDescriptor);
        }

        private void FilterComponentMembers(StandardUserDataDescriptor descriptor)
        {
            descriptor.RemoveMember("__new");
            descriptor.RemoveMember("hideFlags");
            descriptor.RemoveMember("BroadcastMessage");
            descriptor.RemoveMember("GetComponentInChildren");
            descriptor.RemoveMember("GetComponentInParent");
            descriptor.RemoveMember("GetComponents");
            descriptor.RemoveMember("GetComponentsInChildren");
            descriptor.RemoveMember("GetComponentsInParent");
            descriptor.RemoveMember("SendMessage");
            descriptor.RemoveMember("SendMessageUpwards");
            descriptor.RemoveMember("TryGetComponent");
            descriptor.RemoveMember("GetInstanceID");
            descriptor.RemoveMember("DestroyImmediate");
            descriptor.RemoveMember("DontDestroyOnLoad");
        }

        // interface
        private void RegisterTypes()
        {
            // unity common structs
            RegisterStaticWithConstructor<Vector2>();
            script.Globals["Vector2"] = UserData.CreateStatic<Vector2>();
            RegisterStaticWithConstructor<Vector3>();
            script.Globals["Vector3"] = UserData.CreateStatic<Vector3>();
            RegisterStaticWithConstructor<Quaternion>();
            script.Globals["Quaternion"] = UserData.CreateStatic<Quaternion>();
            RegisterStaticWithConstructor<Color>();
            script.Globals["Color"] = UserData.CreateStatic<Color>();
            RegisterStaticWithConstructor<Matrix4x4>();
            script.Globals["Matrix"] = UserData.CreateStatic<Matrix4x4>();

            // level object data
            RegisterStaticWithConstructor<LevelData.LevelObject>();
            script.Globals["LevelObjectData"] = UserData.CreateStatic<LevelData.LevelObject>();

            // unity common objects
            RegisterDestructableProxyType<ObjectProxy, UnityEngine.Object>(o => new ObjectProxy(o));
            script.Globals["Object"] = UserData.CreateStatic<UnityEngine.Object>();
            RegisterDestructableProxyType<GameObjectProxy, GameObject>(o => new GameObjectProxy(o));
            script.Globals["GameObject"] = UserData.CreateStatic<GameObject>();
            RegisterDestructableProxyType<ComponentProxy, Component>(o => new ComponentProxy(o));

            UserData.RegisterType<Texture>(InteropAccessMode.HideMembers);
            RegisterDestructableProxyType<MaterialProxy, Material>(o => new MaterialProxy(o));
            RegisterDestructableProxyType<TextMeshProProxy, TextMeshPro>(o => new TextMeshProProxy(o));
            RegisterDestructableProxyType<LightProxy, Light>(o => new LightProxy(o));

            // easier to create these by removing members rather than by setting up a proxy
            StandardUserDataDescriptor transformDescriptor = (StandardUserDataDescriptor)UserData.RegisterType<Transform>();
            FilterComponentMembers(transformDescriptor);
            transformDescriptor.RemoveMember("hierarchyCapacity");
            transformDescriptor.RemoveMember("hierarchyCount");
            //transformDescriptor.RemoveMember("root");

            StandardUserDataDescriptor rigidbodyDescriptor = (StandardUserDataDescriptor)UserData.RegisterType<Rigidbody>();
            FilterComponentMembers(rigidbodyDescriptor);


            // methods
            script.Globals["Raycast"] = (Func<Vector3, Vector3, float, int, Table>)Raycast;
            // TODO: set gun on enemy

            // enums
            UserData.RegisterType<ObjectType>();
            script.Globals["LevelObjectDataType"] = typeof(ObjectType);
            UserData.RegisterType<PrefabType>();
            script.Globals["Prefab"] = typeof(PrefabType);
            UserData.RegisterType<GeometryShape>();
            script.Globals["Geometry"] = typeof(GeometryShape);

            UserData.RegisterType<EventArgs>();
        }


        // callbacks
        private static DynValue UpdateFunc;
        private static DynValue FixedUpdateFunc;
        private static DynValue TriggerFunc;

        // init the lua script, this should be done after the level has been loaded
        public void LuaStart(GameObject Root, GameObject Sun, GameObject Player, Action BakeReflections)
        {
            // set up globals
            script.Globals["Root"] = Root;
            script.Globals["Sun"] = Sun;
            script.Globals["Player"] = Player;
            script.Globals["BakeReflections"] = BakeReflections;

            // run the lua code
            script.DoString(Code);
            running = true;

            // extract callbacks
            UpdateFunc = script.Globals.Get("Update");
            FixedUpdateFunc = script.Globals.Get("FixedUpdate");
        }
        public void LuaStop()
        {
            running = false;
        }

        // callbacks
        private void Update()
        {
            if (running && UpdateFunc.Type == DataType.Function)
                script.Call(UpdateFunc, Time.deltaTime);
        }
        private void FixedUpdate()
        {
            if (running && FixedUpdateFunc.Type == DataType.Function)
                script.Call(FixedUpdateFunc, Time.fixedDeltaTime);
        }

        private const int defaultLayerMask = 1 << 0 | 1 << 8 | 1 << 9; // layer 0 is default, layer 8 is player, layer 9 is ground
        private Table Raycast(Vector3 origin, Vector3 direction, float maxDistance=Mathf.Infinity, int layerMask=defaultLayerMask)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit info, maxDistance, layerMask))
            {
                Table result = new Table(script);
                result["point"] = info.point;
                result["distance"] = info.distance;
                result["object"] = info.collider.gameObject;
                result["normal"] = info.normal;
                return result;
            }
            return null;
        }
    }
}
