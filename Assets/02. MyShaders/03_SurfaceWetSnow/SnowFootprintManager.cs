using UnityEngine;

/// <summary>
/// Keeps a persistent world-XZ footprint texture for one loaded gameplay area.
/// Use one active manager per loaded map area/scene.
/// </summary>
[DisallowMultipleComponent]
public class SnowFootprintManager : MonoBehaviour
{
    static readonly int FootprintTexId = Shader.PropertyToID("_FootprintTex");
    static readonly int WorldMinSizeId = Shader.PropertyToID("_FootprintWorldMinSize");
    static readonly int TexelWorldSizeId = Shader.PropertyToID("_FootprintTexelWorldSize");
    static readonly int StampWorldMinSizeId = Shader.PropertyToID("_WorldMinSize");
    static readonly int StampCenterId = Shader.PropertyToID("_StampCenter");
    static readonly int StampForwardId = Shader.PropertyToID("_StampForward");
    static readonly int StampSizeStrengthId = Shader.PropertyToID("_StampSizeStrength");
    static readonly int DecayId = Shader.PropertyToID("_Decay");
    static readonly int StampEnabledId = Shader.PropertyToID("_StampEnabled");

    [Header("World Area")]
    [SerializeField] Transform areaCenter;
    [SerializeField] Vector2 worldSize = new Vector2(64f, 64f);
    [SerializeField] int resolution = 2048;

    [Header("Rendering")]
    [Tooltip("Assign FootprintStamp.shader here so it is included in player builds.")]
    [SerializeField] Shader stampShader;

    [Header("Persistence")]
    [Tooltip("Exponential fade rate per second. 0 keeps tracks until Clear() or scene unload.")]
    [Min(0f)] [SerializeField] float fadePerSecond = 0.001f;
    [Min(0.05f)] [SerializeField] float decayInterval = 0.5f;

    RenderTexture textureA;
    RenderTexture textureB;
    RenderTexture currentTexture;
    Material stampMaterial;
    Vector4 worldMinSize;
    float nextDecayTime;
    bool initialized;

    void Awake() => EnsureInitialized();

    void OnEnable()
    {
        if (EnsureInitialized()) PublishGlobals();
    }

    void Update()
    {
        if (!initialized || fadePerSecond <= 0f || Time.time < nextDecayTime)
            return;

        float elapsed = Mathf.Max(0f, Time.time - (nextDecayTime - decayInterval));
        nextDecayTime = Time.time + decayInterval;
        if (elapsed <= 0f) return;

        float decay = Mathf.Exp(-fadePerSecond * elapsed);
        if (decay >= 0.99999f) return;

        stampMaterial.SetFloat(DecayId, decay);
        stampMaterial.SetFloat(StampEnabledId, 0f);
        BlitAndSwap();
    }

    /// <summary>Writes one shoe-shaped footprint. Position/forward are projected onto world XZ.</summary>
    public bool Stamp(Vector3 position, Vector3 forward, float width = 0.19f, float length = 0.34f, float strength = 1f)
    {
        if (!EnsureInitialized()) return false;

        Vector2 min = new Vector2(worldMinSize.x, worldMinSize.y);
        Vector2 max = min + new Vector2(worldMinSize.z, worldMinSize.w);
        if (position.x < min.x || position.x > max.x || position.z < min.y || position.z > max.y)
            return false;

        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        stampMaterial.SetVector(StampWorldMinSizeId, worldMinSize);
        stampMaterial.SetVector(StampCenterId, new Vector4(position.x, position.z, 0f, 0f));
        stampMaterial.SetVector(StampForwardId, new Vector4(flatForward.x, flatForward.z, 0f, 0f));
        stampMaterial.SetVector(StampSizeStrengthId, new Vector4(
            Mathf.Max(0.04f, width), Mathf.Max(0.06f, length), Mathf.Clamp01(strength), 0f
        ));
        stampMaterial.SetFloat(DecayId, 1f);
        stampMaterial.SetFloat(StampEnabledId, 1f);

        BlitAndSwap();
        return true;
    }

    public void Clear()
    {
        if (!EnsureInitialized()) return;
        ClearTexture(textureA);
        ClearTexture(textureB);
        currentTexture = textureA;
        PublishGlobals();
    }

    public bool Contains(Vector3 position)
    {
        Vector2 min = new Vector2(worldMinSize.x, worldMinSize.y);
        Vector2 max = min + new Vector2(worldMinSize.z, worldMinSize.w);
        return position.x >= min.x && position.x <= max.x && position.z >= min.y && position.z <= max.y;
    }

    bool EnsureInitialized()
    {
        if (initialized) return true;

        worldSize.x = Mathf.Max(1f, worldSize.x);
        worldSize.y = Mathf.Max(1f, worldSize.y);
        resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 256, 4096));

        if (stampShader == null)
            stampShader = Shader.Find("Hidden/MyShaders/FootprintStamp");

        if (stampShader == null)
        {
            Debug.LogError("SnowFootprintManager: FootprintStamp.shader를 찾지 못했습니다. Inspector에서 지정하세요.", this);
            return false;
        }

        stampMaterial = new Material(stampShader) { hideFlags = HideFlags.HideAndDontSave };
        Vector3 center = areaCenter != null ? areaCenter.position : transform.position;
        worldMinSize = new Vector4(
            center.x - worldSize.x * 0.5f,
            center.z - worldSize.y * 0.5f,
            worldSize.x,
            worldSize.y
        );

        textureA = CreateTexture("Snow Footprints A");
        textureB = CreateTexture("Snow Footprints B");
        if (textureA == null || textureB == null)
        {
            ReleaseResources();
            return false;
        }

        ClearTexture(textureA);
        ClearTexture(textureB);
        currentTexture = textureA;
        stampMaterial.SetVector(StampWorldMinSizeId, worldMinSize);
        initialized = true;
        nextDecayTime = Time.time + decayInterval;
        PublishGlobals();
        return true;
    }

    RenderTexture CreateTexture(string textureName)
    {
        var rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        if (!rt.Create())
        {
            Debug.LogError($"SnowFootprintManager: {textureName} 생성에 실패했습니다.", this);
            Destroy(rt);
            return null;
        }
        return rt;
    }

    static void ClearTexture(RenderTexture target)
    {
        if (target == null) return;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }

    void BlitAndSwap()
    {
        RenderTexture writeTarget = currentTexture == textureA ? textureB : textureA;
        Graphics.Blit(currentTexture, writeTarget, stampMaterial, 0);
        currentTexture = writeTarget;
        PublishGlobals();
    }

    void PublishGlobals()
    {
        if (currentTexture == null) return;
        Shader.SetGlobalTexture(FootprintTexId, currentTexture);
        Shader.SetGlobalVector(WorldMinSizeId, worldMinSize);
        Shader.SetGlobalVector(TexelWorldSizeId, new Vector4(
            worldSize.x / resolution, worldSize.y / resolution, 0f, 0f
        ));
    }

    void OnDestroy()
    {
        if (Shader.GetGlobalTexture(FootprintTexId) == currentTexture)
            Shader.SetGlobalTexture(FootprintTexId, Texture2D.blackTexture);
        ReleaseResources();
    }

    void ReleaseResources()
    {
        if (textureA != null) { textureA.Release(); Destroy(textureA); }
        if (textureB != null) { textureB.Release(); Destroy(textureB); }
        if (stampMaterial != null) Destroy(stampMaterial);
        textureA = null;
        textureB = null;
        currentTexture = null;
        stampMaterial = null;
        initialized = false;
    }
}
