using System.Collections.Generic;
using UnityEngine;

namespace MCI.GreyBox
{
    /// <summary>
    /// Markiert ein greifbares Grey-Box-Objekt. Merkt sich seinen Startplatz relativ zu seinem
    /// Zuhause (z. B. Fisch → Teich) und kehrt dorthin zurück, wenn es durch den Boden fällt
    /// oder zu weit wegfliegt. Wird das Zuhause umgesetzt, wandert der Startplatz mit.
    /// Im Wasser (siehe ContentZone) schweben Fische, alles andere sinkt gebremst.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GreyBoxItem : MonoBehaviour
    {
        public enum Kind { Fisch, Stein, Pflanze, Rute }

        public Kind kind = Kind.Stein;

        [Tooltip("Wohin das Objekt zurückkehrt (z. B. der Teich). Leer = das Elternobjekt.")]
        public Transform home;

        [Tooltip("Unter dieser Höhe (Meter) wird das Objekt zurückgesetzt.")]
        public float respawnBelowY = -1f;

        [Tooltip("Weiter als so viele Meter vom Zuhause entfernt wird das Objekt zurückgesetzt.")]
        public float respawnDistance = 15f;

        public Rigidbody Body => _rb;

        /// Das Wasser, in dem das Objekt gerade ist (null = an Land).
        public ContentZone Water => _waters.Count > 0 ? _waters[_waters.Count - 1] : null;

        /// Wird gerade zusammen mit Teich, Aquarium oder Kiste umgesetzt (siehe Placeable).
        public bool IsCarried { get; set; }

        Rigidbody _rb;
        Vector3 _homePos;
        Quaternion _homeRot;
        bool _gravity;
        float _linearDamping, _angularDamping;
        readonly List<ContentZone> _waters = new List<ContentZone>(); // Teich und Aquarium können sich berühren

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _gravity = _rb.useGravity;
            _linearDamping = _rb.linearDamping;
            _angularDamping = _rb.angularDamping;
            if (home == null) home = transform.parent;
            _homePos = home.InverseTransformPoint(transform.position);
            _homeRot = Quaternion.Inverse(home.rotation) * transform.rotation;
        }

        public void EnterWater(ContentZone zone)
        {
            _waters.Add(zone);
            _rb.useGravity = _gravity && kind != Kind.Fisch;
            _rb.linearDamping = zone.waterDamping;
            _rb.angularDamping = zone.waterDamping;
        }

        public void ExitWater(ContentZone zone)
        {
            _waters.Remove(zone);
            if (_waters.Count > 0) return;
            _rb.useGravity = _gravity;
            _rb.linearDamping = _linearDamping;
            _rb.angularDamping = _angularDamping;
        }

        void FixedUpdate()
        {
            if (_rb.isKinematic) return; // wird gerade gehalten, mitgetragen oder schwimmt
            var p = transform.position;
            if (p.y < respawnBelowY || (p - home.position).sqrMagnitude > respawnDistance * respawnDistance)
                ResetToStart();
        }

        public void ResetToStart()
        {
            var pos = home.TransformPoint(_homePos);
            var rot = home.rotation * _homeRot;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.position = pos;
            _rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
