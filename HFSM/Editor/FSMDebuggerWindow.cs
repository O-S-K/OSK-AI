#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

public class FSMDebuggerWindow : EditorWindow
{
    private Vector2 scrollLeft, scrollRight;
    private List<IFSMInspectable> allFSMObjects = new();
    private double nextRefreshTime;
    private float refreshInterval = 0.3f;
    private IState selectedState;

    [MenuItem("OSK-AI/AI/HFSM Debugger 🧠")]
    public static void Open() => GetWindow<FSMDebuggerWindow>("FSM Debugger");

    private void OnEnable()
    {
        FindAllFSMs();
        EditorApplication.playModeStateChanged += _ => FindAllFSMs();
    }

    private void Update()
    {
        if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup > nextRefreshTime)
        {
            Repaint();
            nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
        }
    }

    private void OnGUI()
    {
        try
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🎮 FSM Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("🔄 Refresh (Manual)", GUILayout.Height(22)))
                FindAllFSMs();

            if (allFSMObjects == null)
                allFSMObjects = new List<IFSMInspectable>();

            // Dọn các object null trước khi vẽ
            allFSMObjects.RemoveAll(x =>
            {
                var mb = x as MonoBehaviour;
                return x == null || mb == null || mb.Equals(null) || mb.gameObject == null;
            });

            if (allFSMObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Không có FSM nào trong scene hoặc tất cả đã bị destroy.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            try
            {
                DrawLeftPanel();
                DrawRightPanel();
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FSM Debugger] GUI exception handled: {ex.Message}");
        }
    }

    private void DrawLeftPanel()
    {
        try
        {
            scrollLeft = EditorGUILayout.BeginScrollView(scrollLeft, GUILayout.Width(position.width * 0.55f));

            foreach (var obj in allFSMObjects)
            {
                if (obj == null) continue;
                var mono = (MonoBehaviour)obj;
                if (mono == null || mono.Equals(null) || mono.gameObject == null) continue;

                try
                {
                    DrawFSMObject(obj);
                }
                catch (MissingReferenceException)
                {
                    // Enemy bị destroy giữa frame
                    continue;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FSM Debugger] DrawFSMObject error: {ex.Message}");
                }
            }
        }
        finally
        {
            // ✅ luôn gọi EndScrollView dù có lỗi
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawFSMObject(IFSMInspectable fsmOwner)
    {
        if (fsmOwner == null) return;

        var mono = fsmOwner as MonoBehaviour;
        if (mono == null || mono.Equals(null) || mono.gameObject == null) return;

        var fsm = fsmOwner.GetFSM();
        if (fsm == null) return;

        // ✅ Dùng try/finally để đảm bảo EndVertical được gọi
        EditorGUILayout.BeginVertical("box");
        try
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🧩 {fsmOwner.GetFSMName()}", EditorStyles.boldLabel);
            if (GUILayout.Button("Select", GUILayout.Width(70)))
                Selection.activeGameObject = mono.gameObject;
            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField("GameObject:", mono.gameObject.name);
            EditorGUILayout.LabelField("Current State:", fsm.CurrentState?.GetType().Name ?? "None",
                HighlightStyle(fsm.CurrentState, true));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("📋 States:", EditorStyles.boldLabel);
            foreach (var state in fsm.AllStates)
            {
                if (state is HierarchicalState hs)
                    DrawHierarchicalState(hs, fsm.CurrentState, 1);
                else
                    DrawStateItem(state, fsm.CurrentState, 1);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("➡️ Transitions:", EditorStyles.boldLabel);
            DrawTransitions(fsm);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("🔀 Any Transitions:", EditorStyles.boldLabel);
            DrawAnyTransitions(fsm);
        }
        finally
        {
            // ✅ luôn kết thúc layout dù có exception
            EditorGUILayout.EndVertical();
        }
    }


    private void FindAllFSMs()
    {
        allFSMObjects = FindObjectsOfType<MonoBehaviour>(true)
            .OfType<IFSMInspectable>()
            .ToList();
    }

    private void DrawStateItem(IState state, IState currentState, int indent)
    {
        string prefix = new string(' ', indent * 2);
        GUIStyle style = HighlightStyle(state == currentState ? state : null, false, state == selectedState);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indent * 10);
        if (GUILayout.Button($"{prefix}- {state.GetType().Name}", style))
        {
            selectedState = selectedState == state ? null : state;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawHierarchicalState(HierarchicalState hs, IState currentState, int indent)
    {
        string prefix = new string(' ', indent * 2);
        GUIStyle style = HighlightStyle(hs == currentState ? hs : null, false, hs == selectedState);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indent * 10);
        if (GUILayout.Button($"{prefix}🧭 HFSM: {hs.Name}", style))
        {
            selectedState = selectedState == hs ? null : hs;
        }

        EditorGUILayout.EndHorizontal();

        var subField = typeof(HierarchicalState).GetField("_subStates", BindingFlags.NonPublic | BindingFlags.Instance);
        var subStates = subField?.GetValue(hs) as List<IState>;
        if (subStates != null)
        {
            foreach (var sub in subStates)
            {
                if (sub is HierarchicalState inner)
                    DrawHierarchicalState(inner, hs.CurrentSubState, indent + 1);
                else
                    DrawStateItem(sub, hs.CurrentSubState, indent + 1);
            }
        }
    }

    // 🟦 SHOW TRANSITIONS WITH CONDITION
    private void DrawTransitions(HFSM hfsm)
    {
        var transitions = GetPrivateField<List<HFSMTransition>>(hfsm, "_transitions");
        if (transitions == null || transitions.Count == 0)
        {
            EditorGUILayout.LabelField("   (none)");
            return;
        }

        foreach (var t in transitions)
        {
            bool condResult = false;
            try
            {
                condResult = t.Condition?.Invoke() ?? false;
            }
            catch
            {
            }

            string desc = "";
            try
            {
                desc = t.DebugDesc?.Invoke() ?? "";
            }
            catch
            {
            }

            string color = condResult ? "yellow" : "gray";
            string status = condResult ? "✅" : "✖";
            string text = $" - {t.From?.GetType().Name ?? "Any"} → {t.To?.GetType().Name ?? "None"}   " +
                          $"<color={color}>[{(string.IsNullOrEmpty(desc) ? DescribeCondition(t.Condition) : desc)} {status}]</color>";
            EditorGUILayout.LabelField(text, RichLabelStyle());
        }
    }

    private void DrawAnyTransitions(HFSM hfsm)
    {
        var list = GetPrivateField<List<(IState to, Func<bool> cond, Func<string> debug)>>(
            hfsm, "_anyTransitions");
        if (list == null || list.Count == 0)
        {
            EditorGUILayout.LabelField("   (none)");
            return;
        }

        foreach (var (to, cond, debug) in list)
        {
            bool condResult = false;
            try
            {
                condResult = cond?.Invoke() ?? false;
            }
            catch
            {
            }

            string desc = "";
            try
            {
                desc = debug?.Invoke() ?? "";
            }
            catch
            {
            }

            string color = condResult ? "yellow" : "gray";
            string status = condResult ? "✅" : "✖";
            string text = $" - → {to?.GetType().Name ?? "None"}   " +
                          $"<color={color}>[{(string.IsNullOrEmpty(desc) ? DescribeCondition(cond) : desc)} {status}]</color>";
            EditorGUILayout.LabelField(text, RichLabelStyle());
        }
    }

    private GUIStyle RichLabelStyle()
    {
        var style = new GUIStyle(EditorStyles.label);
        style.richText = true;
        style.wordWrap = true;
        return style;
    }

    // 🎯 Convert Func<bool> -> string readable
    private string DescribeCondition(Func<bool> condition)
    {
        if (condition == null) return "[null]";
        var method = condition.Method;
        string methodName = method.Name;

        if (method.IsStatic)
            return $"[{method.DeclaringType.Name}.{methodName}()]";

        // nếu là lambda (biến compiler-generated)
        if (methodName.Contains("lambda_method") || methodName.Contains("<"))
            return "[lambda]";
        return $"[{methodName}()]";
    }

    // ===== RIGHT PANEL =====
    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("🧩 State Detail", EditorStyles.boldLabel);

        if (selectedState == null)
        {
            EditorGUILayout.HelpBox("Chọn 1 state bên trái để xem chi tiết.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Type:", selectedState.GetType().Name, EditorStyles.boldLabel);

        if (selectedState is HierarchicalState hs)
        {
            EditorGUILayout.LabelField("HFSM Name:", hs.Name);
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("SubStates:", EditorStyles.boldLabel);

            var subField =
                typeof(HierarchicalState).GetField("_subStates", BindingFlags.NonPublic | BindingFlags.Instance);
            var subStates = subField?.GetValue(hs) as List<IState>;
            if (subStates != null)
                foreach (var sub in subStates)
                    EditorGUILayout.LabelField($" - {sub.GetType().Name}");

            if (hs.CurrentSubState != null)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Current SubState:", hs.CurrentSubState.GetType().Name,
                    HighlightStyle(hs.CurrentSubState, true));
            }
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Fields:", EditorStyles.boldLabel);
            DrawStateFields(selectedState);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawStateFields(IState state)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var fields = state.GetType().GetFields(flags);

        if (fields.Length == 0)
        {
            EditorGUILayout.LabelField(" (no fields)");
            return;
        }

        foreach (var f in fields)
        {
            var val = f.GetValue(state);
            string valStr = val == null ? "null" : val.ToString();
            EditorGUILayout.LabelField($" - {f.Name}: {valStr}");
        }
    }

    // ===== HELPERS =====
    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return default;
        return (T)field.GetValue(obj);
    }

    private GUIStyle HighlightStyle(object isCurrentState, bool isCurrentLabel = false, bool isSelected = false)
    {
        var style = new GUIStyle(EditorStyles.miniButton);
        style.alignment = TextAnchor.MiddleLeft;
        if (isSelected)
            style.normal.textColor = Color.cyan;
        else if (isCurrentState != null)
            style.normal.textColor = Color.green;
        else
            style.normal.textColor = EditorStyles.label.normal.textColor;
        return style;
    }
}
#endif