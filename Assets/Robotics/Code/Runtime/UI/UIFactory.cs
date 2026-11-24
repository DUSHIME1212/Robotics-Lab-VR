using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RoboticsLab.Story
{
    public enum ButtonStyle { Primary, Secondary, Destructive, Borderless }

    /// <summary>
    /// Builds controller-friendly world-space UI at runtime. When the Meta UI Set (Interaction SDK) is available
    /// via <see cref="MetaUIKitLibrary"/>, panels use Meta's Quest theme (backplate, fonts, text colours) and
    /// buttons/sliders are instantiated from the official UI Set prefabs; otherwise plain uGUI is used.
    /// All canvases keep XRI's TrackedDeviceGraphicRaycaster, so the existing XR Origin rays drive them.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Panel = new(0.05f, 0.07f, 0.1f, 0.93f);
        public static readonly Color Accent = new(0.4f, 0.85f, 1f);
        public static readonly Color Amber = new(1f, 0.72f, 0.2f);
        public static readonly Color Green = new(0.35f, 1f, 0.5f);
        public static readonly Color ButtonColor = new(0.18f, 0.45f, 0.75f);

        static MetaUIKitLibrary Kit => MetaUIKitLibrary.Instance;
        public static bool UsingMetaKit => Kit != null && Kit.theme != null;

        public static Color TextPrimary => UsingMetaKit ? Kit.theme.textPrimaryColor : new Color(0.92f, 0.92f, 0.92f);
        public static Color TextSecondary => UsingMetaKit ? Kit.theme.textSecondaryColor : new Color(0.7f, 0.72f, 0.75f);

        public static RectTransform Canvas(Transform parent, string name, Vector2 sizePx, float metersPerPx = 0.001f, bool interactive = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            if (interactive) go.AddComponent<TrackedDeviceGraphicRaycaster>();
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * metersPerPx;
            return rt;
        }

        /// <summary>Panel backplate. Uses the Meta theme's backplate colour/gradient when the kit is present.</summary>
        public static RectTransform Background(RectTransform parent, Color c, int pad = 24, float spacing = 10)
        {
            var go = new GameObject("Backplate", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            StyleBackplate(go.GetComponent<Image>(), c);
            var r = (RectTransform)go.transform;
            Stretch(r);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            return r;
        }

        public static void StyleBackplate(Image img, Color fallback)
        {
            img.raycastTarget = false;
            if (!UsingMetaKit) { img.color = fallback; return; }
            var t = Kit.theme;
            img.color = new Color(t.backplateColor.r, t.backplateColor.g, t.backplateColor.b, Mathf.Max(0.92f, t.backplateColor.a));
            if (t.backplateGradientMaterial)
            {
                img.material = t.backplateGradientMaterial;
                // The RoundedPanelUI shader reads corner radii from the mesh, written by this Meta UI Set component.
                var rounded = img.GetComponent<Oculus.Interaction.Samples.RoundedBoxUIProperties>()
                              ?? img.gameObject.AddComponent<Oculus.Interaction.Samples.RoundedBoxUIProperties>();
                rounded.borderRadius = Vector4.one * 24f;
            }
        }

        public static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        public static TMP_Text Text(Transform parent, string s, float size, Color c, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            ApplyFont(t, style);
            t.text = s; t.fontSize = size; t.color = c; t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>Meta theme fonts: bold for headings, regular for body.</summary>
        public static void ApplyFont(TMP_Text t, FontStyles style)
        {
            t.fontStyle = style;
            if (!UsingMetaKit) return;
            var theme = Kit.theme;
            var font = (style & FontStyles.Bold) != 0 ? theme.textFontBold : theme.textFontRegular;
            if (font)
            {
                t.font = font;
                t.fontStyle = style & ~FontStyles.Bold; // weight comes from the font asset itself
            }
        }

        public static RectTransform Row(Transform parent, float height = 56)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            go.GetComponent<LayoutElement>().minHeight = height;
            return (RectTransform)go.transform;
        }

        /// <summary>Legacy signature: maps the old colour hint to a Meta button style.</summary>
        public static Button Button(Transform parent, string label, UnityAction onClick, float fontSize = 22, Color? color = null)
        {
            var style = ButtonStyle.Primary;
            if (color.HasValue)
            {
                var c = color.Value;
                if (c.r > c.g + 0.15f && c.r > c.b + 0.15f && c.g < 0.3f) style = ButtonStyle.Destructive;
                else if (Mathf.Max(c.r, c.g, c.b) < 0.5f) style = ButtonStyle.Secondary;
            }
            return Button(parent, label, onClick, style, fontSize);
        }

        public static Button Button(Transform parent, string label, UnityAction onClick, ButtonStyle style, float fontSize = 22)
        {
            var prefab = !UsingMetaKit ? null : style switch
            {
                ButtonStyle.Secondary => Kit.secondaryButton,
                ButtonStyle.Destructive => Kit.destructiveButton,
                ButtonStyle.Borderless => Kit.borderlessButton,
                _ => Kit.primaryButton,
            };
            if (prefab) return MetaButton(parent, prefab, label, onClick, fontSize);

            // Fallback: plain uGUI button.
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = style == ButtonStyle.Destructive ? new Color(0.6f, 0.2f, 0.2f) : style == ButtonStyle.Secondary ? new Color(0.25f, 0.3f, 0.38f) : ButtonColor;
            go.GetComponent<LayoutElement>().minHeight = 56;
            var b = go.GetComponent<Button>();
            if (onClick != null) b.onClick.AddListener(onClick);
            var t = Text(go.transform, label, fontSize, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            t.name = "Label";
            Stretch((RectTransform)t.transform);
            return b;
        }

        const float ButtonSlotHeight = 56f, ButtonNativeHeight = 48f;

        static Button MetaButton(Transform parent, GameObject prefab, string label, UnityAction onClick, float fontSize)
        {
            var slot = KitSlot(parent, label, ButtonSlotHeight);
            var scaler = Scaler(slot, new Vector2(133, ButtonNativeHeight));
            var go = Object.Instantiate(prefab, scaler, false);
            StretchChild((RectTransform)go.transform);
            go.name = "MetaButton";
            // Text-only variant of the IconAndLabel prefab: hide icon, gap and subtitle; centre the label.
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Icon" || t.name == "Subtitle") t.gameObject.SetActive(false);
            var labelText = FindLabel(go);
            float k = ButtonSlotHeight / ButtonNativeHeight;
            if (labelText)
            {
                labelText.text = label;
                labelText.fontSize = fontSize / k; // the whole element is scaled by k
                labelText.enableAutoSizing = false;
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                labelText.alignment = TextAlignmentOptions.Center;
            }
            var fit = slot.GetComponent<ScaledKitElement>();
            fit.element = scaler; fit.nativeHeight = ButtonNativeHeight; fit.Fit();
            var b = go.GetComponentInChildren<Button>(true);
            if (b && onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        /// <summary>
        /// Intermediate rect that carries the uniform scale. Meta UI Set prefabs animate their own root
        /// (hover/press) with an Animator, so the scale must live one level above them.
        /// </summary>
        static RectTransform Scaler(Transform slot, Vector2 nativeSize)
        {
            var s = new GameObject("KitScaler", typeof(RectTransform));
            s.transform.SetParent(slot, false);
            var rt = (RectTransform)s.transform;
            rt.sizeDelta = nativeSize;
            return rt;
        }

        static void StretchChild(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one;
        }

        /// <summary>A layout slot that uniformly scales a Meta UI Set element to fill it.</summary>
        static RectTransform KitSlot(Transform parent, string name, float height)
        {
            var slot = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(ScaledKitElement));
            slot.transform.SetParent(parent, false);
            var le = slot.GetComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleWidth = 1; le.minWidth = 80;
            return (RectTransform)slot.transform;
        }

        static TMP_Text FindLabel(GameObject go)
        {
            TMP_Text best = null;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.transform.name == "Subtitle" || t.transform.parent && t.transform.parent.name == "Subtitle") continue;
                if (t.name == "Text" || t.name == "Label") return t;
                best ??= t;
            }
            return best;
        }

        /// <summary>Slider from the Meta UI Set (falls back to a uGUI default slider).</summary>
        public static Slider Slider(Transform parent, float min, float max, float value)
        {
            Slider s;
            if (UsingMetaKit && Kit.slider)
            {
                var slot = KitSlot(parent, "Slider", 34f);
                var nativeSize = ((RectTransform)Kit.slider.transform).sizeDelta; // MediumSlider is ~225 x 28
                var scaler = Scaler(slot, nativeSize);
                var go = Object.Instantiate(Kit.slider, scaler, false);
                StretchChild((RectTransform)go.transform);
                var fit = slot.GetComponent<ScaledKitElement>();
                fit.element = scaler; fit.nativeHeight = nativeSize.y > 0 ? nativeSize.y : 28f; fit.Fit();
                s = go.GetComponentInChildren<Slider>(true);
            }
            else
            {
                var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
                go.transform.SetParent(parent, false);
                go.AddComponent<LayoutElement>().minHeight = 30;
                foreach (var img in go.GetComponentsInChildren<Image>()) img.color = img.name == "Fill" ? Accent : img.name == "Handle" ? Color.white : new Color(0.25f, 0.28f, 0.33f);
                s = go.GetComponent<Slider>();
            }
            s.wholeNumbers = false;
            s.minValue = min; s.maxValue = max;
            s.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            return s;
        }

        public static void SetLabel(Button b, string s)
        {
            var t = FindLabel(b.gameObject);
            if (t) t.text = s;
        }
    }
}
