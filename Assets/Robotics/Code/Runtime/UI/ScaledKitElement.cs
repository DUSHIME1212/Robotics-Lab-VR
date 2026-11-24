using UnityEngine;

namespace RoboticsLab.Story
{
    /// <summary>
    /// Hosts a Meta UI Set prefab (authored at its own native unit size, e.g. 48-unit-tall buttons) inside our
    /// larger-unit layouts: the element keeps its native layout and is uniformly scaled to fill this slot.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public class ScaledKitElement : MonoBehaviour
    {
        public RectTransform element;
        public float nativeHeight = 48f;
        [Tooltip("Narrowest native width the element may be squeezed to; limits the scale in narrow slots.")]
        public float minNativeWidth = 96f;

        RectTransform _rt;
        Vector2 _last;

        void OnEnable() { _rt = (RectTransform)transform; Fit(); }
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() { if (_rt && _rt.rect.size != _last) Fit(); }

        public void Fit()
        {
            if (!element) return;
            _rt ??= (RectTransform)transform;
            var size = _rt.rect.size;
            if (size.y <= 0) return;
            _last = size;
            // Fill the slot's height, but never squeeze the element below its minimum native width.
            float k = Mathf.Min(size.y / nativeHeight, size.x / minNativeWidth);
            if (k <= 0) return;
            element.anchorMin = element.anchorMax = element.pivot = new Vector2(0.5f, 0.5f);
            element.anchoredPosition = Vector2.zero;
            element.localScale = new Vector3(k, k, 1);
            element.sizeDelta = new Vector2(size.x / k, nativeHeight);
        }
    }
}
