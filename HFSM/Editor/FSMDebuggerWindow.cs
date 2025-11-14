#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using OSK.AIHFSM; // ensure your HFSM namespace

// FSMDebuggerWindow: Editor window to inspect HFSM instances in scene (IFSMInspectable)
public class FSMDebuggerWindow : EditorWindow
{
    private Vector2 scrollLeft, scrollRight;
    private List<IFSMInspectable> allFSMObjects = new List<IFSMInspectable>();
    private double nextRefreshTime;
    private float refreshInterval = 0.25f;
    private IState selectedState;

    [MenuItem("OSK-AI/AI/HFSM Debugger 🧠")]
    public static void Open() => GetWindow<FSMDebuggerWindow>("FSM Debugger");

    private void OnEnable()
    {
        FindAllFSMs();
        EditorApplication.playModeStateChanged += _ => FindAllFSMs();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= _ => FindAllFSMs();
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
            EditorGUILayout.LabelField("🎮 HFSM Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("🔄 Refresh (Manual)", GUILayout.Height(22)))
                FindAllFSMs();

            if (allFSMObjects == null)
                allFSMObjects = new List<IFSMInspectable>();

            // remove destroyed/nulls
            allFSMObjects.RemoveAll(x =>
            {
                var mb = x as MonoBehaviour;
                return x == null || mb == null || mb.Equals(null) || mb.gameObject == null;
            });

            if (allFSMObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Không có IFSMInspectable nào trong scene.", MessageType.Info);
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
        scrollLeft = EditorGUILayout.BeginScrollView(scrollLeft, GUILayout.Width(position.width * 0.55f));
        try
        {
            foreach (var obj in allFSMObjects)
            {
                if (obj == null) continue;
                var mono = obj as MonoBehaviour;
                if (mono == null || mono.Equals(null) || mono.gameObject == null) continue;

                try
                {
                    DrawFSMObject(obj);
                }
                catch (MissingReferenceException)
                {
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
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("📋 States:", EditorStyles.boldLabel);
            try
            {
                foreach (var state in fsm.AllStates)
                {
                    if (state == null) continue;
                    if (IsHierarchicalState(state))
                        DrawHierarchicalState(state, fsm.CurrentState, 1);
                    else
                        DrawStateItem(state, fsm.CurrentState, 1);
                }
            }
            catch
            {
                EditorGUILayout.LabelField("Could not enumerate states (reflection mismatch).");
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

    private void DrawHierarchicalState(IState hsState, IState currentState, int indent)
    {
        // hsState is an IState that represents hierarchical container (could be our HierarchicalState)
        string prefix = new string(' ', indent * 2);
        GUIStyle style = HighlightStyle(hsState == currentState ? hsState : null, false, hsState == selectedState);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(indent * 10);
        if (GUILayout.Button($"{prefix}🧭 HFSM: {hsState.GetType().Name}", style))
        {
            selectedState = selectedState == hsState ? null : hsState;
        }
        EditorGUILayout.EndHorizontal();

        // Try to extract sub-states via reflection:
        // 1) look for a field that is List<IState> or List<object>
        // 2) or look for an internal HFSM field and query its AllStates/current
        var type = hsState.GetType();

        // Try direct list field
        var listField = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => typeof(IEnumerable<IState>).IsAssignableFrom(f.FieldType) || f.FieldType.IsGenericType && f.FieldType.GetGenericArguments().Contains(typeof(IState)));

        if (listField != null)
        {
            var subStates = listField.GetValue(hsState) as IEnumerable<IState>;
            if (subStates != null)
            {
                foreach (var sub in subStates)
                {
                    if (sub == null) continue;
                    if (IsHierarchicalState(sub))
                        DrawHierarchicalState(sub, GetCurrentSubState(hsState), indent + 1);
                    else
                        DrawStateItem(sub, GetCurrentSubState(hsState), indent + 1);
                }
                return;
            }
        }

        // Try to find internal HFSM field named "_sub" or "_subFsm" etc.
        var subFsmField = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.FieldType == typeof(HFSM));
        if (subFsmField != null)
        {
            var inner = subFsmField.GetValue(hsState) as HFSM;
            if (inner != null)
            {
                foreach (var s in inner.AllStates)
                {
                    if (s == null) continue;
                    if (IsHierarchicalState(s))
                        DrawHierarchicalState(s, inner.CurrentState, indent + 1);
                    else
                        DrawStateItem(s, inner.CurrentState, indent + 1);
                }
                return;
            }
        }

        // Fallback: show no details
        EditorGUILayout.LabelField($"{new string(' ', (indent + 1) * 2)}(no sub-states discoverable via reflection)");
    }

    // ---------------- TRANSITIONS ----------------

    private void DrawTransitions(HFSM hfsm)
    {
        // Try to get private field _transitions: List<HFSMTransition>
        var transitions = GetPrivateField<IEnumerable<object>>(hfsm, "_transitions");
        if (transitions == null)
        {
            EditorGUILayout.LabelField("   (none or private field name mismatch)");
            return;
        }

        foreach (var tObj in transitions)
        {
            if (tObj == null) continue;
            // try to read From, To, Condition, DebugDesc, Priority via reflection
            var tType = tObj.GetType();
            var from = tType.GetField("From", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tObj) as IState;
            var to = tType.GetField("To", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tObj) as IState;
            var cond = tType.GetField("Condition", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tObj) as Func<bool>;
            var debugDesc = tType.GetField("DebugDesc", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tObj) as Func<string>;
            var priorityField = tType.GetField("Priority", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            int priority = 0;
            if (priorityField != null) priority = (int)(priorityField.GetValue(tObj) ?? 0);

            bool condResult = false;
            try { condResult = cond?.Invoke() ?? false; } catch { condResult = false; }

            string desc = "";
            try { desc = debugDesc?.Invoke() ?? ""; } catch { desc = ""; }

            string color = condResult ? "yellow" : "gray";
            string status = condResult ? "✅" : "✖";
            string text = $" - {from?.GetType().Name ?? "Any"} → {to?.GetType().Name ?? "None"}   " +
                          $"<color={color}>[{(string.IsNullOrEmpty(desc) ? DescribeCondition(cond) : desc)} {status}]</color> (p={priority})";
            EditorGUILayout.LabelField(text, RichLabelStyle());
        }
    }

    private void DrawAnyTransitions(HFSM hfsm)
    {
        // Try couple of patterns: _anyTransitions field (List<HFSMTransition>), or _anyTransitions as List<Tuple/...>
        var any1 = GetPrivateField<IEnumerable<object>>(hfsm, "_anyTransitions");
        if (any1 != null)
        {
            foreach (var item in any1)
            {
                if (item == null) continue;
                var itemType = item.GetType();

                // if it's HFSMTransition-like
                if (itemType.GetField("To") != null)
                {
                    var to = itemType.GetField("To", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(item) as IState;
                    var cond = itemType.GetField("Condition", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(item) as Func<bool>;
                    var debug = itemType.GetField("DebugDesc", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(item) as Func<string>;
                    bool condResult = false;
                    try { condResult = cond?.Invoke() ?? false; } catch { condResult = false; }
                    string desc = "";
                    try { desc = debug?.Invoke() ?? ""; } catch { desc = ""; }
                    string color = condResult ? "yellow" : "gray";
                    string status = condResult ? "✅" : "✖";
                    string text = $" - → {to?.GetType().Name ?? "None"}   <color={color}>[{(string.IsNullOrEmpty(desc) ? DescribeCondition(cond) : desc)} {status}]</color>";
                    EditorGUILayout.LabelField(text, RichLabelStyle());
                    continue;
                }

                // if item is tuple-like (to, cond, debug) — try to decompose by fields
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                IState toState = null;
                Func<bool> condFunc = null;
                Func<string> debugFunc = null;
                foreach (var f in fields)
                {
                    var v = f.GetValue(item);
                    if (v is IState s) toState = s;
                    else if (v is Func<bool> fb) condFunc = fb;
                    else if (v is Func<string> fs) debugFunc = fs;
                }
                bool res = false;
                try { res = condFunc?.Invoke() ?? false; } catch { res = false; }
                string d = "";
                try { d = debugFunc?.Invoke() ?? ""; } catch { d = ""; }
                string col = res ? "yellow" : "gray";
                string st = res ? "✅" : "✖";
                string txt = $" - → {toState?.GetType().Name ?? "None"}   <color={col}>[{(string.IsNullOrEmpty(d) ? DescribeCondition(condFunc) : d)} {st}]</color>";
                EditorGUILayout.LabelField(txt, RichLabelStyle());
            }
            return;
        }

        EditorGUILayout.LabelField("   (none or reflection mismatch)");
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

        if (IsHierarchicalState(selectedState))
        {
            EditorGUILayout.LabelField("HFSM (Hierarchical):", selectedState.GetType().Name);
            EditorGUILayout.Space(3);
            // try get sub list
            var subList = TryGetSubStates(selectedState);
            if (subList != null && subList.Count > 0)
            {
                EditorGUILayout.LabelField("SubStates:", EditorStyles.boldLabel);
                foreach (var sub in subList)
                    EditorGUILayout.LabelField($" - {sub.GetType().Name}");
            }
            else
            {
                EditorGUILayout.LabelField("(no sub-states discoverable)");
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
            object val = null;
            try { val = f.GetValue(state); } catch { val = "(err)"; }
            string valStr = val == null ? "null" : val.ToString();
            EditorGUILayout.LabelField($" - {f.Name}: {valStr}");
        }
    }

    // ===== Helpers & Reflection utils =====

    private T GetPrivateField<T>(object obj, string fieldName)
    {
        if (obj == null) return default;
        var type = obj.GetType();
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return default;
        try { return (T)field.GetValue(obj); } catch { return default; }
    }

    private bool IsHierarchicalState(IState s)
    {
        if (s == null) return false;
        // basic check: type name contains "Hierarchical" or has an internal HFSM field
        var t = s.GetType();
        if (t.Name.IndexOf("Hierarch", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Any(f => f.FieldType == typeof(HFSM))) return true;
        if (t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Any(f => typeof(IEnumerable<IState>).IsAssignableFrom(f.FieldType))) return true;
        return false;
    }

    private IState GetCurrentSubState(object hierarchicalState)
    {
        if (hierarchicalState == null) return null;
        var t = hierarchicalState.GetType();

        // try find inner HFSM and return its CurrentState
        var innerField = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.FieldType == typeof(HFSM));
        if (innerField != null)
        {
            var inner = innerField.GetValue(hierarchicalState) as HFSM;
            if (inner != null) return inner.CurrentState;
        }

        // try property CurrentSubState / CurrentState
        var prop = t.GetProperty("CurrentSubState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
        {
            return prop.GetValue(hierarchicalState) as IState;
        }

        var field = t.GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return field.GetValue(hierarchicalState) as IState;

        return null;
    }

    private List<IState> TryGetSubStates(object hierarchicalState)
    {
        if (hierarchicalState == null) return null;
        var t = hierarchicalState.GetType();

        // try common private List<IState> style
        var listField = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => typeof(IEnumerable<IState>).IsAssignableFrom(f.FieldType));
        if (listField != null)
        {
            var val = listField.GetValue(hierarchicalState) as IEnumerable<IState>;
            return val?.ToList();
        }

        // try inner HFSM
        var innerField = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.FieldType == typeof(HFSM));
        if (innerField != null)
        {
            var inner = innerField.GetValue(hierarchicalState) as HFSM;
            if (inner != null) return inner.AllStates.ToList();
        }

        return null;
    }

    // Try to generate a readable description from Func<bool>
    private string DescribeCondition(Func<bool> condition)
    {
        if (condition == null) return "[null]";
        var method = condition.Method;
        string methodName = method.Name;

        if (method.IsStatic)
            return $"{method.DeclaringType.Name}.{methodName}()";

        if (methodName.Contains("lambda_method") || methodName.Contains("<"))
            return "[lambda]";

        return $"{methodName}()";
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

    private GUIStyle RichLabelStyle()
    {
        var style = new GUIStyle(EditorStyles.label);
        style.richText = true;
        style.wordWrap = true;
        return style;
    }
}
#endif
