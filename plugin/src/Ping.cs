using UnityEngine;

namespace PingingStreamlinedSystem
{
    public class Ping : MonoBehaviour
    {
        static Vector2 size = Vector2.one;

        private void Awake()
        {
            Destroy(this.gameObject, PingingStreamlinedSystemPlugin.Options.pingDuration.Value);

            DestroyImmediate(this.GetComponent<MeshCollider>());

            var renderer = this.GetComponent<MeshRenderer>();
            var mat = new Material(PingingStreamlinedSystemPlugin.Instance.pingMat);
            renderer.material = mat;
            renderer.material.mainTexture = PingingStreamlinedSystemPlugin.Instance.pingTexture;
        }

        private void Update()
        {
            this.transform.LookAt((this.transform.position - Camera.main.transform.position) + this.transform.position, Vector3.up);
        }
    }
}