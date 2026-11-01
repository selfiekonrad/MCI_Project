using System.Collections.Generic;
using UnityEngine;

namespace MCI.GreyBox
{
    /// <summary>
    /// Innenraum (Trigger) von Teich, Aquarium oder Kiste. Merkt sich, welche Objekte darin liegen,
    /// damit sie beim Umsetzen mitwandern.
    /// Als Wasser: Fische schweben bzw. schwimmen (siehe FishSwimmer), Steine und Pflanzen sinken langsam.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ContentZone : MonoBehaviour
    {
        [Tooltip("Wasser: Fische schweben, alles andere sinkt gebremst. Aus: nur Inhalt merken (z. B. Kiste).")]
        public bool water = true;

        [Tooltip("Bremsung im Wasser. Höher = Objekte kommen schneller zur Ruhe.")]
        public float waterDamping = 4f;

        readonly HashSet<GreyBoxItem> _inside = new HashSet<GreyBoxItem>();
        BoxCollider _box;

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
        }

        /// Fügt alle Objekte, die gerade im Bereich liegen, zur Liste hinzu.
        public void CollectItems(List<Rigidbody> into)
        {
            foreach (var item in _inside)
                if (item != null && !into.Contains(item.Body)) into.Add(item.Body);
        }

        /// Zufälliger Punkt im Bereich, mit Abstand (m) zum Rand (nach oben und unten nur halb so viel).
        public Vector3 RandomPoint(float margin)
        {
            var half = HalfExtentsInside(margin);
            var local = _box.center + new Vector3(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y), Random.Range(-half.z, half.z));
            return transform.TransformPoint(local);
        }

        /// Schiebt einen Punkt in den Bereich zurück, mit Abstand (m) zum Rand. Funktioniert auch gedreht und skaliert.
        public Vector3 ClampInside(Vector3 worldPoint, float margin)
        {
            var half = HalfExtentsInside(margin);
            var local = transform.InverseTransformPoint(worldPoint) - _box.center;
            local = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            return transform.TransformPoint(local + _box.center);
        }

        Vector3 HalfExtentsInside(float margin)
        {
            var s = transform.lossyScale;
            var half = _box.size / 2f;
            return new Vector3(Mathf.Max(0f, half.x - margin / s.x), Mathf.Max(0f, half.y - margin / 2f / s.y), Mathf.Max(0f, half.z - margin / s.z));
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var item = ItemOf(other);
            if (item != null && _inside.Add(item) && water) item.EnterWater(this);
        }

        void OnTriggerExit(Collider other)
        {
            var item = ItemOf(other);
            if (item != null && _inside.Remove(item) && water) item.ExitWater(this);
        }

        static GreyBoxItem ItemOf(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return null;
            var item = rb.GetComponent<GreyBoxItem>();
            return item != null && item.kind != GreyBoxItem.Kind.Rute ? item : null;
        }
    }
}
