using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 플레이어 없이 발자국 셰이더를 확인하기 위한 테스트 도구.
/// 눈 머테리얼이 적용된 바닥(Collider 필요)에 붙이고 Play 모드에서 사용합니다.
///
/// 마우스  : 좌클릭 = 발자국 1개, 좌클릭 드래그 = 걸어간 자국
/// 키보드  : WASD/방향키 = 가상 걷기(Shift 달리기), Space = 현재 위치에 발자국
///           C = 전체 지우기, R = 자동 걷기 토글, L = 직선, O = 원형
/// 화면    : 좌상단 버튼
/// 컨텍스트: 컴포넌트 ⋮ 메뉴의 "Footprint Test/..." 항목
/// </summary>
[DisallowMultipleComponent]
public class SnowFootprintTester : MonoBehaviour
{
    [Header("References (비워두면 자동 탐색)")]
    [SerializeField] SnowFootprintManager footprintManager;
    [SerializeField] Collider surfaceCollider;
    [SerializeField] Camera viewCamera;

    [Header("Footprint Shape")]
    [Min(0.04f)] [SerializeField] float footprintWidth = 0.19f;
    [Min(0.06f)] [SerializeField] float footprintLength = 0.34f;
    [Range(0f, 1f)] [SerializeField] float strength = 0.95f;

    [Header("Walking")]
    [Min(0.1f)] [SerializeField] float stepDistance = 0.62f;
    [Min(0f)] [SerializeField] float sideOffset = 0.09f;
    [Min(0.1f)] [SerializeField] float walkSpeed = 1.6f;
    [Min(1f)] [SerializeField] float runMultiplier = 2.2f;

    [Header("Presets")]
    [Min(0.5f)] [SerializeField] float circleRadius = 2.5f;
    [Min(0f)] [SerializeField] float presetStampInterval = 0.08f;

    [Header("UI")]
    [SerializeField] bool showOnScreenUI = true;

    // 가상 보행자 상태
    Vector3 walkerPos;
    Vector3 walkerForward = Vector3.forward;
    float walkerDistance;
    bool nextLeft = true;

    // 마우스 드래그 상태
    bool dragging;
    Vector3 lastDragPoint;
    float dragDistance;

    bool autoWalk;
    float autoWalkAngle;
    Coroutine presetRoutine;

    void Reset()
    {
        footprintManager = FindAnyObjectByType<SnowFootprintManager>();
        surfaceCollider = GetComponent<Collider>();
    }

    void Start()
    {
        if (footprintManager == null) footprintManager = FindAnyObjectByType<SnowFootprintManager>();
        if (surfaceCollider == null) surfaceCollider = GetComponent<Collider>();
        if (viewCamera == null) viewCamera = Camera.main;

        if (footprintManager == null)
            Debug.LogWarning("SnowFootprintTester: 씬에서 SnowFootprintManager를 찾지 못했습니다.", this);
        if (surfaceCollider == null)
            Debug.LogWarning("SnowFootprintTester: 바닥 Collider가 없어 클릭 위치를 계산할 수 없습니다. MeshCollider 등을 추가하세요.", this);

        walkerPos = SurfaceCenter();
    }

    void Update()
    {
        if (footprintManager == null) return;
        HandleMouse();
        HandleKeyboard();
        if (autoWalk) TickAutoWalk();
    }

    // ───────────────────────── Mouse ─────────────────────────

    void HandleMouse()
    {
        if (!TryGetMouse(out Vector2 mousePos, out bool pressed, out bool held, out bool released)) return;
        if (showOnScreenUI && IsOverUI(mousePos)) { dragging = false; return; }

        if (pressed && RaycastSurface(mousePos, out Vector3 hit))
        {
            dragging = true;
            lastDragPoint = hit;
            dragDistance = 0f;
            Vector3 fwd = CameraFlatForward();
            StampAt(hit, fwd, 0f);
            walkerPos = hit;
            walkerForward = fwd;
        }
        else if (held && dragging && RaycastSurface(mousePos, out Vector3 dragHit))
        {
            Vector3 delta = dragHit - lastDragPoint;
            delta.y = 0f;
            float len = delta.magnitude;
            if (len > 0.0001f)
            {
                Vector3 dir = delta / len;
                WalkSegment(lastDragPoint, dir, len, ref dragDistance);
                lastDragPoint = dragHit;
                walkerPos = dragHit;
                walkerForward = dir;
            }
        }

        if (released) dragging = false;
    }

    // ───────────────────────── Keyboard ─────────────────────────

    void HandleKeyboard()
    {
        Vector2 move = ReadMoveInput(out bool run);
        if (move.sqrMagnitude > 0.0001f)
        {
            autoWalk = false;
            Vector3 camF = CameraFlatForward();
            Vector3 camR = new Vector3(camF.z, 0f, -camF.x);
            Vector3 dir = (camR * move.x + camF * move.y).normalized;
            float len = walkSpeed * (run ? runMultiplier : 1f) * Time.deltaTime;
            MoveWalker(dir, len);
        }

        if (KeyDown(KeyId.Space)) StampAt(walkerPos, walkerForward, 0f);
        if (KeyDown(KeyId.C)) ClearFootprints();
        if (KeyDown(KeyId.R)) ToggleAutoWalk();
        if (KeyDown(KeyId.L)) StampStraightLine();
        if (KeyDown(KeyId.O)) StampCircle();
    }

    void TickAutoWalk()
    {
        // 원을 그리며 천천히 반경이 흔들리는 산책 경로
        autoWalkAngle += walkSpeed * Time.deltaTime / Mathf.Max(0.5f, circleRadius);
        Vector3 center = SurfaceCenter();
        float r = circleRadius * (1f + 0.35f * Mathf.Sin(autoWalkAngle * 0.37f));
        Vector3 target = center + new Vector3(Mathf.Cos(autoWalkAngle), 0f, Mathf.Sin(autoWalkAngle)) * r;
        Vector3 delta = target - walkerPos;
        delta.y = 0f;
        float len = delta.magnitude;
        if (len < 0.0001f) return;
        if (len > 1f) { walkerPos = ProjectToSurface(target); return; } // 첫 진입 시 순간이동
        MoveWalker(delta / len, len);
    }

    void MoveWalker(Vector3 dir, float len)
    {
        Vector3 start = walkerPos;
        WalkSegment(start, dir, len, ref walkerDistance);
        walkerPos = ProjectToSurface(start + dir * len);
        walkerForward = dir;
    }

    /// <summary>start에서 dir 방향으로 len만큼 걸으며 stepDistance마다 좌우 교대로 발자국을 찍습니다.</summary>
    void WalkSegment(Vector3 start, Vector3 dir, float len, ref float accumulated)
    {
        float consumed = 0f;
        while (accumulated + (len - consumed) >= stepDistance)
        {
            consumed += stepDistance - accumulated;
            accumulated = 0f;
            Vector3 p = ProjectToSurface(start + dir * consumed);
            StampAlternating(p, dir);
        }
        accumulated += len - consumed;
    }

    // ───────────────────────── Stamping ─────────────────────────

    void StampAlternating(Vector3 center, Vector3 forward)
    {
        float side = nextLeft ? -1f : 1f;
        nextLeft = !nextLeft;
        StampAt(center, forward, side);
    }

    void StampAt(Vector3 center, Vector3 forward, float side)
    {
        if (footprintManager == null) return;
        Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        flat.Normalize();
        Vector3 right = new Vector3(flat.z, 0f, -flat.x);
        Vector3 pos = center + right * (side * sideOffset);
        footprintManager.Stamp(pos, flat, footprintWidth, footprintLength, strength);
    }

    // ───────────────────────── Context menu / presets ─────────────────────────

    [ContextMenu("Footprint Test/Stamp Straight Line")]
    public void StampStraightLine()
    {
        if (!CheckPlaying()) return;
        Vector3 c = SurfaceCenter();
        Vector3 dir = CameraRightFlat();
        float half = Mathf.Min(SurfaceHalfExtent() * 0.8f, 4f);
        RunPreset(PresetPath(i => c + dir * Mathf.Lerp(-half, half, i), dir, half * 2f));
    }

    [ContextMenu("Footprint Test/Stamp Circle")]
    public void StampCircle()
    {
        if (!CheckPlaying()) return;
        Vector3 c = SurfaceCenter();
        float r = Mathf.Min(circleRadius, SurfaceHalfExtent() * 0.85f);
        RunPreset(PresetPath(t =>
        {
            float a = t * Mathf.PI * 2f;
            return c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
        }, null, Mathf.PI * 2f * r));
    }

    [ContextMenu("Footprint Test/Stamp Single At Center")]
    public void StampSingleAtCenter()
    {
        if (!CheckPlaying()) return;
        StampAt(SurfaceCenter(), CameraFlatForward(), 0f);
    }

    [ContextMenu("Footprint Test/Toggle Auto Walk")]
    public void ToggleAutoWalk()
    {
        if (!CheckPlaying()) return;
        autoWalk = !autoWalk;
        autoWalkAngle = 0f;
    }

    [ContextMenu("Footprint Test/Clear Footprints")]
    public void ClearFootprints()
    {
        if (!CheckPlaying()) return;
        if (presetRoutine != null) { StopCoroutine(presetRoutine); presetRoutine = null; }
        autoWalk = false;
        footprintManager?.Clear();
    }

    void RunPreset(IEnumerator routine)
    {
        if (presetRoutine != null) StopCoroutine(presetRoutine);
        autoWalk = false;
        presetRoutine = StartCoroutine(routine);
    }

    /// <summary>pathAt(0..1)로 정의된 경로를 따라 stepDistance 간격으로 발자국을 찍습니다.</summary>
    IEnumerator PresetPath(System.Func<float, Vector3> pathAt, Vector3? fixedForward, float totalLength)
    {
        int steps = Mathf.Max(2, Mathf.CeilToInt(totalLength / stepDistance));
        nextLeft = true;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 p = ProjectToSurface(pathAt(t));
            Vector3 fwd = fixedForward ?? (pathAt(Mathf.Min(1f, t + 0.01f)) - pathAt(Mathf.Max(0f, t - 0.01f)));
            StampAlternating(p, fwd);
            walkerPos = p;
            walkerForward = fwd;
            if (presetStampInterval > 0f) yield return new WaitForSeconds(presetStampInterval);
        }
        presetRoutine = null;
    }

    bool CheckPlaying()
    {
        if (Application.isPlaying && footprintManager != null) return true;
        Debug.LogWarning("SnowFootprintTester: Play 모드에서 실행하세요 (SnowFootprintManager 필요).", this);
        return false;
    }

    // ───────────────────────── On-screen UI ─────────────────────────

    static readonly Rect UiRect = new Rect(10, 10, 250, 250);

    bool IsOverUI(Vector2 mouseScreenPos)
    {
        // GUI 좌표는 Y가 위에서부터
        Vector2 guiPos = new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y);
        return UiRect.Contains(guiPos);
    }

    void OnGUI()
    {
        if (!showOnScreenUI) return;
        GUILayout.BeginArea(UiRect, GUI.skin.box);
        GUILayout.Label("<b>Snow Footprint Tester</b>", new GUIStyle(GUI.skin.label) { richText = true });
        if (GUILayout.Button("직선 걷기 (L)")) StampStraightLine();
        if (GUILayout.Button("원형 걷기 (O)")) StampCircle();
        if (GUILayout.Button(autoWalk ? "자동 걷기 끄기 (R)" : "자동 걷기 켜기 (R)")) ToggleAutoWalk();
        if (GUILayout.Button("발자국 지우기 (C)")) ClearFootprints();
        GUILayout.Label("좌클릭: 찍기 / 드래그: 걷기\nWASD: 걷기 (Shift 달리기)\nSpace: 제자리 찍기");
        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(walkerPos + Vector3.up * 0.05f, 0.12f);
        Gizmos.DrawLine(walkerPos + Vector3.up * 0.05f, walkerPos + Vector3.up * 0.05f + walkerForward * 0.4f);
    }

    // ───────────────────────── Surface helpers ─────────────────────────

    bool RaycastSurface(Vector2 screenPos, out Vector3 point)
    {
        point = default;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null || surfaceCollider == null) return false;
        Ray ray = viewCamera.ScreenPointToRay(screenPos);
        if (!surfaceCollider.Raycast(ray, out RaycastHit hit, 10000f)) return false;
        point = hit.point;
        return true;
    }

    Vector3 ProjectToSurface(Vector3 p)
    {
        if (surfaceCollider == null) return p;
        Bounds b = surfaceCollider.bounds;
        var ray = new Ray(new Vector3(p.x, b.max.y + 1f, p.z), Vector3.down);
        if (surfaceCollider.Raycast(ray, out RaycastHit hit, b.size.y + 2f)) return hit.point;
        // 바닥 밖으로 나가면 경계 안으로 고정
        return new Vector3(Mathf.Clamp(p.x, b.min.x, b.max.x), b.max.y, Mathf.Clamp(p.z, b.min.z, b.max.z));
    }

    Vector3 SurfaceCenter()
    {
        if (surfaceCollider != null) return ProjectToSurface(surfaceCollider.bounds.center);
        return transform.position;
    }

    float SurfaceHalfExtent()
    {
        if (surfaceCollider == null) return 5f;
        Vector3 e = surfaceCollider.bounds.extents;
        return Mathf.Max(0.5f, Mathf.Min(e.x, e.z));
    }

    Vector3 CameraFlatForward()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        Vector3 f = viewCamera != null ? viewCamera.transform.forward : Vector3.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = viewCamera != null ? viewCamera.transform.up : Vector3.forward;
        f.y = 0f;
        return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
    }

    Vector3 CameraRightFlat()
    {
        Vector3 f = CameraFlatForward();
        return new Vector3(f.z, 0f, -f.x);
    }

    // ───────────────────────── Input abstraction ─────────────────────────

    enum KeyId { Space, C, R, L, O }

#if ENABLE_INPUT_SYSTEM
    static bool TryGetMouse(out Vector2 pos, out bool pressed, out bool held, out bool released)
    {
        var m = Mouse.current;
        pos = default; pressed = held = released = false;
        if (m == null) return false;
        pos = m.position.ReadValue();
        pressed = m.leftButton.wasPressedThisFrame;
        held = m.leftButton.isPressed;
        released = m.leftButton.wasReleasedThisFrame;
        return true;
    }

    static Vector2 ReadMoveInput(out bool run)
    {
        var k = Keyboard.current;
        run = false;
        if (k == null) return Vector2.zero;
        run = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
        float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
        float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    static bool KeyDown(KeyId id)
    {
        var k = Keyboard.current;
        if (k == null) return false;
        switch (id)
        {
            case KeyId.Space: return k.spaceKey.wasPressedThisFrame;
            case KeyId.C: return k.cKey.wasPressedThisFrame;
            case KeyId.R: return k.rKey.wasPressedThisFrame;
            case KeyId.L: return k.lKey.wasPressedThisFrame;
            case KeyId.O: return k.oKey.wasPressedThisFrame;
        }
        return false;
    }
#else
    static bool TryGetMouse(out Vector2 pos, out bool pressed, out bool held, out bool released)
    {
        pos = Input.mousePosition;
        pressed = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        released = Input.GetMouseButtonUp(0);
        return true;
    }

    static Vector2 ReadMoveInput(out bool run)
    {
        run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        return new Vector2(x, y);
    }

    static bool KeyDown(KeyId id)
    {
        switch (id)
        {
            case KeyId.Space: return Input.GetKeyDown(KeyCode.Space);
            case KeyId.C: return Input.GetKeyDown(KeyCode.C);
            case KeyId.R: return Input.GetKeyDown(KeyCode.R);
            case KeyId.L: return Input.GetKeyDown(KeyCode.L);
            case KeyId.O: return Input.GetKeyDown(KeyCode.O);
        }
        return false;
    }
#endif
}
