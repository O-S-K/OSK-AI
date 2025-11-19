namespace OSK.AIHFSM.Editor
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Reflection;
    using OSK.AIHFSM; // adjust if needed

    /// <summary>
    /// Compact + colored FSMDebuggerWindow
    /// - Short, single-line transition descriptions
    /// - Colored lines: current state green, selected cyan, transition true=yellow false=gray
    /// - Shows priority, short desc, eval/trigger times (if available)
    /// </summary>
    public class HFSMDebug : EditorWindow
    {
        private Vector2 leftScroll, rightScroll;
        private List<IFSMInspectable> allFSM = new List<IFSMInspectable>();
        private double nextRefresh;
        private float refreshInterval = 0.25f;

        private IFSMInspectable selectedOwner;
        private IState selectedState;
        private string search = "";

        [MenuItem("OSK-AI/AI/HFSM Debugger")]
        public static void Open() => GetWindow<HFSMDebug>("HFSM Debugger");

        private void OnEnable()
        {
            RefreshList();
            EditorApplication.playModeStateChanged += _ => RefreshList();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= _ => RefreshList();
        }

        private void Update()
        {
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup > nextRefresh)
            {
                Repaint();
                nextRefresh = EditorApplication.timeSinceStartup + refreshInterval;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(90))) RefreshList();
            GUILayout.Label($"Found: {allFSM.Count}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            refreshInterval = EditorGUILayout.Slider(refreshInterval, 0.05f, 2f, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            DrawRight();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeft()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.42f));
            EditorGUILayout.LabelField("FSM Owners", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(40));
            search = EditorGUILayout.TextField(search);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) search = "";
            EditorGUILayout.EndHorizontal();

            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            if (allFSM.Count == 0)
            {
                EditorGUILayout.HelpBox("Không tìm thấy IFSMInspectable trong scene.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            foreach (var o in allFSM)
            {
                if (o == null) continue;
                var mb = o as MonoBehaviour;
                if (mb == null || mb.Equals(null) || mb.gameObject == null) continue;

                string title = $"{o.GetFSMName()} ({mb.gameObject.name})";
                if (!string.IsNullOrEmpty(search) && !title.ToLower().Contains(search.ToLower())) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(title, EditorStyles.label))
                {
                    selectedOwner = o;
                    selectedState = null;
                    Selection.activeGameObject = mb.gameObject;
                }

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = mb.gameObject;
                    selectedOwner = o;
                    selectedState = null;
                }

                EditorGUILayout.EndHorizontal();

                // compact current state
                try
                {
                    var f = o.GetFSM();
                    string cur = f?.CurrentState?.GetType().Name ?? "—";
                    var lbl = new GUIStyle(EditorStyles.miniLabel) { richText = false };
                    lbl.normal.textColor = Color.Lerp(Color.white, Color.green, 0.6f);
                    EditorGUILayout.LabelField($"Current: {cur}", lbl);
                }
                catch
                {
                    EditorGUILayout.LabelField("Current: (err)");
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRight()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if (selectedOwner == null)
            {
                EditorGUILayout.HelpBox("Chọn một FSM owner bên trái để xem chi tiết.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var mb = selectedOwner as MonoBehaviour;
            EditorGUILayout.LabelField("Owner:", mb != null ? mb.gameObject.name : selectedOwner.GetType().Name);
            if (GUILayout.Button("Ping Owner"))
            {
                if (mb != null) EditorGUIUtility.PingObject(mb.gameObject);
            }

            EditorGUILayout.Space();
            try
            {
                var fsm = selectedOwner.GetFSM();
                var cur = fsm?.CurrentState;

                // Current state big
                var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
                big.normal.textColor = cur != null ? Color.green : Color.white;
                EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(cur?.GetType().Name ?? "null", big);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("States", EditorStyles.boldLabel);
                var states = fsm?.AllStates;
                if (states != null)
                {
                    // show compact buttons; click to select state detail
                    EditorGUILayout.BeginHorizontal();
                    foreach (var s in states.Take(8))
                    {
                        if (s == null) continue;
                        bool isCur = s == fsm.CurrentState;
                        bool isSel = s == selectedState;
                        var st = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
                        st.normal.textColor =
                            isSel ? Color.cyan : (isCur ? Color.green : EditorStyles.label.normal.textColor);
                        if (GUILayout.Button(s.GetType().Name, st, GUILayout.MinWidth(80)))
                        {
                            selectedState = isSel ? null : s;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(6);
                // transitions
                EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
                DrawTransitionsColored(fsm);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Any Transitions", EditorStyles.boldLabel);
                DrawAnyTransitionsColored(fsm);

                EditorGUILayout.Space(6);
                if (selectedState != null)
                {
                    EditorGUILayout.LabelField("Selected State Fields", EditorStyles.boldLabel);
                    DrawStateFields(selectedState);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.LabelField($"Error reading FSM: {ex.Message}");
            }

            EditorGUILayout.EndVertical();
        }

        // ---------------- compact colored transitions ----------------
        private void DrawTransitionsColored(HFSM hfsm)
        {
            if (hfsm == null)
            {
                EditorGUILayout.LabelField("(no HFSM)");
                return;
            }

            var transitions = GetPrivateField<IEnumerable<object>>(hfsm, "_transitions") ??
                              GetPrivateField<IEnumerable<object>>(hfsm, "transitions") ??
                              GetPrivateField<IEnumerable<object>>(hfsm, "Transitions");

            if (transitions == null)
            {
                EditorGUILayout.LabelField("(no transitions found)");
                return;
            }

            foreach (var t in transitions)
            {
                if (t == null) continue;
                // read common fields
                var tType = t.GetType();
                var from = tType.GetField("From", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(t) as IState;
                var to = tType.GetField("To", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(t) as IState;
                var cond = tType
                    .GetField("Condition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(t) as Func<bool>;
                var debugDescField = tType.GetField("DebugDesc",
                                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                     tType.GetField("Description",
                                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var debugDescFunc = debugDescField != null ? debugDescField.GetValue(t) as Func<string> : null;
                var priorityField = tType.GetField("Priority",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                int priority = priorityField != null ? (int)(priorityField.GetValue(t) ?? 0) : 0;

                bool res = SafeInvoke(cond);
                string shortDesc = ShortConditionDesc(debugDescFunc, cond);

                float lastEval = ReadFloatFieldIfExists(t, "LastEvaluatedTime", -1f);
                float lastTrig = ReadFloatFieldIfExists(t, "LastTriggeredTime", -1f);

                // build single-line text
                string evalInfo = lastEval >= 0 ? $" eval@{lastEval:F2}" : "";
                string trigInfo = lastTrig >= 0 ? $" trig@{lastTrig:F2}" : "";
                string line =
                    $"[{priority}] {ShortStateName(from)} → {ShortStateName(to)}   [{shortDesc}] {evalInfo}{trigInfo}";

                var style = new GUIStyle(EditorStyles.label) { richText = false };
                style.wordWrap = false;
                style.clipping = TextClipping.Clip;
                style.normal.textColor = res ? Color.yellow : Color.grey;
                style.fontSize = 11;

                EditorGUILayout.LabelField(line, style);
            }
        }

        private void DrawAnyTransitionsColored(HFSM hfsm)
        {
            if (hfsm == null)
            {
                EditorGUILayout.LabelField("(no HFSM)");
                return;
            }

            var any = GetPrivateField<IEnumerable<object>>(hfsm, "_anyTransitions") ??
                      GetPrivateField<IEnumerable<object>>(hfsm, "anyTransitions") ??
                      GetPrivateField<IEnumerable<object>>(hfsm, "AnyTransitions");

            if (any == null)
            {
                EditorGUILayout.LabelField("(none)");
                return;
            }

            foreach (var item in any)
            {
                if (item == null) continue;
                var tType = item.GetType();
                var to = tType.GetField("To", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(item) as IState;
                var cond = tType
                    .GetField("Condition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(item) as Func<bool>;
                var debugDescField = tType.GetField("DebugDesc",
                                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                     tType.GetField("Description",
                                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var debugDescFunc = debugDescField != null ? debugDescField.GetValue(item) as Func<string> : null;
                int priority = ReadIntFieldIfExists(item, "Priority", 0);

                bool res = SafeInvoke(cond);
                string shortDesc = ShortConditionDesc(debugDescFunc, cond);
                float lastEval = ReadFloatFieldIfExists(item, "LastEvaluatedTime", -1f);
                float lastTrig = ReadFloatFieldIfExists(item, "LastTriggeredTime", -1f);
                string evalInfo = lastEval >= 0 ? $" eval@{lastEval:F2}" : "";
                string trigInfo = lastTrig >= 0 ? $" trig@{lastTrig:F2}" : "";
                string line = $"[{priority}] → {ShortStateName(to)}   [{shortDesc}]{evalInfo}{trigInfo}";

                var style = new GUIStyle(EditorStyles.label) { richText = false };
                style.normal.textColor = res ? Color.yellow : Color.grey;
                style.fontSize = 11;
                EditorGUILayout.LabelField(line, style);
            }
        }

        // ---------------- small helpers ----------------
        private string ShortStateName(IState s)
        {
            if (s == null) return "None";
            var n = s.GetType().Name;
            // strip common prefixes (S_, State_, etc.)
            n = n.Replace("S_", "").Replace("State", "");
            return n;
        }

        private bool SafeInvoke(Func<bool> cond)
        {
            if (cond == null) return false;
            try
            {
                return cond();
            }
            catch
            {
                return false;
            }
        }

        // produce short condition description: prefer debugDescFunc() string; else cond.Method name simplified; trim GameObject(...) verbose patterns
        private string ShortConditionDesc(Func<string> debugDescFunc, Func<bool> cond)
        {
            try
            {
                if (debugDescFunc != null)
                {
                    try
                    {
                        var s = debugDescFunc();
                        if (!string.IsNullOrEmpty(s)) return ShortenText(CleanVerboseNames(s));
                    }
                    catch
                    {
                        /* ignore */
                    }
                }

                if (cond != null)
                {
                    var m = cond.Method;
                    if (m != null)
                    {
                        // if it's a lambda method name or compiler generated, try to show simple fallback
                        string name = m.Name;
                        if (name.Contains("<") || name.Contains("lambda") || name.Contains("b__"))
                        {
                            // try to get declaring type + name, then shorten
                            var decl = m.DeclaringType?.Name ?? "";
                            string raw = decl + "." + name;
                            return ShortenText(CleanVerboseNames(raw));
                        }

                        // normal method, show name (and try remove long type name)
                        return ShortenText(CleanVerboseNames(name + "()"));
                    }
                }
            }
            catch
            {
            }

            return "[lambda]";
        }

        // remove "GameObject(EnemyFSM)" noise and long type paths
        private string CleanVerboseNames(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // heuristic: remove occurrences like "GameObject(EnemyFSM)" or "GameObject (EnemyFSM)"
            s = s.Replace("GameObject(", "").Replace(")", "");
            s = s.Replace("GameObject ", "");
            // remove fully-qualified type names like Namespace.Class -> Class
            var tokens = s.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 1)
            {
                // if likely a long token sequence, take last two tokens
                if (s.Length > 50)
                {
                    return tokens.Skip(Math.Max(0, tokens.Length - 2)).Aggregate((a, b) => a + "." + b);
                }
            }

            return s;
        }

        private string ShortenText(string s, int max = 45)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            if (s.Length <= max) return s;
            return s.Substring(0, max - 3).Trim() + "...";
        }

        private void DrawStateFields(IState state)
        {
            if (state == null) return;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var fields = state.GetType().GetFields(flags);
            if (fields == null || fields.Length == 0)
            {
                EditorGUILayout.LabelField(" (no fields)");
                return;
            }

            foreach (var f in fields)
            {
                object val = null;
                try
                {
                    val = f.GetValue(state);
                }
                catch
                {
                    val = "(err)";
                }

                string sVal = val == null ? "null" : val.ToString();
                EditorGUILayout.LabelField($" - {f.Name}: {sVal}", EditorStyles.miniLabel);
            }
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            if (obj == null) return default;
            var ty = obj.GetType();
            var fld = ty.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (fld == null) return default;
            try
            {
                return (T)fld.GetValue(obj);
            }
            catch
            {
                return default;
            }
        }

        private float ReadFloatFieldIfExists(object obj, string name, float fallback)
        {
            if (obj == null) return fallback;
            var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return fallback;
            try
            {
                var v = f.GetValue(obj);
                if (v is float fv) return fv;
                if (v is double dv) return (float)dv;
                if (v is int iv) return iv;
            }
            catch
            {
            }

            return fallback;
        }

        private int ReadIntFieldIfExists(object obj, string name, int fallback)
        {
            if (obj == null) return fallback;
            var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return fallback;
            try
            {
                var v = f.GetValue(obj);
                if (v is int iv) return iv;
                if (v is float fv) return (int)fv;
            }
            catch
            {
            }

            return fallback;
        }

        private void RefreshList()
        {
            allFSM = FindObjectsOfType<MonoBehaviour>(true).OfType<IFSMInspectable>().Where(x => x != null).ToList();
            Repaint();
        }
    }

#endif

}