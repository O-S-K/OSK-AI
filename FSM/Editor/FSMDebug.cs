namespace OSK.AIFSM
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OSK.AIFSM;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// FinalFSMDebuggerWindow - compact, colored, safe.
    /// - One-line transition display similar to HFSM screenshot.
    /// - Uses FinalStateMachine public API (Transition.GetDescription(), Priority, LastEvaluatedTime, LastTriggeredTime).
    /// - Guards against destroyed owners and balanced GUILayout (using scopes).
    /// </summary>
    [InitializeOnLoad]
    public class FSMDebug : EditorWindow
    {
        private Vector2 leftScroll, rightScroll;
        private double nextRefresh = 0;
        private float refreshInterval = 0.25f;
        private List<DetectedFSM> detected = new List<DetectedFSM>();
        private object selectedFSMOwner = null;
        private IState selectedStateObj = null;

        [MenuItem("OSK-AI/AI/FSM Debugger")]
        public static void Open() => GetWindow<FSMDebug>("Final FSM Debugger");

        private void OnEnable()
        {
            RefreshAll();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            RefreshAll();
        }

        public static void OpenFor(UnityEngine.Object owner)
        {
            var w = GetWindow<FSMDebug>("Final FSM Debugger");
            // refresh will repopulate detected list
            w.RefreshAll();
            // schedule selection on next Editor frame to allow RefreshAll() to populate
            EditorApplication.delayCall += () =>
            {
                try
                {
                    w.SelectOwnerSafe(owner);
                    w.Focus();
                    w.Repaint();
                }
                catch
                {
                    /* swallow */
                }
                // single-use, do not leave a lingering callback
            };
        }

        private void SelectOwnerSafe(UnityEngine.Object owner)
        {
            if (owner == null) return;

            // find the DetectedFSM that wraps this owner
            var det = detected.FirstOrDefault(d => (d.owner as UnityEngine.Object) == owner || Equals(d.owner, owner));
            if (det != null)
            {
                selectedFSMOwner = det.owner;
                selectedStateObj = null;
                // bring selection in Editor to the object too
                if (owner is UnityEngine.Object uo) Selection.activeObject = uo;
            }
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup >= nextRefresh)
            {
                Repaint();
                nextRefresh = EditorApplication.timeSinceStartup + refreshInterval;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    RefreshAll();

                GUILayout.Label($"Detected: {detected.Count}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                refreshInterval = EditorGUILayout.Slider(refreshInterval, 0.05f, 2f, GUILayout.Width(200));
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPanel();
                DrawRightPanel();
            }
        }

        // ---------------- LEFT PANEL ----------------
        private void DrawLeftPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.52f)))
            {
                EditorGUILayout.LabelField("Detected FSM owners", EditorStyles.boldLabel);

                if (detected.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Không tìm thấy FinalStateMachine trong scene. Expose FinalStateMachine bằng field/property hoặc implement IFSMInspectableFinal.",
                        MessageType.Info);
                    return;
                }

                foreach (var d in detected.ToArray())
                {
                    if (d == null || d.owner == null) continue;

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        try
                        {
                            // -------------------------------------------
                            // HEADER
                            // -------------------------------------------
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                string title = $"{d.friendlyName} ({SafeOwnerTypeName(d.owner)})";
                                if (GUILayout.Button(title, EditorStyles.label))
                                {
                                    var mb = d.owner as UnityEngine.Object;
                                    if (mb != null) Selection.activeObject = mb;
                                    selectedFSMOwner = d.owner;
                                    selectedStateObj = null;
                                }

                                if (GUILayout.Button("Select GameObject", GUILayout.Width(130)))
                                {
                                    var mb = d.owner as MonoBehaviour;
                                    if (mb != null) Selection.activeGameObject = mb.gameObject;
                                }
                            }

                            // -------------------------------------------
                            // CURRENT STATE
                            // -------------------------------------------
                            var cur = d.GetCurrentState();
                            string curName = cur?.GetType().Name ?? "null";
                            var curStyle = new GUIStyle(EditorStyles.miniLabel) { richText = false };
                            curStyle.normal.textColor = cur != null ? Color.green : EditorStyles.label.normal.textColor;

                            EditorGUILayout.LabelField("Current State:", curName, curStyle);

                            // -------------------------------------------
                            // AVAILABLE STATES (GRID — NO SCROLL)
                            // -------------------------------------------
                            EditorGUILayout.LabelField("Available States:", EditorStyles.boldLabel);
                            var states = d.GetStates();

                            if (states != null && states.Count > 0)
                            {
                                // Responsive columns
                                float viewWidth = EditorGUIUtility.currentViewWidth;
                                float usableWidth = Mathf.Max(200f, viewWidth / 2f);

                                int minButtonWidth = 120;
                                int columns = Mathf.Clamp(Mathf.FloorToInt(usableWidth / minButtonWidth), 1, 10);
                                float buttonWidth = usableWidth / columns;
                                float buttonHeight = 22;

                                int total = states.Count;
                                int rows = Mathf.CeilToInt((float)total / columns);

                                for (int r = 0; r < rows; r++)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        for (int c = 0; c < columns; c++)
                                        {
                                            int idx = r * columns + c;
                                            if (idx >= total)
                                            {
                                                GUILayout.Label("", GUILayout.Width(buttonWidth));
                                                continue;
                                            }

                                            var s = states[idx];
                                            if (s == null)
                                            {
                                                GUILayout.Label("", GUILayout.Width(buttonWidth));
                                                continue;
                                            }

                                            bool isCur = (cur == s);

                                            var st = new GUIStyle(EditorStyles.miniButton)
                                            {
                                                alignment = TextAnchor.MiddleLeft,
                                            };
                                            st.normal.textColor = isCur ? Color.green : EditorStyles.label.normal.textColor;

                                            if (GUILayout.Button($" {ShortStateName(s)} ", st,
                                                    GUILayout.Width(buttonWidth),
                                                    GUILayout.Height(buttonHeight)))
                                            {
                                                selectedFSMOwner = d.owner;
                                                selectedStateObj = s;
                                            }
                                        }
                                    }
                                }
                            }

                            // -------------------------------------------
                            // TRANSITIONS COUNT
                            // -------------------------------------------
                            int anyCount = d.GetAnyTransitionsCount();
                            int transCount = d.GetTransitionsCount();
                            EditorGUILayout.LabelField($"Transitions: {transCount}   AnyTransitions: {anyCount}");
                        }
                        catch (Exception ex)
                        {
                            EditorGUILayout.LabelField($"Error reading FSM: {ex.Message}");
                        }
                    }
                }
            }
        }

        // ---------------- RIGHT PANEL ----------------
        private void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

                if (selectedFSMOwner == null)
                {
                    EditorGUILayout.HelpBox("Chọn một FSM owner bên trái để xem chi tiết.", MessageType.Info);
                    return;
                }

                var det = detected.FirstOrDefault(x => x.owner == selectedFSMOwner);
                if (det == null || det.owner == null)
                {
                    EditorGUILayout.LabelField("Selected FSM disappeared.");
                    return;
                }

                // safety: if owner is destroyed, clear selection
                if (IsOwnerDestroyed(det.owner))
                {
                    EditorGUILayout.LabelField("Selected owner destroyed.");
                    selectedFSMOwner = null;
                    selectedStateObj = null;
                    return;
                }

                if (det.owner == null)
                {
                    EditorGUILayout.LabelField("Owner:", "None (Destroyed)");
                }
                else
                {
                    var mb = det.owner as MonoBehaviour;
                    string ownerName = mb != null ? mb.gameObject.name : SafeOwnerTypeName(det.owner);
                    EditorGUILayout.LabelField("Owner:", ownerName);
                }

                if (GUILayout.Button("Ping Owner Object"))
                {
                    var mb = det.owner as MonoBehaviour;
                    if (mb != null) EditorGUIUtility.PingObject(mb.gameObject);
                }

                // Current state
                var cur = det.GetCurrentState();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
                var bigStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = cur != null ? Color.green : Color.white } };
                EditorGUILayout.LabelField(cur?.GetType().Name ?? "null", bigStyle);

                // Per-state transitions
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Transitions (per-state)", EditorStyles.boldLabel);
                var allTrans = det.GetAllTransitions();
                if (allTrans == null || allTrans.Count == 0)
                {
                    EditorGUILayout.LabelField("(no transitions found)");
                }
                else
                {
                    foreach (var t in allTrans)
                    {
                        if (t == null) continue;

                        bool cond = false;
                        try
                        {
                            cond = t.Condition?.Invoke() ?? false;
                        }
                        catch
                        {
                            cond = false;
                        }

                        bool triggered = Mathf.Abs(Time.time - t.LastTriggeredTime) < 0.05f;

                        string desc = SafeDesc(t);
                        string left = t.FromName ?? "Any";
                        string right = t.ToName ?? "None";

                        string evalInfo = t.LastEvaluatedTime >= 0 ? $" eval@{t.LastEvaluatedTime:F2}" : "";
                        string trigInfo = t.LastTriggeredTime >= 0 ? $" trig@{t.LastTriggeredTime:F2}" : "";

                        // Color highlight:
                        // - Green → transition just triggered
                        // - Yellow → condition true
                        // - Gray → condition false
                        string color =
                            triggered ? "#00FF00" : // bright green
                            cond ? "#FFD966" : // yellow
                            "#9B9B9B"; // gray

                        string line =
                            $"<color={color}>[{t.Priority}] {ShortStateName(left)} → {ShortStateName(right)}  [{desc} {(cond ? "✅" : "✖")}]</color>" +
                            $"{(string.IsNullOrEmpty(evalInfo) ? "" : $" <color=#888888>{evalInfo.Trim()}</color>")}" +
                            $"{(string.IsNullOrEmpty(trigInfo) ? "" : $" <color=#888888>{trigInfo.Trim()}</color>")}";

                        var style = new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = false };
                        EditorGUILayout.LabelField(line, style);
                    }
                }

                // Any transitions
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Any Transitions", EditorStyles.boldLabel);
                var anyTransitions = det.GetAnyTransitions();
                if (anyTransitions == null || anyTransitions.Count == 0)
                {
                    EditorGUILayout.LabelField("(none)");
                }
                else
                {
                    foreach (var at in anyTransitions)
                    {
                        if (at == null) continue;

                        bool cond = at.Result == true;
                        bool triggered = at.LastTriggeredTime >= 0 && Mathf.Abs(Time.time - at.LastTriggeredTime) < 0.1f;

                        string desc = SafeDesc(at);
                        string to = at.ToName ?? "None";

                        string evalInfo = at.LastEvaluatedTime >= 0 ? $" eval@{at.LastEvaluatedTime:F2}" : "";
                        string trigInfo = at.LastTriggeredTime >= 0 ? $" trig@{at.LastTriggeredTime:F2}" : "";

                        string color =
                            triggered ? "#00FF00" :
                            cond ? "#FFD966" :
                            "#9B9B9B";

                        string line =
                            $"<color={color}>[{at.Priority}] → {ShortStateName(to)}  [{desc} {(cond ? "✅" : "✖")}]</color>" +
                            $"{(string.IsNullOrEmpty(evalInfo) ? "" : $" <color=#888888>{evalInfo.Trim()}</color>")}" +
                            $"{(string.IsNullOrEmpty(trigInfo) ? "" : $" <color=#888888>{trigInfo.Trim()}</color>")}";

                        var style = new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = false };
                        EditorGUILayout.LabelField(line, style);
                    }
                }

                // selected state fields
                EditorGUILayout.Space();
                if (selectedStateObj != null)
                {
                    EditorGUILayout.LabelField("Selected State Fields", EditorStyles.boldLabel);
                    DrawStateFields(selectedStateObj);
                }
            }
        }

        // ---------------- Helpers ----------------
        private string SafeDesc(FinalStateMachine.Transition t)
        {
            try
            {
                // prefer GetDescription which we implemented in FSM
                var s = t.GetDescription();
                return ShortenAndClean(s);
            }
            catch
            {
                return "[desc]";
            }
        }

        private string ShortStateName(IState s)
        {
            if (s == null) return "None";
            return ShortStateName(s.GetType().Name);
        }

        private string ShortStateName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var n = raw.Replace("S_", "").Replace("State", "");
            return n;
        }

        private string ShortenAndClean(string s, int max = 36)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // remove GameObject(...) noise
            s = s.Replace("GameObject(", "").Replace(")", "");
            s = s.Replace("UnityEngine.", "");
            // trim long fully-qualified names
            var parts = s.Split('.');
            if (s.Length > max && parts.Length > 1)
                s = string.Join(".", parts.Skip(Math.Max(0, parts.Length - 2)));
            if (s.Length > max) s = s.Substring(0, max - 3).Trim() + "...";
            return s;
        }

        private GUIStyle HighlightStyle(bool highlight)
        {
            var s = new GUIStyle(EditorStyles.boldLabel);
            s.normal.textColor = highlight ? Color.green : EditorStyles.label.normal.textColor;
            return s;
        }

        private void DrawStateFields(IState state)
        {
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;
            var fields = state.GetType().GetFields(flags);
            if (fields.Length == 0)
            {
                EditorGUILayout.LabelField("(no fields)");
                return;
            }

            foreach (var f in fields)
            {
                object v = null;
                try
                {
                    v = f.GetValue(state);
                }
                catch
                {
                    v = "(err)";
                }

                EditorGUILayout.LabelField($" - {f.Name}: {v ?? "null"}", EditorStyles.miniLabel);
            }
        }

        private bool IsOwnerDestroyed(object owner)
        {
            if (owner == null) return true;
            var u = owner as UnityEngine.Object;
            if (u != null) return u.Equals(null);
            return false;
        }

        private string SafeOwnerTypeName(object owner)
        {
            if (owner == null) return "Unknown";
            return owner.GetType().Name;
        }

        // ---------------- Discovery ----------------
        private void RefreshAll()
        {
            detected.Clear();
            // scan all MonoBehaviours in scene (including disabled)
            var allMB = Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(x => x.gameObject.scene.isLoaded).ToArray();
            foreach (var mb in allMB)
            {
                try
                {
                    // prefer interface IFSMInspectableFinal if implemented
                    var iface = mb.GetType().GetInterfaces().FirstOrDefault(i => i.Name == "IFSMInspectableFinal");
                    if (iface != null)
                    {
                        var method = iface.GetMethod("GetFinalFSM");
                        var nameMethod = iface.GetMethod("GetFSMName");
                        if (method != null)
                        {
                            var fsmObj = method.Invoke(mb, null) as FinalStateMachine;
                            string fname = nameMethod != null ? (nameMethod.Invoke(mb, null) as string) : mb.gameObject.name;
                            if (fsmObj != null)
                            {
                                detected.Add(new DetectedFSM(mb, fsmObj, fname));
                                continue;
                            }
                        }
                    }

                    // try fields/properties of type FinalStateMachine
                    var type = mb.GetType();
                    var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                                System.Reflection.BindingFlags.NonPublic |
                                                System.Reflection.BindingFlags.Instance);
                    bool found = false;
                    foreach (var f in fields)
                    {
                        if (f.FieldType == typeof(FinalStateMachine))
                        {
                            var fsm = f.GetValue(mb) as FinalStateMachine;
                            if (fsm != null)
                            {
                                detected.Add(new DetectedFSM(mb, fsm, mb.gameObject.name + "." + f.Name));
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found) continue;

                    var props = type.GetProperties(System.Reflection.BindingFlags.Public |
                                                   System.Reflection.BindingFlags.NonPublic |
                                                   System.Reflection.BindingFlags.Instance);
                    foreach (var p in props)
                    {
                        if (p.PropertyType == typeof(FinalStateMachine) && p.GetIndexParameters().Length == 0)
                        {
                            FinalStateMachine fsm = null;
                            try
                            {
                                fsm = p.GetValue(mb) as FinalStateMachine;
                            }
                            catch
                            {
                                fsm = null;
                            }

                            if (fsm != null)
                            {
                                detected.Add(new DetectedFSM(mb, fsm, mb.gameObject.name + "." + p.Name));
                                found = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    /* ignore single MB issues */
                }
            }

            // clear selection if selected owner destroyed or removed
            if (selectedFSMOwner != null && !detected.Any(d => d.owner == selectedFSMOwner))
            {
                selectedFSMOwner = null;
                selectedStateObj = null;
            }

            Repaint();
        }

        // Wrapper class that uses public FinalStateMachine API (no heavy reflection)
        private class DetectedFSM
        {
            public object owner;
            public FinalStateMachine fsm;
            public string friendlyName;

            public DetectedFSM(object owner, FinalStateMachine fsm, string name)
            {
                this.owner = owner;
                this.fsm = fsm;
                this.friendlyName = name;
            }

            public IState GetCurrentState()
            {
                try
                {
                    return fsm.GetCurrentState();
                }
                catch
                {
                    return null;
                }
            }

            public List<IState> GetStates()
            {
                try
                {
                    return fsm.GetStates();
                }
                catch
                {
                    return null;
                }
            }

            public List<FinalStateMachine.Transition> GetAllTransitions()
            {
                try
                {
                    var all = new List<FinalStateMachine.Transition>();
                    var states = GetStates();
                    if (states != null)
                    {
                        foreach (var s in states)
                        {
                            var list = fsm.GetTransitionsForState(s);
                            if (list != null && list.Count > 0) all.AddRange(list);
                        }
                    }

                    return all;
                }
                catch
                {
                    return null;
                }
            }

            public List<FinalStateMachine.Transition> GetAnyTransitions()
            {
                try
                {
                    return fsm.GetAnyTransitions();
                }
                catch
                {
                    return null;
                }
            }

            public int GetTransitionsCount()
            {
                var all = GetAllTransitions();
                return all?.Count ?? 0;
            }

            public int GetAnyTransitionsCount()
            {
                var a = GetAnyTransitions();
                return a?.Count ?? 0;
            }
        }
    }
#endif
}