using System;
using System.Collections;
using UnityEngine;

namespace ShaderGallery.Samples
{
    [RequireComponent(typeof(Renderer))]
    public class DissolveController : MonoBehaviour
    {
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");

        [SerializeField, Min(0.01f)] private float duration = 1.2f;
        [SerializeField] private bool destroyWhenDone = true;

        public enum TestKey { Alpha1, Alpha2, Alpha3, Alpha4 }

        [Header("Keyboard Test (Play 모드)")]
        [Tooltip("켜면 키 입력으로 나타남/사라짐을 확인할 수 있다. 키 입력으로는 오브젝트를 파괴하지 않는다.")]
        [SerializeField] private bool keyboardTest = true;
        [SerializeField] private TestKey appearKey = TestKey.Alpha1;
        [SerializeField] private TestKey disappearKey = TestKey.Alpha2;

        private Material materialInstance;
        private Coroutine running;

        private void Awake()
        {
            // Renderer.material creates a per-renderer material instance.
            // This prevents this object's dissolve animation from changing shared materials.
            materialInstance = GetComponent<Renderer>().material;

            // Always begin fully visible, regardless of the preview value saved in the material.
            materialInstance.SetFloat(ThresholdId, 0f);
        }

        private void Update()
        {
            if (!keyboardTest)
                return;

            // 테스트용: 오브젝트를 파괴하지 않고 나타남/사라짐을 반복 확인한다.
            if (WasPressed(appearKey))
                Play(1f, 0f, false);
            else if (WasPressed(disappearKey))
                Play(0f, 1f, false);
        }

        private static bool WasPressed(TestKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return false;
            switch (key)
            {
                case TestKey.Alpha1: return k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame;
                case TestKey.Alpha2: return k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame;
                case TestKey.Alpha3: return k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame;
                case TestKey.Alpha4: return k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame;
            }
            return false;
#else
            switch (key)
            {
                case TestKey.Alpha1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case TestKey.Alpha2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case TestKey.Alpha3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                case TestKey.Alpha4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            }
            return false;
#endif
        }

        /// <summary>Play the dissolve-out effect and optionally destroy this GameObject.</summary>
        public void DissolveOut()
        {
            Play(0f, 1f, destroyWhenDone);
        }

        /// <summary>Play the dissolve-in effect. This does not destroy the GameObject.</summary>
        public void DissolveIn()
        {
            Play(1f, 0f, false);
        }

        private void Play(float from, float to, bool destroyAtEnd)
        {
            if (!isActiveAndEnabled || materialInstance == null)
                return;

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(Animate(from, to, destroyAtEnd));
        }

        private IEnumerator Animate(float from, float to, bool destroyAtEnd)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                float progress = elapsed / safeDuration;
                materialInstance.SetFloat(ThresholdId, Mathf.Lerp(from, to, progress));
                elapsed += Time.deltaTime;
                yield return null;
            }

            materialInstance.SetFloat(ThresholdId, to);
            running = null;

            if (destroyAtEnd)
                Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                Destroy(materialInstance);
                materialInstance = null;
            }
        }
    }
}
