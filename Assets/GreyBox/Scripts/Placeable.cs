using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using Oculus.Interaction;
using UnityEngine;

namespace MCI.GreyBox
{
    /// <summary>
    /// Teich, Aquarium oder Kiste, die man an den gelben Griffen tragen kann.
    /// Eine Hand: tragen und drehen. Zwei Hände: zusätzlich größer oder kleiner ziehen.
    /// Beim Loslassen rastet das Objekt auf der echten Fläche darunter ein (Boden, Tisch),
    /// erkannt über die Tiefensensoren der Quest 3. Der Inhalt (Fische, Steine, Pflanzen)
    /// wandert beim Tragen mit, behält aber seine Größe.
    /// </summary>
    public class Placeable : MonoBehaviour
    {
        [Tooltip("Griffe mit Grabbable, dessen Ziel dieses Objekt ist.")]
        public Grabbable handles;

        [Tooltip("Wie weit (m) unter dem Objekt nach einer echten Fläche gesucht wird.")]
        public float maxDropDistance = 2.5f;

        [Tooltip("Wird keine Fläche erkannt: Loslassen unter dieser Höhe (m) setzt das Objekt auf den Boden, darüber bleibt es schweben.")]
        public float floorSnapHeight = 0.4f;

        [Tooltip("Dauer (s) der kurzen Absetz-Animation.")]
        public float dropDuration = 0.2f;

        const float MinNormalConfidence = 0.4f;
        const float MinUpDot = 0.7f; // nur waagrechte Flächen (Normale zeigt nach oben)

        struct Carried
        {
            public Rigidbody rb;
            public Grabbable grabbable;
            public GreyBoxItem item;
            public Pose local; // Position im lokalen Raum (inkl. Größe), Drehung relativ
            public RigidbodyInterpolation interpolation;
        }

        readonly List<Carried> _carried = new List<Carried>();
        readonly List<Rigidbody> _found = new List<Rigidbody>();
        ContentZone[] _zones;
        EnvironmentRaycastManager _raycaster;
        Transform _trackingSpace;
        bool _carrying;

        void Awake()
        {
            _zones = GetComponentsInChildren<ContentZone>();
        }

        void Start()
        {
            _raycaster = FindAnyObjectByType<EnvironmentRaycastManager>();
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig != null) _trackingSpace = rig.trackingSpace;
        }

        void OnEnable()
        {
            if (handles != null) handles.WhenPointerEventRaised += OnPointerEvent;
        }

        void OnDisable()
        {
            if (handles != null) handles.WhenPointerEventRaised -= OnPointerEvent;
        }

        void OnPointerEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select && !_carrying)
            {
                BeginCarry();
            }
            else if ((evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
                     && _carrying && handles.SelectingPointsCount == 0)
            {
                _carrying = false;
                StartCoroutine(Drop());
            }
        }

        void BeginCarry()
        {
            _carrying = true;
            StopAllCoroutines(); // falls es gerade noch abgesetzt wird
            if (_carried.Count > 0) return; // Inhalt hängt noch vom letzten Tragen dran

            _found.Clear();
            foreach (var zone in _zones) zone.CollectItems(_found);
            foreach (var rb in _found)
            {
                var g = rb.GetComponent<Grabbable>();
                if (g != null && g.SelectingPointsCount > 0) continue; // hält man gerade in der anderen Hand

                rb.LockKinematic();
                var item = rb.GetComponent<GreyBoxItem>();
                if (item != null) item.IsCarried = true;
                _carried.Add(new Carried
                {
                    rb = rb,
                    grabbable = g,
                    item = item,
                    local = new Pose(transform.InverseTransformPoint(rb.position), Quaternion.Inverse(transform.rotation) * rb.rotation),
                    interpolation = rb.interpolation
                });
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        void LateUpdate()
        {
            for (int i = _carried.Count - 1; i >= 0; i--)
            {
                var c = _carried[i];
                if (c.rb == null) { _carried.RemoveAt(i); continue; }
                if (c.grabbable != null && c.grabbable.SelectingPointsCount > 0)
                {
                    // Jemand hat das Objekt aus dem Teich/Aquarium gegriffen: nicht mehr mittragen
                    Release(c);
                    _carried.RemoveAt(i);
                    continue;
                }
                c.rb.transform.SetPositionAndRotation(transform.TransformPoint(c.local.position), transform.rotation * c.local.rotation);
            }
        }

        IEnumerator Drop()
        {
            var startPos = transform.position;
            var startRot = transform.rotation;
            var endRot = Quaternion.Euler(0f, startRot.eulerAngles.y, 0f);
            var endPos = startPos;

            if (TryFindSurface(startPos, out var surfaceY)) endPos.y = surfaceY;
            else if (startPos.y - FloorY < floorSnapHeight) endPos.y = FloorY;
            // sonst: bleibt schweben, wo man es losgelassen hat

            for (float t = 0f; t < dropDuration; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / dropDuration);
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k), Quaternion.Slerp(startRot, endRot, k));
                yield return null;
            }
            transform.SetPositionAndRotation(endPos, endRot);

            // Inhalt erst loslassen, wenn Wände und Boden an der neuen Stelle angekommen sind
            yield return new WaitForFixedUpdate();
            LateUpdate();
            foreach (var c in _carried) Release(c);
            _carried.Clear();
        }

        static void Release(Carried c)
        {
            if (c.item != null) c.item.IsCarried = false;
            c.rb.interpolation = c.interpolation;
            c.rb.UnlockKinematic();
            if (!c.rb.isKinematic)
            {
                c.rb.linearVelocity = Vector3.zero;
                c.rb.angularVelocity = Vector3.zero;
            }
        }

        bool TryFindSurface(Vector3 pos, out float y)
        {
            y = 0f;
            if (_raycaster == null || !EnvironmentRaycastManager.IsSupported) return false;

            // Strahl senkrecht nach unten, etwas oberhalb des Objekts starten
            const float startAbove = 0.3f;
            var ray = new Ray(pos + Vector3.up * startAbove, Vector3.down);
            if (!_raycaster.Raycast(ray, out var hit, startAbove + maxDropDistance)) return false;
            if (hit.normalConfidence < MinNormalConfidence || Vector3.Dot(hit.normal, Vector3.up) < MinUpDot) return false;

            y = hit.point.y;
            return true;
        }

        float FloorY => _trackingSpace != null ? _trackingSpace.position.y : 0f;
    }
}
