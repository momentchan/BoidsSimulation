using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace BoidsSimulationOnGPU
{
    [RequireComponent(typeof(GPUBoids))]
    public class BoidsVFXRender : MonoBehaviour
    {

        [SerializeField] private VisualEffect graph;

        public GPUBoids GPUBoidsScript;
        public Vector3 ObjectScale = new Vector3(0.1f, 0.2f, 0.5f);

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

            graph.SetVector3("ObjectScale", ObjectScale);
        }
    }
}