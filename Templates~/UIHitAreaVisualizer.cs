using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Canvas))]
public class UIHitAreaVisualizer : MonoBehaviour
{
    [Tooltip("Color of the debug overlay. Alpha controls transparency.")]
    public Color debugColor = new Color(1f, 0f, 1f, 0.3f);

    [Tooltip("Keyboard key to toggle debug visuals.")]
    public KeyCode toggleKey = KeyCode.F12;

    [Tooltip("Continuously update overlays to match moving/resizing UI.")]
    public bool updateEveryFrame = true;

    private bool isVisible = false;
    private List<DebugOverlay> overlays = new List<DebugOverlay>();
    private Canvas targetCanvas;

    private struct DebugOverlay
    {
        public RectTransform target;   // The RectTransform of the actual Graphic
        public RectTransform overlay;  // The debug overlay RectTransform
        public Graphic graphic;        // Reference to read raycastPadding
    }

    void Awake()
    {
        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("UIHitAreaVisualizer requires a Canvas component on the same GameObject.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDebug();
        }
    }

    void LateUpdate()
    {
        if (isVisible && updateEveryFrame)
        {
            UpdateOverlays();
        }
    }

    void OnDisable()
    {
        HideDebug();
    }

    /// <summary>
    /// Toggle the debug overlays on/off. Wire this to a UI Button in the Inspector.
    /// </summary>
    public void ToggleDebug()
    {
        if (isVisible)
            HideDebug();
        else
            ShowDebug();
    }

    void ShowDebug()
    {
        if (isVisible) return;
        isVisible = true;

        // Clean slate in case a previous session left orphaned data
        if (overlays.Count > 0)
        {
            HideDebug();
            isVisible = true;
        }

        // Find all Selectables under this canvas (including inactive)
        Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
        HashSet<Graphic> seen = new HashSet<Graphic>();
        foreach (Selectable sel in selectables)
        {
            // Find all Graphic components under this Selectable that are raycast targets.
            Graphic[] graphics = sel.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic g in graphics)
            {
                // Skip non-raycast graphics and any already covered by a parent Selectable.
                if (!g.raycastTarget || !seen.Add(g))
                    continue;

                RectTransform targetRt = g.transform as RectTransform;
                if (targetRt == null)
                    continue;

                GameObject go = new GameObject("[HitAreaDebug]");
                go.transform.SetParent(targetRt, false);
                go.transform.SetAsFirstSibling(); // Render behind other children; after parent
                go.layer = targetRt.gameObject.layer;

                RectTransform rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;

                Image img = go.AddComponent<Image>();
                img.color = debugColor;
                img.raycastTarget = false; // Do not block real input

                overlays.Add(new DebugOverlay
                {
                    target = targetRt,
                    overlay = rt,
                    graphic = g
                });
            }
        }

        UpdateOverlays();
    }

    void HideDebug()
    {
        isVisible = false;
        foreach (var o in overlays)
        {
            if (o.overlay != null)
            {
                if (Application.isPlaying)
                    Destroy(o.overlay.gameObject);
                else
                    DestroyImmediate(o.overlay.gameObject);
            }
        }
        overlays.Clear();
    }

    void UpdateOverlays()
    {
        // Remove stale entries for objects destroyed at runtime
        overlays.RemoveAll(o => o.target == null || o.overlay == null || o.graphic == null);

        foreach (var o in overlays)
        {
            // Sync visibility with the target
            bool targetActive = o.target.gameObject.activeInHierarchy;
            if (o.overlay.gameObject.activeSelf != targetActive)
                o.overlay.gameObject.SetActive(targetActive);

            if (!targetActive)
                continue;

            // raycastPadding: x=left, y=bottom, z=right, w=top
            // Negative values = expand outward (larger hit area).
            // Positive values = shrink inward (smaller hit area).
            //
            // RectTransform.offsetMin  -> offset of lower-left corner from the anchor lower-left.
            //   Negative = push edge outward.
            // RectTransform.offsetMax  -> offset of upper-right corner from the anchor upper-right.
            //   Positive = push edge outward.
            //
            // Therefore right/top padding must be inverted for offsetMax.
            Vector4 p = o.graphic.raycastPadding;
            o.overlay.offsetMin = new Vector2(p.x, p.y);
            o.overlay.offsetMax = new Vector2(-p.z, -p.w);
        }
    }
}
