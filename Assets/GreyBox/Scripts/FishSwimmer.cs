using Oculus.Interaction;
using UnityEngine;

namespace MCI.GreyBox
{
    /// <summary>
    /// Lässt einen Fisch aus "Fish Alive" im Wasser (Teich, Aquarium) selbst herumschwimmen.
    /// Fish Alive (FishMotion) bewegt das Modell; dieses Skript überträgt die Bewegung auf das
    /// greifbare Objekt, wählt immer neue Ziele im Wasser und hält den Fisch darin.
    /// Gegriffen, mitgetragen oder an Land schwimmt der Fisch nicht.
    /// FishMotion wird über den Namen angesprochen, damit das Projekt auch ohne das Paket kompiliert.
    /// </summary>
    [RequireComponent(typeof(GreyBoxItem))]
    public class FishSwimmer : MonoBehaviour
    {
        [Tooltip("Modell mit Animator und FishMotion (Fish Alive).")]
        public Transform model;

        [Tooltip("Abstand (m), den der Fisch zu Glas, Boden und Wasseroberfläche hält.")]
        public float margin = 0.07f;

        [Tooltip("Nach so vielen Sekunden (zufällig dazwischen) schwimmt der Fisch ein neues Ziel an.")]
        public Vector2 newTargetEvery = new Vector2(2f, 5f);

        Behaviour _motion;
        System.Reflection.FieldInfo _targetField;
        GreyBoxItem _item;
        Grabbable _grabbable;
        Rigidbody _rb;
        Vector3 _restPos;
        Quaternion _restRot;
        Transform _target;
        ContentZone _zone;
        bool _swimming;
        float _nextTarget;

        void Awake()
        {
            _item = GetComponent<GreyBoxItem>();
            _grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();
            if (model == null) return;

            _restPos = model.localPosition;
            _restRot = model.localRotation;
            foreach (var mb in model.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "FishMotion") _motion = mb;
            if (_motion == null) return;

            _targetField = _motion.GetType().GetField("target");
            _motion.enabled = false;
            _target = new GameObject($"Schwimmziel ({name})").transform;
        }

        void OnDestroy()
        {
            if (_target != null) Destroy(_target.gameObject);
        }

        void Update()
        {
            if (_motion == null) return;

            var zone = _item.Water;
            var held = _grabbable != null && _grabbable.SelectingPointsCount > 0;
            var shouldSwim = zone != null && !held && !_item.IsCarried;

            if (shouldSwim && (!_swimming || zone != _zone)) StartSwimming(zone);
            else if (!shouldSwim && _swimming) StopSwimming();

            if (_swimming && (Time.time > _nextTarget || (_target.position - transform.position).sqrMagnitude < 0.01f))
                PickTarget();
        }

        void LateUpdate()
        {
            if (!_swimming) return;

            // Bewegung des Modells auf das greifbare Objekt übertragen und im Wasser halten
            var rot = model.rotation * Quaternion.Inverse(_restRot);
            var pos = _zone.ClampInside(model.position - rot * _restPos, margin);
            transform.SetPositionAndRotation(pos, rot);
            model.localPosition = _restPos;
            model.localRotation = _restRot;
        }

        void StartSwimming(ContentZone zone)
        {
            if (!_swimming) _rb.LockKinematic();
            _swimming = true;
            _zone = zone;
            _target.SetParent(zone.transform, true); // wandert mit, wenn das Aquarium umgesetzt wird
            PickTarget();
            _targetField?.SetValue(_motion, _target.gameObject);
            _motion.enabled = true;
            _motion.SendMessage("SetAutoMotion", true, SendMessageOptions.DontRequireReceiver);
        }

        void StopSwimming()
        {
            _swimming = false;
            _motion.enabled = false;
            _rb.UnlockKinematic();
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        void PickTarget()
        {
            _target.position = _zone.RandomPoint(margin);
            _nextTarget = Time.time + Random.Range(newTargetEvery.x, newTargetEvery.y);
        }
    }
}
