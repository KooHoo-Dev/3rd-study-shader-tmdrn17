using System.Collections;
using UnityEngine;

/// <summary>
/// 빛기둥을 한 번 강하게 점멸시킨 다음, 위에서 아래로 쓸어내리며 사라지게 한다.
/// 이 스크립트는 빛기둥용 Cylinder의 Renderer가 있는 GameObject에 붙인다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class DescendingLightColumnController : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");

    [Header("재생")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0f)] private float flashDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float descendDuration = 0.9f;
    [SerializeField] private bool disableRendererWhenFinished = true;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine playCoroutine;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        // 대기 상태에서는 빛기둥이 보이지 않도록 끝난 상태로 초기화한다.
        SetEffect(1f, 0f);
        targetRenderer.enabled = false;
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    [Header("키보드 테스트 (Play 모드)")]
    [Tooltip("켜면 1 = 나타나기(섬광 후 유지), 2 = 사라지기(하강 소멸), 3 = 전체 재생.")]
    [SerializeField] private bool keyboardTest = true;

    private void Update()
    {
        if (!keyboardTest)
        {
            return;
        }

        if (WasPressed(1))
        {
            Appear();
        }
        else if (WasPressed(2))
        {
            Disappear();
        }
        else if (WasPressed(3))
        {
            Play();
        }
    }

    private static bool WasPressed(int digit)
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        switch (digit)
        {
            case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
        }
        return false;
#else
        switch (digit)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
        }
        return false;
#endif
    }

    /// <summary>빛기둥 효과를 처음부터 한 번 재생한다. (나타나기 → 사라지기)</summary>
    public void Play()
    {
        StartRoutine(PlayRoutine());
    }

    /// <summary>섬광과 함께 나타난 뒤 사라지지 않고 유지한다.</summary>
    public void Appear()
    {
        StartRoutine(FlashRoutine());
    }

    /// <summary>현재 보이는 빛기둥을 위에서 아래로 쓸어내리며 사라지게 한다.</summary>
    public void Disappear()
    {
        if (targetRenderer == null || !targetRenderer.enabled)
        {
            return;
        }

        StartRoutine(DescendRoutine());
    }

    private void StartRoutine(IEnumerator routine)
    {
        if (!isActiveAndEnabled || targetRenderer == null)
        {
            return;
        }

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        targetRenderer.enabled = true;
        playCoroutine = StartCoroutine(routine);
    }

    private IEnumerator PlayRoutine()
    {
        yield return FlashRoutine();
        yield return DescendRoutine();
    }

    private IEnumerator FlashRoutine()
    {
        // 1단계: 기둥 전체가 한 번 밝게 빛난 뒤 빠르게 잦아든다.
        SetEffect(0f, 1f);

        if (flashDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                float t = Mathf.Clamp01(elapsed / flashDuration);
                float flash = 1f - Mathf.SmoothStep(0f, 1f, t);
                SetEffect(0f, flash);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 섬광이 끝나면 기둥이 온전히 보이는 상태로 유지된다.
        SetEffect(0f, 0f);
    }

    private IEnumerator DescendRoutine()
    {
        // 2단계: 경계 발광이 위에서 아래로 이동하고, 지나간 부분은 사라진다.
        SetEffect(0f, 0f);

        float descendElapsed = 0f;
        while (descendElapsed < descendDuration)
        {
            float t = Mathf.Clamp01(descendElapsed / descendDuration);
            float progress = Mathf.SmoothStep(0f, 1f, t);
            SetEffect(progress, 0f);

            descendElapsed += Time.deltaTime;
            yield return null;
        }

        SetEffect(1f, 0f);
        playCoroutine = null;

        if (disableRendererWhenFinished && targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }
    }

    private void SetEffect(float progress, float flash)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ProgressId, Mathf.Clamp01(progress));
        propertyBlock.SetFloat(FlashId, Mathf.Clamp01(flash));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDisable()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }
}
