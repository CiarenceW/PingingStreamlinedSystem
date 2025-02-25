using UnityEngine;

namespace PingingStreamlinedSystem
{
    public class Ping : MonoBehaviour
    {
        private void Awake()
        {
            Destroy(this, PingingStreamlinedSystemPlugin.Options.pingDuration.Value);
        }

        private void Update()
        {
            Rect rect = new Rect((Vector2)transform.position - Vector2.one, Vector2.one);

            Gizmos.DrawGUITexture(rect, PingingStreamlinedSystemPlugin.Instance.pingTexture);
        }
    }
}