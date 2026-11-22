using UnityEngine;

namespace Logistics
{
    /// <summary>Scrolls the water normal map so the river reads as moving.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.004f, 0.018f);
        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
            mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            float t = Time.time;
            mr.GetPropertyBlock(mpb);
            mpb.SetVector(BaseMapST, new Vector4(1f, 1f, speed.x * t, speed.y * t));
            mr.SetPropertyBlock(mpb);
        }
    }
}
