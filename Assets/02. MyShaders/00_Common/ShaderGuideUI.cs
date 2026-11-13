using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game 뷰 오른쪽 위에 이 씬에서 확인할 수 있는 셰이더와 조작 방법을 보여준다.
/// 씬의 아무 GameObject에 붙이면 된다. F1 키로 숨기기/보이기.
/// </summary>
[DisallowMultipleComponent]
public class ShaderGuideUI : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string shaderName;
        public string targetObject;
        [TextArea(2, 8)] public string controls;
    }

    [SerializeField] bool visible = true;
    [Tooltip("1080p 기준 배율. 화면 높이에 맞춰 자동으로 곱해진다.")]
    [SerializeField, Range(0.5f, 2f)] float uiScale = 1f;
    [SerializeField, Min(200f)] float panelWidth = 330f;

    [SerializeField] List<Entry> entries = new List<Entry>
    {
        new Entry
        {
            shaderName = "Dissolve Edge Glow",
            targetObject = "ShaderBall_4",
            controls = "1 : 나타나기\n2 : 사라지기"
        },
        new Entry
        {
            shaderName = "Descending Light Column",
            targetObject = "ShaderCylinder",
            controls = "1 : 나타나기 (섬광 후 유지)\n2 : 사라지기 (위에서 아래로)\n3 : 전체 다시 재생 (시작 시 자동 재생)"
        },
        new Entry
        {
            shaderName = "Surface Wet / Snow + 발자국",
            targetObject = "Floor",
            controls = "좌클릭 : 발자국 찍기 / 드래그 : 걷기\n" +
                       "WASD·방향키 : 걷기 (Shift 달리기)\n" +
                       "Space : 제자리 찍기\n" +
                       "L : 직선  O : 원형  R : 자동 걷기\n" +
                       "C : 발자국 지우기"
        },
    };

    GUIStyle titleStyle;
    GUIStyle headerStyle;
    GUIStyle bodyStyle;
    GUIStyle hintStyle;

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.f1Key.wasPressedThisFrame) visible = !visible;
#else
        if (Input.GetKeyDown(KeyCode.F1)) visible = !visible;
#endif
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
        headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
    }

    void OnGUI()
    {
        EnsureStyles();

        float scale = uiScale * Mathf.Max(0.5f, Screen.height / 1080f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float screenW = Screen.width / scale;
        const float margin = 10f;

        if (!visible)
        {
            GUI.Label(new Rect(screenW - 170f - margin, margin, 170f, 22f), "F1 : 셰이더 가이드 보기", hintStyle);
            GUI.matrix = previous;
            return;
        }

        GUILayout.BeginArea(new Rect(screenW - panelWidth - margin, margin, panelWidth, Screen.height / scale - margin * 2f));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("셰이더 확인 가이드", titleStyle);
        GUILayout.Space(4f);

        foreach (var e in entries)
        {
            if (e == null) continue;
            string header = string.IsNullOrEmpty(e.targetObject) ? e.shaderName : $"{e.shaderName}  ({e.targetObject})";
            GUILayout.Label(header, headerStyle);
            if (!string.IsNullOrEmpty(e.controls)) GUILayout.Label(e.controls, bodyStyle);
            GUILayout.Space(6f);
        }

        GUILayout.Label("F1 : 가이드 숨기기", hintStyle);
        GUILayout.EndVertical();
        GUILayout.EndArea();

        GUI.matrix = previous;
    }
}
