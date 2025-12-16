using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor.LevelLoader
{
    public static class Main
    {
        public static IPrefabProvider PrefabManager;
        public static Action<string> Logger;
        public static Texture2D[] GameTex;

        public static class Skybox
        {
            public static Material Default;
            public static Material Procedural;
            public static Material SixSided;
        }

        public static void Init(IPrefabProvider prefabProvider, Action<string> logger, Texture2D[] gameTex)
        {
            PrefabManager = prefabProvider;
            Logger = logger;
            GameTex = gameTex;
        }
    }

    public abstract class IPrefabProvider
    {
        public virtual GameObject NewPistol() { return null; }
        public virtual GameObject NewAk47() { return null; }
        public virtual GameObject NewShotgun() { return null; }
        public virtual GameObject NewBoomer() { return null; }
        public virtual GameObject NewGrappler() { return null; }
        public virtual GameObject NewDummyGrappler() { return null; }
        public virtual GameObject NewTable() { return null; }
        public virtual GameObject NewBarrel() { return null; }
        public virtual GameObject NewLocker() { return null; }
        public virtual GameObject NewScreen() { return null; }
        public virtual GameObject NewMilk() { return null; }
        public virtual GameObject NewEnemy() { return null; }
        public virtual GameObject NewGlass() { return null; }
        public virtual PhysicMaterial BounceMaterial() { return null; }
    }
}
