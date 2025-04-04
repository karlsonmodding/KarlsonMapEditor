using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace KarlsonMapEditor
{
    internal class EnvironmentBaker : MonoBehaviour
    {
        private ReflectionProbe baker;
        private Cubemap cubemap;
        private bool inProgress = false;

        void Start()
        {
            baker = gameObject.AddComponent<ReflectionProbe>();
            baker.cullingMask = 0;
            baker.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            baker.mode = ReflectionProbeMode.Realtime;
            baker.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;

            cubemap = new Cubemap(baker.resolution, baker.hdr ? TextureFormat.RGBAHalf : TextureFormat.RGBA32, true);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            StartCoroutine(UpdateEnvironmentCoroutine());
        }

        private IEnumerator UpdateEnvironmentCoroutine()
        {
            DynamicGI.UpdateEnvironment();
            baker.RenderProbe();
            yield return new WaitForEndOfFrame();

            Graphics.CopyTexture(baker.texture, cubemap);
            RenderSettings.customReflection = cubemap;
            inProgress = false;
        }

        public void UpdateEnvironment()
        {
            if (inProgress) return;
            inProgress = true;
            StartCoroutine(UpdateEnvironmentCoroutine());
        }

    }
}
