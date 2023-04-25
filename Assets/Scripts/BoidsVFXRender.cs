using System.Collections;
using System.Collections.Generic;
using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;
using UnityEngine;
using UnityEngine.VFX;

namespace BoidsSimulationOnGPU
{
    [RequireComponent(typeof(GPUBoids))]
    public class BoidsVFXRender : MonoBehaviour, IGUIUser
    {
        [SerializeField] private VisualEffect graph;

        public GPUBoids GPUBoidsScript;
        private PrefsVector3 objectScale;
        private PrefsFloat scaler;

        private bool initialized = false;

        private void Update()
        {
            if (!initialized)
            {
                var buffer = GPUBoidsScript.GetBoidDataBuffer();
                if (buffer != null)
                {
                    graph.SetGraphicsBuffer("Boids", buffer);
                    initialized = true;
                }
            }

            graph.SetVector3("ObjectScale", objectScale);
            graph.SetFloat("Scaler", scaler);
        }

        public string GetName() => "BoidsRender";

        public void ShowGUI()
        {
            objectScale.DoGUI();
            scaler.DoGUI();
        }

        public void SetupGUI()
        {
            objectScale = new PrefsVector3("ObjectScale", new Vector3(1f, 5f, 2f));
            scaler = new PrefsFloat("Scaler", 0.7f);
        }
    }
}