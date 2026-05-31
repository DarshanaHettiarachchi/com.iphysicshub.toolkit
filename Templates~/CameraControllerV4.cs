using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraControllerV4 : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object to orbit around. If null, orbits around world origin plus offset.")]
    public Transform target;
    [Tooltip("Offset from the target transform (or world origin if target is null).")]
    public Vector3 targetOffset;

    [Header("Distance")]
    [Tooltip("Initial distance from the target.")]
    public float distance = 10f;

    public float minDistance = 2f;
    public float maxDistance = 50f;

    [Header("Rotation")]
    [Tooltip("Rotation sensitivity. 1.0 = a swipe across the screen height rotates 360°.")]
    public float rotationSpeed = 1.0f;
    [Tooltip("Lowest vertical angle.")]
    public float minPitch = -89f;
    [Tooltip("Highest vertical angle.")]
    public float maxPitch = 89f;
    public bool invertX = false;
    public bool invertY = false;

    [Header("Pan")]
    [Tooltip("Panning sensitivity. 1.0 = exact 1:1 finger-to-world tracking at the focal plane (FOV-aware).")]
    public float panSpeed = 1f;

    [Header("Zoom")]
    [Tooltip("Mouse scroll wheel sensitivity. 1.0 = ~5% distance change per scroll notch (multiplicative).")]
    public float zoomSpeedMouse = 1.0f;
    [Tooltip("Touch pinch sensitivity. 1.0 = perfect 1:1 pinch-to-distance ratio.")]
    public float zoomSpeedTouch = 1.0f;

    [Header("Smoothing")]
    [Tooltip("Damping for rotation (yaw + pitch). ~100ms = Sketchfab feel.")]
    public float rotateSmoothTime = 0.10f;
    [Tooltip("Damping for zoom (distance). Slightly slower than rotation for weight.")]
    public float zoomSmoothTime = 0.12f;
    [Tooltip("Damping for pan (target position). Near-instant so world tracks finger.")]
    public float panSmoothTime = 0.03f;

    [Header("Double-Tap Reset")]
    [Tooltip("Enable double-click / double-tap to reset the camera to its starting pivot and distance.")]
    public bool enableFocus = true;

    [Header("UI Blocking")]
    [Tooltip("Ignore input when clicking/touching UI.")]
    public bool blockWhenOverUI = true;

    [Header("3D Object Blocking")]
    [Tooltip("Ignore input when clicking/touching 3D objects (requires colliders).")]
    public bool blockWhenOver3DObject = false;
    [Tooltip("Layer mask to check for 3D objects. Only used if blockWhenOver3DObject is true.")]
    public LayerMask blockingLayers = -1; // Everything by default

    [Header("Debug")]
    [Tooltip("Show the on-screen FPS / DPI / Touches overlay.")]
    public bool showDebugOverlay = false;

    // --- Runtime State ---

    private Camera _cam;

    // Target values (driven by input)
    private float _targetYaw;
    private float _targetPitch;
    private float _targetDistance;
    private Vector3 _targetPosition;

    // Current smoothed values
    private float _currentYaw;
    private float _currentPitch;
    private float _currentDistance;
    private Vector3 _currentPosition;

    // SmoothDamp velocities
    private float _yawVelocity;
    private float _pitchVelocity;
    private float _distanceVelocity;
    private Vector3 _positionVelocity;

    // Input tracking
    private Vector3 _lastMousePos;
    private bool _isOrbiting;
    private bool _isPanning;
    private bool _touchOrbitActive;
    private bool _pinchActive;
    private float _lastPinchDistance;
    private Vector2 _lastTouchMidpoint;
    private int _lastTouchCount;

    // Cached UI raycast scratch (avoid per-frame GC)
    private PointerEventData _uiEventData;
    private EventSystem _uiEventSystem;
    private List<RaycastResult> _uiRaycastResults;

    // Cached 3D object raycast
    private RaycastHit _raycastHit;

    // Cached debug overlay style (built once on first OnGUI)
    private GUIStyle _debugStyle;

    // Apply global Input/framerate config exactly once across all instances.
    private static bool _globalSettingsApplied;

    // Canonical world axes for 2D auto-detect. Static so we don't allocate a fresh array on every toggle.
    private static readonly Vector3[] CANONICAL_AXES =
    {
        Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
    };

    // Original camera pose for double-tap reset
    private Vector3 _originalTargetPosition;
    private float _originalDistance;
    private float _originalYaw;
    private float _originalPitch;

    // Double click / tap
    private float _lastClickTime;
    private int _clickCount;
    private Vector2 _lastClickPosition;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private const float DOUBLE_CLICK_DISTANCE_THRESHOLD = 30f;

    // --- 2D Orthographic toggle ---
    // Active when the camera is locked to an axis-aligned ortho view.
    private bool _is2DMode;

    // Read-only state + change notification for UI (e.g. Camera2DToggleUI button icon).
    // Fires on every Enter2DMode/Exit2DMode, so it also covers the double-tap reset path (TryFocus -> Exit2DMode).
    public bool Is2DMode => _is2DMode;
    public event System.Action<bool> On2DModeChanged; // arg = is now in 2D mode

    // Smoothed orthographic size, mirroring the existing distance smoothing model.
    private float _targetOrthoSize;
    private float _currentOrthoSize;
    private float _orthoSizeVelocity;

    // Locked Euler angles applied every frame while in 2D mode so user orbit input cannot drift the view.
    private float _lockedYaw;
    private float _lockedPitch;

    // Saved pitch clamps — widened to ±90° while in 2D so top/bottom auto-detect snaps land exactly.
    private float _savedMinPitch;
    private float _savedMaxPitch;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        ApplyGlobalSettingsOnce();
        _uiRaycastResults = new List<RaycastResult>(8);
    }

    static void ApplyGlobalSettingsOnce()
    {
        if (_globalSettingsApplied) return;
        // Stop a single WebGL touch from firing both touch and simulated-mouse events.
        Input.simulateMouseWithTouches = false;
        // Keep WebGL framerate steady; default can be -1 / vsync-only on browsers.
        Application.targetFrameRate = 60;
        _globalSettingsApplied = true;
    }

    void Start()
    {
        // Initialize target position
        _targetPosition = GetTargetWorldPosition();

        // Initialize from current camera transform to avoid jumps on start
        Vector3 toCamera = transform.position - _targetPosition;
        _targetDistance = _currentDistance = Mathf.Clamp(toCamera.magnitude, minDistance, maxDistance);
        distance = _targetDistance;

        Quaternion lookRot = Quaternion.LookRotation(_targetPosition - transform.position);
        _targetYaw = _currentYaw = lookRot.eulerAngles.y;
        _targetPitch = _currentPitch = Mathf.Clamp(NormalizeAngle(lookRot.eulerAngles.x), minPitch, maxPitch);

        _currentPosition = _targetPosition;

        // Store original values for double-tap reset
        _originalTargetPosition = _targetPosition;
        _originalDistance = distance;
        _originalYaw = _targetYaw;
        _originalPitch = _targetPitch;
    }

    void Update()
    {
        // Blocking is checked at input-start (mouse-down / touch-began / scroll) inside
        // the handlers, not every frame, so an in-progress drag is never interrupted by
        // the cursor passing over UI mid-gesture (standard 3D-viewer convention).
        // Mutually exclusive paths: WebGL forwards a finger as both a touch and a mouse event.
        if (Input.touchCount > 0)
            HandleTouchInput();
        else
            HandleMouseInput();
    }

    void LateUpdate()
    {
        ApplyCameraTransform();
    }

    #region Initialization Helpers

    Vector3 GetTargetWorldPosition()
    {
        if (target != null)
            return target.position + targetOffset;
        return targetOffset; // effectively world space origin + offset
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region Input Blocking Logic

    // Per-gesture blocking is applied inline at each input down-edge:
    //   - Orbit / single-finger drag: blocked by interactive UI AND by 3D objects on blockingLayers.
    //   - Pan / zoom / pinch: blocked by interactive UI only (3D objects pass through).
    //   - Double-tap focus: suppressed over interactive UI only (fires over 3D objects).

    bool PointerIsOverInteractiveUI(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null)
            return false;

        // Lazy-init + rebind if the active EventSystem has changed (scene swap, additive load).
        if (_uiEventData == null || _uiEventSystem != es)
        {
            _uiEventData = new PointerEventData(es);
            _uiEventSystem = es;
        }

        _uiEventData.position = screenPos;
        _uiRaycastResults.Clear();
        es.RaycastAll(_uiEventData, _uiRaycastResults);

        for (int i = 0; i < _uiRaycastResults.Count; i++)
        {
            RaycastResult r = _uiRaycastResults[i];
            // Only block on interactive UI elements (buttons, sliders, etc).
            // Ignore background Images/RawImages and Physics raycasters.
            if (r.module is GraphicRaycaster && HasInteractiveUIComponent(r.gameObject))
                return true;
        }
        return false;
    }

    bool HasInteractiveUIComponent(GameObject go)
    {
        Transform t = go.transform;
        while (t != null)
        {
            // Block on any UI panel explicitly tagged as a blocker
            if (t.CompareTag("UIBlocker"))
                return true;

            // Only block on enabled, interactable Selectables — a disabled Button shouldn't eat input.
            Selectable sel = t.GetComponent<Selectable>();
            if (sel != null && sel.IsInteractable())
                return true;
            if (t.GetComponent<ScrollRect>() != null)
                return true;
            t = t.parent;
        }
        return false;
    }

    bool IsPointerOver3DObject(Vector2 screenPos)
    {
        Ray ray = _cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out _raycastHit, Mathf.Infinity, blockingLayers))
        {
            // Optional: Filter out specific tags or components if needed
            // Example: if (_raycastHit.collider.CompareTag("IgnoreCamera")) return false;
            return true;
        }
        return false;
    }

    #endregion

    #region Input Handling

    void HandleMouseInput()
    {
        // --- Orbit (Left Mouse) ---
        // Gate on blocking only at the down-edge; once a drag starts it runs to button-up.
        if (Input.GetMouseButtonDown(0))
        {
            // Double-tap is suppressed over interactive UI but still fires over 3D objects.
            bool uiBlocked = blockWhenOverUI && PointerIsOverInteractiveUI(Input.mousePosition);
            // A press over UI breaks any pending double-tap chain so it can't bridge two off-UI taps.
            if (uiBlocked)
                _clickCount = 0;
            bool didFocus = enableFocus && !uiBlocked && RegisterClick(Input.mousePosition);

            // Orbit is blocked by UI and by 3D objects; don't re-arm it if a focus reset just fired.
            if (!didFocus && !uiBlocked && !(blockWhenOver3DObject && IsPointerOver3DObject(Input.mousePosition)))
            {
                _lastMousePos = Input.mousePosition;
                _isOrbiting = true;
            }
        }

        if (Input.GetMouseButton(0) && _isOrbiting)
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            if (_is2DMode)
            {
                // In 2D, rotation is locked — repurpose left-drag as a pan so the gesture stays useful (Sketchfab / CAD convention).
                float w = WorldPerPixel() * panSpeed;
                _targetPosition -= transform.right * (delta.x * w);
                _targetPosition -= transform.up * (delta.y * w);
            }
            else
            {
                float dirX = invertX ? -1f : 1f;
                float dirY = invertY ? -1f : 1f;
                float k = DegPerPixel() * rotationSpeed;

                _targetYaw += delta.x * k * dirX;
                _targetPitch -= delta.y * k * dirY;
                _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
            }
            _lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isOrbiting = false;
        }

        // --- Pan (Right Mouse) ---
        if (Input.GetMouseButtonDown(1) && !(blockWhenOverUI && PointerIsOverInteractiveUI(Input.mousePosition)))
        {
            _lastMousePos = Input.mousePosition;
            _isPanning = true;
        }

        if (Input.GetMouseButton(1) && _isPanning)
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            float w = WorldPerPixel() * panSpeed;

            _targetPosition -= transform.right * (delta.x * w);
            _targetPosition -= transform.up * (delta.y * w);

            _lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
        {
            _isPanning = false;
        }

        // --- Zoom (Scroll Wheel) ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f && !(blockWhenOverUI && PointerIsOverInteractiveUI(Input.mousePosition)))
        {
            // ~5% per notch baseline (Three.js convention), scaled by zoomSpeedMouse.
            float zoomFactor = Mathf.Pow(0.95f, scroll * zoomSpeedMouse * 10f);
            if (_is2DMode)
            {
                _targetOrthoSize = Mathf.Clamp(_targetOrthoSize * zoomFactor, GetMinOrthoSize(), GetMaxOrthoSize());
            }
            else
            {
                _targetDistance = Mathf.Clamp(_targetDistance * zoomFactor, minDistance, maxDistance);
            }
        }
    }

    void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 0)
        {
            _lastTouchCount = 0;
            _touchOrbitActive = false;
            _pinchActive = false;
            return;
        }

        // Touch count changed (e.g. 2 -> 1). Stale deltas would jump the view; clear them.
        if (touchCount != _lastTouchCount)
        {
            _lastPinchDistance = 0f;
            _lastTouchMidpoint = Vector2.zero;
            // Gaining a second finger cancels the single-finger orbit and zeroes the tap counter, so a pending
            // single-finger tap can't complete a stray focus double-tap once the pinch gesture takes over.
            if (touchCount == 2)
            {
                _clickCount = 0;
                _touchOrbitActive = false;
            }
        }

        // --- Single Touch: Orbit ---
        if (touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // Double-tap is suppressed over interactive UI but still fires over 3D objects.
                bool uiBlocked = blockWhenOverUI && PointerIsOverInteractiveUI(touch.position);
                // A touch over UI breaks any pending double-tap chain so it can't bridge two off-UI taps.
                if (uiBlocked)
                    _clickCount = 0;
                bool didFocus = enableFocus && !uiBlocked && RegisterClick(touch.position);

                // Orbit is blocked by UI and by 3D objects; don't re-arm it if a focus reset just fired.
                _touchOrbitActive = !didFocus && !uiBlocked
                    && !(blockWhenOver3DObject && IsPointerOver3DObject(touch.position));
            }

            if (touch.phase == TouchPhase.Moved && _touchOrbitActive)
            {
                Vector2 delta = touch.deltaPosition;
                if (_is2DMode)
                {
                    // In 2D, rotation is locked — repurpose single-finger drag as a pan.
                    float w = WorldPerPixel() * panSpeed;
                    _targetPosition -= transform.right * (delta.x * w);
                    _targetPosition -= transform.up * (delta.y * w);
                }
                else
                {
                    float dirX = invertX ? -1f : 1f;
                    float dirY = invertY ? -1f : 1f;
                    float k = DegPerPixel() * rotationSpeed;

                    _targetYaw += delta.x * k * dirX;
                    _targetPitch -= delta.y * k * dirY;
                    _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
                }
            }
        }

        // --- Two Touches: Pinch zoom + midpoint pan (concurrent) ---
        if (touchCount == 2)
        {
            Touch t1 = Input.GetTouch(0);
            Touch t2 = Input.GetTouch(1);

            float currentDist = Vector2.Distance(t1.position, t2.position);
            Vector2 currMid = (t1.position + t2.position) * 0.5f;

            bool justStarted = _lastTouchCount < 2
                || t1.phase == TouchPhase.Began
                || t2.phase == TouchPhase.Began;

            if (justStarted)
            {
                // If either finger began over interactive UI, ignore this whole pinch sequence.
                // (3D objects never block pinch — zoom/pan pass through.)
                if (blockWhenOverUI && (PointerIsOverInteractiveUI(t1.position) || PointerIsOverInteractiveUI(t2.position)))
                {
                    _pinchActive = false;
                }
                else
                {
                    _pinchActive = true;
                    // Seed the trackers; defer the first delta until next frame.
                    _lastPinchDistance = currentDist;
                    _lastTouchMidpoint = currMid;
                }
            }
            else if (_pinchActive)
            {
                const float DEADZONE = 2f; // pixels

                // Pinch zoom — multiplicative. Distance follows the inverse of the pinch-distance ratio.
                float pinchPxDelta = currentDist - _lastPinchDistance;
                if (Mathf.Abs(pinchPxDelta) > DEADZONE)
                {
                    float ratio = _lastPinchDistance / Mathf.Max(1f, currentDist);
                    float zoomFactor = Mathf.Pow(ratio, zoomSpeedTouch);
                    if (_is2DMode)
                    {
                        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize * zoomFactor, GetMinOrthoSize(), GetMaxOrthoSize());
                    }
                    else
                    {
                        _targetDistance = Mathf.Clamp(_targetDistance * zoomFactor, minDistance, maxDistance);
                    }
                }

                // Midpoint pan — FOV-aware. Runs alongside pinch in the same frame (industry convention).
                Vector2 midDelta = currMid - _lastTouchMidpoint;
                if (midDelta.magnitude > DEADZONE)
                {
                    float w = WorldPerPixel() * panSpeed;
                    _targetPosition -= transform.right * (midDelta.x * w);
                    _targetPosition -= transform.up * (midDelta.y * w);
                }

                _lastPinchDistance = currentDist;
                _lastTouchMidpoint = currMid;
            }
        }

        _lastTouchCount = touchCount;
    }

    // Returns true if this click completed a double-tap and triggered a focus reset.
    // Callers use that to avoid re-arming an orbit on the same event (TryFocus cancels gestures).
    bool RegisterClick(Vector2 screenPos)
    {
        float timeSinceLast = Time.time - _lastClickTime;
        bool withinTime = timeSinceLast <= DOUBLE_CLICK_THRESHOLD;
        bool withinDistance = Vector2.Distance(screenPos, _lastClickPosition) < DOUBLE_CLICK_DISTANCE_THRESHOLD;

        if (withinTime && withinDistance)
            _clickCount++;
        else
            _clickCount = 1;

        _lastClickTime = Time.time;
        _lastClickPosition = screenPos;

        if (_clickCount >= 2)
        {
            TryFocus();
            _clickCount = 0;
            return true;
        }
        return false;
    }

    void TryFocus()
    {
        // Reset always lands on the original 3D home pose, including when invoked from inside 2D mode.
        if (_is2DMode)
            Exit2DMode();

        // Double-tap anywhere resets to the starting view.
        _targetPosition = _originalTargetPosition;
        _targetDistance = _originalDistance;
        _targetYaw = _originalYaw;
        _targetPitch = _originalPitch;

        // Cancel any in-flight drag so the next mouse-move delta does not stack on top
        // of the just-reset values and drift the camera back away from the home pose.
        _isOrbiting = false;
        _isPanning = false;
        _touchOrbitActive = false;
        _pinchActive = false;
    }

    #region 2D Toggle

    public void Toggle2DMode()
    {
        // Mirror TryFocus's cleanup: any in-flight gesture would stack a stale delta onto the new pose.
        _isOrbiting = false;
        _isPanning = false;
        _touchOrbitActive = false;
        _pinchActive = false;

        if (_is2DMode)
            Exit2DMode();
        else
            Enter2DMode();
    }

    void Enter2DMode()
    {
        Vector3 forward, up;
        GetAutoDetectedAxis(out forward, out up);

        Quaternion lookRot = Quaternion.LookRotation(forward, up);
        Vector3 euler = lookRot.eulerAngles;
        _lockedYaw = euler.y;
        _lockedPitch = NormalizeAngle(euler.x);

        // Widen pitch clamp so ±90° (top/bottom) snaps land exactly; restore on exit.
        _savedMinPitch = minPitch;
        _savedMaxPitch = maxPitch;
        minPitch = -90f;
        maxPitch = 90f;

        // Snap both target and current so ApplyCameraTransform doesn't smooth-rotate through an intermediate angle.
        // Zero the SmoothDamp velocities too — otherwise residual orbit velocity briefly swings the locked view off-axis.
        _targetYaw = _currentYaw = _lockedYaw;
        _targetPitch = _currentPitch = _lockedPitch;
        _yawVelocity = 0f;
        _pitchVelocity = 0f;

        // Derive ortho size from current perspective distance + FOV so apparent scale doesn't jump.
        // Standard projection identity: at distance d with vertical FOV f, frustum half-height = d * tan(f/2).
        float halfFovRad = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float orthoSize = _currentDistance * Mathf.Tan(halfFovRad);
        _targetOrthoSize = _currentOrthoSize = orthoSize;
        _orthoSizeVelocity = 0f;

        _cam.orthographic = true;
        _cam.orthographicSize = _currentOrthoSize;

        _is2DMode = true;
        On2DModeChanged?.Invoke(true);
    }

    void Exit2DMode()
    {
        // Reverse-map ortho size back to perspective distance so apparent scale is preserved.
        float halfFovRad = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanHalf = Mathf.Tan(halfFovRad);
        if (tanHalf > 0.0001f)
        {
            float newDistance = Mathf.Clamp(_currentOrthoSize / tanHalf, minDistance, maxDistance);
            _targetDistance = _currentDistance = newDistance;
            _distanceVelocity = 0f;
        }

        // Restore pitch clamp and re-clamp target so a top-/bottom-down lock doesn't strand the target outside the new range.
        minPitch = _savedMinPitch;
        maxPitch = _savedMaxPitch;
        _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);

        _cam.orthographic = false;
        _is2DMode = false;
        On2DModeChanged?.Invoke(false);
    }

    void GetAutoDetectedAxis(out Vector3 forward, out Vector3 up)
    {
        // Pick the canonical world axis most closely aligned with the camera's current forward.
        Vector3 fwd = transform.forward;
        int bestIdx = 0;
        float bestDot = Vector3.Dot(fwd, CANONICAL_AXES[0]);
        for (int i = 1; i < CANONICAL_AXES.Length; i++)
        {
            float d = Vector3.Dot(fwd, CANONICAL_AXES[i]);
            if (d > bestDot) { bestDot = d; bestIdx = i; }
        }

        forward = CANONICAL_AXES[bestIdx];
        // For ±Y forward, world +Y is degenerate as up. World +Z is the standard "north" up for a top/bottom view.
        up = (bestIdx == 2 || bestIdx == 3) ? Vector3.forward : Vector3.up;
    }

    float GetMinOrthoSize() => minDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    float GetMaxOrthoSize() => maxDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

    #endregion

    // Pixels-to-degrees factor: a swipe equal to screen height rotates 360°.
    float DegPerPixel() => 360f / Mathf.Max(1, Screen.height);

    // World units per screen pixel at the focal plane. 1px finger ≈ 1px world tracking.
    float WorldPerPixel()
    {
        if (_cam.orthographic)
            return _cam.orthographicSize * 2f / Mathf.Max(1, Screen.height);
        return 2f * _currentDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // WebGL: tab switch or virtual keyboard can drop touches without an Ended phase.
        // Clear in-flight gesture state so the next interaction does not start from a stale anchor.
        if (!hasFocus)
        {
            _isOrbiting = false;
            _isPanning = false;
            _touchOrbitActive = false;
            _pinchActive = false;
            _lastTouchCount = 0;
            _lastPinchDistance = 0f;
            _lastTouchMidpoint = Vector2.zero;
            _clickCount = 0;
            // Leave _is2DMode untouched — tab-switching shouldn't yank projection.
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Stripped from release builds entirely — the IMGUI dispatcher won't even register
    // this listener, eliminating the small per-frame Layout/Repaint overhead on mobile.
    void OnGUI()
    {
        if (!showDebugOverlay)
            return;

        // Build the style once. OnGUI runs ~2x per frame (Layout + Repaint), so allocating
        // a new GUIStyle here would generate per-frame GC garbage on WebGL/mobile.
        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.label) { fontSize = GUI.skin.label.fontSize * 3 };
            _debugStyle.normal.textColor = Color.black;
        }

        float fps = 1f / Time.smoothDeltaTime;
        float dpi = Screen.dpi > 0f ? Screen.dpi : 0f;
        string debugText = $"FPS: {fps:F1}\nDPI: {dpi:F0}\nTouches: {Input.touchCount}";

        GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height - 220, 360, 180), debugText, _debugStyle);
    }
#endif

    #endregion

    #region Transform Application

    void ApplyCameraTransform()
    {
        // In 2D mode, hard-lock yaw/pitch every frame so any in-flight orbit input from the user (or stale smoothing
        // velocities) cannot drift the view away from the chosen axis.
        if (_is2DMode)
        {
            _targetYaw = _lockedYaw;
            _targetPitch = _lockedPitch;
        }

        // Smoothly interpolate current values towards targets for buttery smooth motion
        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, rotateSmoothTime);
        _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _pitchVelocity, rotateSmoothTime);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, zoomSmoothTime);
        _currentPosition = Vector3.SmoothDamp(_currentPosition, _targetPosition, ref _positionVelocity, panSmoothTime);

        // Keep the inspector-visible `distance` field tracking the current smoothed value
        // so it reads as a live view-distance rather than drifting from the initial seed.
        distance = _currentDistance;

        // Ortho size is only used in 2D mode; skip the SmoothDamp work entirely in 3D.
        if (_is2DMode)
        {
            _currentOrthoSize = Mathf.SmoothDamp(_currentOrthoSize, _targetOrthoSize, ref _orthoSizeVelocity, zoomSmoothTime);
            _cam.orthographicSize = _currentOrthoSize;
        }

        // Calculate final rotation and position
        Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -_currentDistance);

        transform.position = _currentPosition + offset;
        transform.rotation = rotation;
    }

    #endregion
}