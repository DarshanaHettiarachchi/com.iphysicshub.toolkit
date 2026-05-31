using UnityEngine;
using UnityEngine.UI;

// Drives the top-right 2D/3D toggle button. Replaces the old gesture triggers on CameraControllerV3:
// one tap calls Toggle2DMode(), and the button icon mirrors the camera's current mode via the
// On2DModeChanged event (so it also stays correct when a double-tap reset exits 2D without a button press).
public class Camera2DToggleUI : MonoBehaviour
{
    [Header("References")]
    public CameraControllerV3 cameraController;
    public Button button;
    public Image iconImage;

    [Header("State Icons")]
    [Tooltip("Shown while in 3D — tap to enter 2D.")]
    public Sprite sprite3D;
    [Tooltip("Shown while in 2D — tap to return to 3D.")]
    public Sprite sprite2D;

    void Start()
    {
        // Fallback so a dropped-in/prefabbed button self-wires to the scene's controller.
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraControllerV3>();

        if (cameraController == null || button == null || iconImage == null)
        {
            Debug.LogWarning("[Camera2DToggleUI] Unassigned reference(s) — assign cameraController, button and iconImage in the Inspector.", this);
            return;
        }

        if (sprite3D == null || sprite2D == null)
            Debug.LogWarning("[Camera2DToggleUI] sprite3D and/or sprite2D is unassigned — the icon will appear blank for that state.", this);

        button.onClick.AddListener(cameraController.Toggle2DMode);
        cameraController.On2DModeChanged += OnModeChanged;
        OnModeChanged(cameraController.Is2DMode); // initial icon
    }

    void OnDestroy()
    {
        if (button != null && cameraController != null)
            button.onClick.RemoveListener(cameraController.Toggle2DMode);
        if (cameraController != null)
            cameraController.On2DModeChanged -= OnModeChanged;
    }

    void OnModeChanged(bool is2D)
    {
        iconImage.sprite = is2D ? sprite2D : sprite3D;
    }
}
