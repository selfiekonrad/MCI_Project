using Oculus.Interaction;
using UnityEngine;

namespace MCI.GreyBox
{
    /// <summary>
    /// Färbt die weißen Griffe ein, sobald eine Hand in der Nähe ist (hellblau)
    /// oder zugreift (blau). So sieht man sofort, was man gerade greifen würde.
    /// Liegt auf dem Griffe-Objekt neben dem Grabbable und nimmt alle Renderer darunter.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class HandleHighlight : MonoBehaviour
    {
        public Color idle = new Color(0.95f, 0.95f, 0.95f);
        public Color hover = new Color(0.6f, 0.85f, 1f);
        public Color grab = new Color(0.25f, 0.6f, 1f);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        Grabbable _grabbable;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;

        void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            _grabbable.WhenPointerEventRaised += OnPointerEvent;
            Apply();
        }

        void OnDisable()
        {
            _grabbable.WhenPointerEventRaised -= OnPointerEvent;
        }

        void OnPointerEvent(PointerEvent evt) => Apply();

        void Apply()
        {
            var color = _grabbable.SelectingPointsCount > 0 ? grab
                : _grabbable.PointsCount > 0 ? hover
                : idle;
            _block.SetColor(BaseColor, color);
            foreach (var r in _renderers)
                if (r != null) r.SetPropertyBlock(_block);
        }
    }
}
