using System.Collections;
using UnityEngine;

namespace ShaderGallery.Samples
{
    /// <summary>Controls snow/wet coverage and short storm bursts on this Renderer or a shared Material.</summary>
    [DisallowMultipleComponent]
    public class SurfaceWeatherController : MonoBehaviour
    {
        static readonly int StormId = Shader.PropertyToID("_Storm");
        static readonly int CoverageId = Shader.PropertyToID("_Coverage");

        [Tooltip("Assign a shared material to control that asset. Leave empty to create a private instance for this Renderer.")]
        [SerializeField] Material sharedMaterial;
        [Range(0f, 1f)] [SerializeField] float peak = 1f;
        [Min(0f)] [SerializeField] float riseSeconds = 0.8f;
        [Min(0f)] [SerializeField] float holdSeconds = 1.6f;
        [Min(0f)] [SerializeField] float fallSeconds = 1.6f;
        [Min(0f)] [SerializeField] float autoInterval = 0f;

        Material material;
        bool ownsCopy;
        float originalStorm;
        float originalCoverage;
        float nextAuto;
        Coroutine stormRoutine;
        Coroutine coverageRoutine;

        void Awake()
        {
            if (sharedMaterial != null)
            {
                material = sharedMaterial;
                originalStorm = material.GetFloat(StormId);
                originalCoverage = material.GetFloat(CoverageId);
            }
            else
            {
                Renderer targetRenderer = GetComponent<Renderer>();
                if (targetRenderer == null)
                {
                    Debug.LogWarning($"{name}: Renderer가 없어 날씨 효과를 제어할 수 없습니다.", this);
                    enabled = false;
                    return;
                }
                material = targetRenderer.material;
                ownsCopy = true;
            }
            material.SetFloat(StormId, 0f);
        }

        void OnEnable() => nextAuto = Time.time + autoInterval;

        void Update()
        {
            if (autoInterval > 0f && Time.time >= nextAuto)
            {
                nextAuto = Time.time + autoInterval;
                Storm();
            }
        }

        public void Storm()
        {
            if (!isActiveAndEnabled || material == null) return;
            if (stormRoutine != null) StopCoroutine(stormRoutine);
            stormRoutine = StartCoroutine(Blow());
        }

        public void SetCoverage(float target, float seconds)
        {
            if (!isActiveAndEnabled || material == null) return;
            if (coverageRoutine != null)
            {
                StopCoroutine(coverageRoutine);
                coverageRoutine = null;
            }
            target = Mathf.Clamp01(target);
            if (seconds <= 0f)
            {
                material.SetFloat(CoverageId, target);
                return;
            }
            coverageRoutine = StartCoroutine(MoveCoverage(target, seconds));
        }

        IEnumerator MoveCoverage(float target, float seconds)
        {
            float start = material.GetFloat(CoverageId);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                material.SetFloat(CoverageId, Ease(start, target, t / seconds));
                yield return null;
            }
            material.SetFloat(CoverageId, target);
            coverageRoutine = null;
        }

        IEnumerator Blow()
        {
            float start = material.GetFloat(StormId);
            if (riseSeconds > 0f)
            {
                for (float t = 0f; t < riseSeconds; t += Time.deltaTime)
                {
                    material.SetFloat(StormId, Ease(start, peak, t / riseSeconds));
                    yield return null;
                }
            }
            material.SetFloat(StormId, peak);
            if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);
            if (fallSeconds > 0f)
            {
                for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
                {
                    material.SetFloat(StormId, Ease(peak, 0f, t / fallSeconds));
                    yield return null;
                }
            }
            material.SetFloat(StormId, 0f);
            stormRoutine = null;
        }

        static float Ease(float from, float to, float t)
        {
            return Mathf.Lerp(from, to, 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)));
        }

        void OnDisable()
        {
            StopAllCoroutines();
            stormRoutine = null;
            coverageRoutine = null;
            if (material == null) return;
            if (ownsCopy) material.SetFloat(StormId, 0f);
            else
            {
                material.SetFloat(StormId, originalStorm);
                material.SetFloat(CoverageId, originalCoverage);
            }
        }

        void OnDestroy()
        {
            if (ownsCopy && material != null) Destroy(material);
        }
    }
}
