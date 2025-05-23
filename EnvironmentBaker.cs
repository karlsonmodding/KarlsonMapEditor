﻿using System;
using System.Collections;
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
            UpdateEnvironment();
        }

        private IEnumerator UpdateEnvironmentCoroutine()
        {
            DynamicGI.UpdateEnvironment();
            int id = baker.RenderProbe();

            while (!baker.IsFinishedRendering(id))
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
