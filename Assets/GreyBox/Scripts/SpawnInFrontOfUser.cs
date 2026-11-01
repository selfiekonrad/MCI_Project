using System.Collections;
using UnityEngine;

namespace MCI.GreyBox
{
    /// <summary>
    /// Stellt dieses Objekt beim Start dorthin, wo du stehst, und dreht es in deine Blickrichtung.
    /// So liegt die Grey-Box immer vor dir, egal wo im Raum du die App startest.
    /// </summary>
    public class SpawnInFrontOfUser : MonoBehaviour
    {
        [Tooltip("So lange (s) wird höchstens gewartet, bis das Headset-Tracking läuft.")]
        public float maxWait = 2f;

        IEnumerator Start()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) yield break;

            var head = rig.centerEyeAnchor;
            for (float t = 0f; t < maxWait && head.localPosition == Vector3.zero; t += Time.deltaTime)
                yield return null;
            yield return null;

            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            var floor = rig.trackingSpace.position.y;
            transform.SetPositionAndRotation(new Vector3(head.position.x, floor, head.position.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));
        }
    }
}
