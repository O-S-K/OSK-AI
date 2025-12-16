#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace OSK.AIFSM
{
    // ---------- Drawer (giữ UI chính, thêm nút mở cửa sổ debug) ----------
    public class TransitionDataDrawer : OdinValueDrawer<TransitionData>
    { 
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var td = this.ValueEntry.SmartValue;
            if (td == null) return;

            if (td.targetObject == null) td.targetObject = this.Property.Tree.WeakTargets.FirstOrDefault();

            var path = this.Property.Path ?? string.Empty;
            td.hideFromField = path.Contains("anyTransitions");
            td.hideToField = false;

            bool isExitList = path.Contains("exitTransitions");
            var compType = GetRealTargetType(td.targetObject);

            // Grouping header logic
            bool isHeader = false;
            int myIndex = this.Property.Index;
            var list = this.Property.Parent.ValueEntry.WeakSmartValue as System.Collections.IList;
            if (!td.hideFromField && list != null)
            {
                if (myIndex == 0) isHeader = true;
                else
                {
                    var prev = list[myIndex - 1] as TransitionData;
                    if (prev == null || prev.fromFieldName != td.fromFieldName) isHeader = true;
                }
            }
            else if (td.hideFromField && myIndex == 0) isHeader = true;

            // header
            if (isHeader)
            {
                GUILayout.Space(12);
                var stateName = string.IsNullOrEmpty(td.fromFieldName) ? "Any" : td.fromFieldName;
                var colorHash = Mathf.Abs(stateName.GetHashCode()) % 100;
                var headerColor = Color.HSVToRGB((colorHash / 100f) * 0.8f, 0.35f, 0.7f);
                if (td.hideFromField) headerColor = new Color(0.3f, 0.3f, 0.3f);
                headerColor.a = 0.6f;

                bool isActive = false;
                if (Application.isPlaying && !td.hideFromField)
                {
                    isActive = FSMEditorUtils.IsCurrentStateActive(td.targetObject, td.fromFieldName);
                    if (isActive) headerColor = new Color(0.1f, 0.8f, 0.2f, 0.9f);
                }

                SirenixEditorGUI.BeginHorizontalToolbar(28);
                {
                    var rect = GUIHelper.GetCurrentLayoutRect();
                    EditorGUI.DrawRect(rect, headerColor);
                    if (isActive)
                    {
                        GUIHelper.PushColor(Color.white);
                        SirenixEditorGUI.DrawBorders(rect, 1, new Color(0, 1, 0, 1));
                        GUIHelper.PopColor();
                    }

                    GUILayout.Space(5);
                    var iconName = isActive ? "d_PlayButton" : (td.hideFromField ? "NetworkView Icon" : "Folder Icon");
                    GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(20), GUILayout.Height(18));

                    GUIHelper.PushColor(Color.white);
                    GUILayout.Label("FROM:", SirenixGUIStyles.BoldLabel, GUILayout.Width(45));

                    if (!td.hideFromField)
                    {
                        var stateNames = FSMEditorUtils.GetStateFieldNamesForType(compType);
                        int fromIdx = Array.IndexOf(stateNames, td.fromFieldName);

                        var headerDropStyle = new GUIStyle(GUI.skin.button);
                        headerDropStyle.normal.textColor = Color.white;
                        headerDropStyle.fontStyle = FontStyle.Bold;
                        headerDropStyle.alignment = TextAnchor.MiddleLeft;
                        if (isActive) headerDropStyle.fontSize = 12;

                        int newIdx = EditorGUILayout.Popup(fromIdx, stateNames, headerDropStyle, GUILayout.Width(200));
                        if (newIdx != fromIdx && newIdx >= 0)
                        {
                            td.fromFieldName = stateNames[newIdx];
                            EditorUtility.SetDirty(td.targetObject as UnityEngine.Object);
                        }

                        if (isActive) GUILayout.Label("->>> (ACTIVE) <<<-");

                        GUILayout.FlexibleSpace();

                        // // Debug button: mở popup window
                        // if (GUILayout.Button(new GUIContent("Debug...", "Open FSM debug window"), GUILayout.Width(70), GUILayout.Height(18)))
                        // {
                        //     FSMDebugWindow.ShowWindow(td.targetObject);
                        // }
                    }
                    else
                    {
                        GUILayout.Label(isExitList ? "EXIT TRANSITIONS" : "ANY TRANSITIONS", SirenixGUIStyles.BoldLabel);
                    }

                    GUIHelper.PopColor();
                }
                SirenixEditorGUI.EndHorizontalToolbar();
            }

            // body (giữ nguyên)
            SirenixEditorGUI.BeginHorizontalToolbar();
            {
                #region State To
                if (!td.hideToField)
                {
                    var stateNamesTo = FSMEditorUtils.GetStateFieldNamesForType(compType);
                    var extraOptions = new List<string> { "(Reset To Start)" };
                    var fullOptions = extraOptions.Concat(stateNamesTo).ToArray();
                    int toIdx = Array.IndexOf(fullOptions, td.toFieldName);

                    bool isPureExit = isExitList && toIdx < 0;

                    Color chipColor;
                    if (isPureExit) chipColor = new Color(0.3f, 0.3f, 0.3f);
                    else if (toIdx >= 0) chipColor = new Color(0.8f, 0.9f, 1f);
                    else chipColor = new Color(1f, 0.6f, 0.6f);

                    GUIHelper.PushColor(chipColor);

                    if (isPureExit)
                    {
                        int newIdx = EditorGUILayout.Popup(-1, fullOptions, GUILayout.Width(110));
                        if (newIdx >= 0) td.toFieldName = fullOptions[newIdx];
                        var lastRect = GUILayoutUtility.GetLastRect();
                        GUI.Label(lastRect, "⛔ Pure Exit", SirenixGUIStyles.CenteredGreyMiniLabel);
                    }
                    else
                    {
                        int newToIdx = SirenixEditorFields.Dropdown(toIdx, fullOptions, GUILayout.MinWidth(100));
                        if (newToIdx != toIdx && newToIdx >= 0) td.toFieldName = fullOptions[newToIdx];
                    }

                    GUIHelper.PopColor();
                }
                

                #endregion
                GUILayout.Label("➜", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.Width(20));
                #region Condition
                if (!td.hideFromField) GUILayout.Space(24);
                GUILayout.Label(EditorGUIUtility.IconContent("cs Script Icon"), GUILayout.Width(20), GUILayout.Height(18));
                var condNames = FSMEditorUtils.GetMethodListForType(compType);
                int condIdx = Array.IndexOf(condNames, td.conditionMethod);
                if (condIdx < 0) GUIHelper.PushColor(new Color(1f, 0.7f, 0.7f));
                int newCondIdx = SirenixEditorFields.Dropdown(condIdx, condNames, GUILayout.Width(200));
                if (condIdx < 0) GUIHelper.PopColor();
                if (newCondIdx != condIdx) td.conditionMethod = (newCondIdx >= 0 && condNames.Length > 0) ? condNames[newCondIdx] : null;

                td.invertCondition = GUILayout.Toggle(td.invertCondition, "!", GUILayout.Width(18));
                if (td.conditionMethod != null && td.conditionMethod.EndsWith("(param)"))
                    td.conditionParam = EditorGUILayout.TextField(td.conditionParam, GUILayout.Width(50));

                #endregion

                #region Priority
                GUILayout.Label("P:", GUILayout.Width(12));
                int newPriority = EditorGUILayout.IntField(td.priority, GUILayout.Width(30));
                if (newPriority != td.priority) td.priority = newPriority;

                #endregion
              
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(EditorGUIUtility.IconContent("d_CreateAddNew"), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20)))
                {
                    if (list != null)
                    {
                        var newItem = new TransitionData();
                        newItem.fromFieldName = td.fromFieldName;
                        list.Insert(myIndex + 1, newItem);
                        GUIHelper.RequestRepaint();
                    }
                }

                GUILayout.Space(2);

                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20)))
                {
                    if (list != null)
                    {
                        list.RemoveAt(myIndex);
                        GUIHelper.RequestRepaint();
                    }
                }
            }
            SirenixEditorGUI.EndHorizontalToolbar();

            if (Application.isPlaying) GUIHelper.RequestRepaint();

            this.ValueEntry.SmartValue = td;
        }

        // --- Helpers (reuse some utility functions from FSMEditorUtils) ---
        private Type GetRealTargetType(object target) => target?.GetType();
    }

    // ---------- EditorWindow: Popup debug ----------
    public class FSMDebugWindow : EditorWindow
    {
        private UnityEngine.Object _owner;
        private Type _ownerType;
        private Vector2 _scroll;
        private string _detectedActive;
        private string _detectedNext;

        public static void ShowWindow(object owner)
        {
            if (owner == null) return;
            var win = GetWindow<FSMDebugWindow>(true, "FSM Debug", true);
            win.minSize = new Vector2(480, 400);
            win._owner = owner as UnityEngine.Object;
            win._ownerType = owner.GetType();
            win.Refresh();
            win.Show();
        }

        private void OnGUI()
        {
            if (_owner == null)
            {
                EditorGUILayout.HelpBox("No owner selected or owner is null.", MessageType.Warning);
                if (GUILayout.Button("Close")) Close();
                return;
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField($"Owner: {_ownerType.Name}", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            {
                Refresh();
            }

            if (GUILayout.Button("Ping Owner", GUILayout.Width(100)))
            {
                EditorGUIUtility.PingObject(_owner);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Detected Active:", GUILayout.Width(120));
            EditorGUILayout.SelectableLabel(_detectedActive ?? "<null>", GUILayout.Height(16));
            GUILayout.Space(2);
            EditorGUILayout.LabelField("Detected Next:", GUILayout.Width(120));
            EditorGUILayout.SelectableLabel(_detectedNext ?? "<null>", GUILayout.Height(16));
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Candidate members (fields & properties)", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            RenderMembersRecursive(_owner, _ownerType, "", 0);
            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            FSMEditorUtils.TryGetActiveAndNextStateNames(_owner, out _detectedActive, out _detectedNext);
            Repaint();
        }

        // And add this method inside FSMDebugWindow class:
        private void RenderMembersRecursive(object targetRoot, Type rootType, string memberPathPrefix, int indent)
        {
            // memberPathPrefix = "" for root members; otherwise "parentField.childField"
            var indentSpace = GUILayout.Width(18 * indent);
            foreach (var mem in FSMEditorUtils.GetCandidateMemberNames(rootType))
            {
                string fullPath = string.IsNullOrEmpty(memberPathPrefix) ? mem : memberPathPrefix + "." + mem;
                var val = FSMEditorUtils.GetMemberValueByPath(targetRoot, fullPath);
                var valType = val?.GetType() ?? FSMEditorUtils.GetMemberType(rootType, mem);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(18 * indent);
                EditorGUILayout.LabelField(mem, GUILayout.Width(200 - 18 * indent));

                if (val == null)
                {
                    EditorGUILayout.LabelField("<null>", GUILayout.Height(16), GUILayout.Width(200));
                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                        /* nothing */
                    }
                }
                else if (val is bool b)
                {
                    bool newB = EditorGUILayout.Toggle(b, GUILayout.Width(60));
                    if (newB != b)
                    {
                        FSMEditorUtils.SetMemberValueByPath(targetRoot, fullPath, newB);
                        FSMEditorUtils.TryGetActiveAndNextStateNames(_owner, out _detectedActive, out _detectedNext);
                        Repaint();
                    }

                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                    }
                }
                else if (val is int i)
                {
                    int newI = EditorGUILayout.IntField(i, GUILayout.Width(80));
                    if (newI != i)
                    {
                        FSMEditorUtils.SetMemberValueByPath(targetRoot, fullPath, newI);
                        FSMEditorUtils.TryGetActiveAndNextStateNames(_owner, out _detectedActive, out _detectedNext);
                        Repaint();
                    }

                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                    }
                }
                else if (val is float f)
                {
                    float newF = EditorGUILayout.FloatField(f, GUILayout.Width(80));
                    if (Math.Abs(newF - f) > Mathf.Epsilon)
                    {
                        FSMEditorUtils.SetMemberValueByPath(targetRoot, fullPath, newF);
                        FSMEditorUtils.TryGetActiveAndNextStateNames(_owner, out _detectedActive, out _detectedNext);
                        Repaint();
                    }

                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                    }
                }
                else if (val is string s)
                {
                    string newS = EditorGUILayout.TextField(s, GUILayout.Width(200));
                    if (newS != s)
                    {
                        FSMEditorUtils.SetMemberValueByPath(targetRoot, fullPath, newS);
                        FSMEditorUtils.TryGetActiveAndNextStateNames(_owner, out _detectedActive, out _detectedNext);
                        Repaint();
                    }

                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                    }
                }
                else if (val is Vector3 v3)
                {
                    Vector3 newV3 = EditorGUILayout.Vector3Field("", v3);
                    if (newV3 != v3)
                    {
                        FSMEditorUtils.SetMemberValueByPath(targetRoot, fullPath, newV3);
                        FSMEditorUtils.TryGetActiveAndNextStateNames(_owner, out _detectedActive, out _detectedNext);
                        Repaint();
                    }

                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                    }
                }
                else if (val is UnityEngine.Object uo)
                {
                    EditorGUILayout.ObjectField(uo, uo.GetType(), true, GUILayout.Width(200));
                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                        Selection.activeObject = uo;
                        EditorGUIUtility.PingObject(uo);
                    }
                }
                else
                {
                    // If it's a complex object, show foldout to drill down
                    bool isFolded = FSMEditorUtils.ToggleFoldout(fullPath);
                    if (GUILayout.Button(isFolded ? "▼" : "▶", GUILayout.Width(24)))
                    {
                        FSMEditorUtils.SetFoldout(fullPath, !isFolded);
                        Repaint();
                    }

                    EditorGUILayout.SelectableLabel(val.ToString(), GUILayout.Height(16), GUILayout.Width(140));
                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                        try
                        {
                            OdinEditorWindow.InspectObject(val);
                        }
                        catch
                        {
                            Debug.Log($"Inspect: {val.GetType().Name}");
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();

                // If this member is folded-out and is complex, recurse into its fields
                if (val != null && !(val is ValueType) && !(val is string) && !(val is UnityEngine.Object))
                {
                    if (FSMEditorUtils.IsFolded(fullPath))
                    {
                        RenderMembersRecursive(targetRoot, val.GetType(), fullPath, indent + 1);
                    }
                }
            }
        }
    }


    // ---------- Utility helpers shared by Drawer & Window ----------
    public static partial class FSMEditorUtils
    {
        private const string CURRENT_STATE_VAR_NAME = "_currentState";
        private static readonly Dictionary<Type, string[]> _methodCache = new();
        private static readonly Dictionary<Type, string[]> _stateFieldCache = new();

        public static string[] GetMethodListForType(Type compType)
        {
            if (compType == null) return new string[0];
            if (_methodCache.TryGetValue(compType, out var list)) return list;
            try
            {
                var methods = compType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.ReturnType == typeof(bool) && (m.GetParameters().Length == 0 || m.GetParameters().Length == 1))
                    .Select(m => m.Name + (m.GetParameters().Length == 1 ? " (param)" : "")).OrderBy(n => n).ToArray();
                _methodCache[compType] = methods;
                return methods;
            }
            catch
            {
                return new string[0];
            }
        }

        public static string[] GetStateFieldNamesForType(Type compType)
        {
            if (compType == null) return new string[0];
            if (_stateFieldCache.TryGetValue(compType, out var arr)) return arr;
            try
            {
                var fields = compType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(f => typeof(IState).IsAssignableFrom(f.FieldType)).Select(f => f.Name).OrderBy(n => n).ToArray();
                _stateFieldCache[compType] = fields;
                return fields;
            }
            catch
            {
                return new string[0];
            }
        }

        public static IEnumerable<string> GetCandidateMemberNames(Type t)
        {
            if (t == null) yield break;
            var names = new List<string>();
            names.AddRange(new[] { "currentState", "activeState", "runningState", "_currentState", "m_currentState", "CurrentState", "CurrentStateName", "nextState", "pendingState", "targetState" });
            names.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(f => f.Name));
            names.AddRange(t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(p => p.Name));
            foreach (var n in names.Distinct()) yield return n;
        }
 

        public static void TryGetActiveAndNextStateNames(object target, out string activeStateName, out string nextStateName)
        {
            activeStateName = null;
            nextStateName = null;
            if (target == null) return;
            Type t = target.GetType();

            string[] activeCandidates = new[] { "currentState", "activeState", "runningState", "_currentState", "m_currentState", "CurrentState", "CurrentStateName", "current" };
            string[] nextCandidates = new[] { "nextState", "pendingState", "_nextState", "m_nextState", "targetState", "NextState", "pending" };

            object ReadMember(string name)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (f != null) return f.GetValue(target);
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (p != null) return p.GetValue(target);
                return null;
            }

            foreach (var c in activeCandidates)
            {
                try
                {
                    var val = ReadMember(c);
                    if (val == null) continue;
                    if (val is string s)
                    {
                        activeStateName = s;
                        break;
                    }

                    if (val is IState state)
                    {
                        var nameProp = state.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                        if (nameProp != null)
                        {
                            activeStateName = nameProp.GetValue(state) as string;
                            break;
                        }

                        activeStateName = FindOwnerFieldNameForStateInstance(target, state) ?? state.GetType().Name;
                        break;
                    }
                }
                catch
                {
                }
            }

            foreach (var c in nextCandidates)
            {
                try
                {
                    var val = ReadMember(c);
                    if (val == null) continue;
                    if (val is string s)
                    {
                        nextStateName = s;
                        break;
                    }

                    if (val is IState state)
                    {
                        var nameProp = state.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                        if (nameProp != null)
                        {
                            nextStateName = nameProp.GetValue(state) as string;
                            break;
                        }

                        nextStateName = FindOwnerFieldNameForStateInstance(target, state) ?? state.GetType().Name;
                        break;
                    }
                }
                catch
                {
                }
            }

            // final fallback: try property "CurrentStateName" on owner
            try
            {
                var p = t.GetProperty("CurrentStateName", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && string.IsNullOrEmpty(activeStateName))
                    activeStateName = p.GetValue(target) as string;
            }
            catch
            {
            }
        }

        public static string FindOwnerFieldNameForStateInstance(object owner, IState stateInstance)
        {
            if (owner == null || stateInstance == null) return null;
            var t = owner.GetType();
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Where(f => typeof(IState).IsAssignableFrom(f.FieldType));
            foreach (var f in fields)
            {
                try
                {
                    var val = f.GetValue(owner);
                    if (val == stateInstance) return f.Name;
                    if (val != null && val.GetType().Name == stateInstance.GetType().Name) return f.Name;
                }
                catch
                {
                }
            }

            return null;
        }

        public static bool IsCurrentStateActive(object target, string stateFieldName)
        {
            if (target == null || string.IsNullOrEmpty(stateFieldName)) return false;
            try
            {
                var t = target.GetType();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;

                var stateField = t.GetField(stateFieldName, flags);
                if (stateField == null) return false;

                var stateToCheck = stateField.GetValue(target);
                if (stateToCheck == null) return false;

                // 1) Try direct current field on owner
                var currentFieldDirect = t.GetField(CURRENT_STATE_VAR_NAME, flags);
                if (currentFieldDirect != null)
                {
                    var activeStateRuntime = currentFieldDirect.GetValue(target);
                    if (activeStateRuntime != null && ReferenceEquals(stateToCheck, activeStateRuntime)) return true;
                }

                // 2) Try any nested FSM field of type FinalStateMachine (or similar)
                var fsmField = t.GetFields(flags)
                    .FirstOrDefault(f => f.FieldType == typeof(FinalStateMachine) || f.FieldType.Name.Contains("FinalStateMachine"));
                if (fsmField != null)
                {
                    var fsmInstance = fsmField.GetValue(target);
                    if (fsmInstance != null)
                    {
                        var innerStateField = fsmInstance.GetType().GetField(CURRENT_STATE_VAR_NAME, flags);
                        if (innerStateField != null)
                        {
                            var activeStateRuntime = innerStateField.GetValue(fsmInstance);
                            if (activeStateRuntime != null && ReferenceEquals(stateToCheck, activeStateRuntime)) return true;
                        }
                    }
                }

                // 3) Try to read a string property/field "CurrentStateName" or similar from owner and compare names
                string ownerStateName = TryReadStateNameFromOwner(target);
                if (!string.IsNullOrEmpty(ownerStateName))
                {
                    if (AreStateNamesEquivalent(ownerStateName, stateFieldName)) return true;
                    if (stateToCheck != null && AreStateNamesEquivalent(ownerStateName, stateToCheck.GetType().Name)) return true;
                }

                // 4) fallback to type name compare
                if (stateToCheck != null)
                {
                    var fieldType = stateField.FieldType;
                    if (stateToCheck.GetType().Name == fieldType.Name) return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string TryReadStateNameFromOwner(object owner)
        {
            if (owner == null) return null;
            var t = owner.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            string[] candidates = new[] { "CurrentStateName", "currentStateName", "CurrentState", "currentState", "current", "_currentStateName" };

            foreach (var c in candidates)
            {
                try
                {
                    var f = t.GetField(c, flags);
                    if (f != null)
                    {
                        var v = f.GetValue(owner);
                        if (v is string s) return s;
                        if (v is IState st)
                        {
                            var p = st.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                            if (p != null) return p.GetValue(st) as string;
                            return st.GetType().Name;
                        }
                    }

                    var pInfo = t.GetProperty(c, flags);
                    if (pInfo != null)
                    {
                        var v = pInfo.GetValue(owner);
                        if (v is string s2) return s2;
                        if (v is IState st2)
                        {
                            var p2 = st2.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                            if (p2 != null) return p2.GetValue(st2) as string;
                            return st2.GetType().Name;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public static bool AreStateNamesEquivalent(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            string Norm(string s) => s.EndsWith("State", StringComparison.OrdinalIgnoreCase) ? s.Substring(0, s.Length - 5) : s;
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // --- Setter: write back value to target's field/property ---
        public static bool SetMemberValue(object target, string memberName, object newValue)
        {
            if (target == null) return false;
            var t = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            try
            {
                var f = t.GetField(memberName, flags);
                if (f != null)
                {
                    var converted = ConvertIfNeeded(newValue, f.FieldType);
                    f.SetValue(target, converted);
                    return true;
                }

                var p = t.GetProperty(memberName, flags);
                if (p != null && p.CanWrite)
                {
                    var converted = ConvertIfNeeded(newValue, p.PropertyType);
                    p.SetValue(target, converted);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FSMEditorUtils.SetMemberValue failed for {memberName}: {e.Message}");
            }

            return false;
        }

        private static object ConvertIfNeeded(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;
            try
            {
                if (targetType.IsEnum)
                {
                    if (value is string s) return Enum.Parse(targetType, s);
                    return Enum.ToObject(targetType, value);
                }

                if (targetType == typeof(int)) return Convert.ToInt32(value);
                if (targetType == typeof(float)) return Convert.ToSingle(value);
                if (targetType == typeof(bool)) return Convert.ToBoolean(value);
                if (targetType == typeof(string)) return value.ToString();
                if (targetType == typeof(Vector3) && value is Vector3) return value;
                if (targetType == typeof(Vector2) && value is Vector2) return value;
                // Try ChangeType fallback
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        // foldout state storage (per-editor session)
        private static HashSet<string> _foldouts = new HashSet<string>();

        public static bool ToggleFoldout(string key)
        {
            return IsFolded(key);
        }

        public static bool IsFolded(string key) => _foldouts.Contains(key);

        public static void SetFoldout(string key, bool v)
        {
            if (v) _foldouts.Add(key);
            else _foldouts.Remove(key);
        }

// Get member Type for a member name on a type (field or property)
        public static Type GetMemberType(Type ownerType, string memberName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            var f = ownerType.GetField(memberName, flags);
            if (f != null) return f.FieldType;
            var p = ownerType.GetProperty(memberName, flags);
            if (p != null) return p.PropertyType;
            return null;
        }

// Get value by a dotted path (e.g. "someState.isKnockedBack" or "stateField")
        // Safe get member value (field or property). Returns null if can't read.
        public static object GetMemberValue(object target, string memberName)
        {
            if (target == null) return null;
            var t = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            try
            {
                var f = t.GetField(memberName, flags);
                if (f != null)
                {
                    try
                    {
                        return f.GetValue(target);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"GetField '{memberName}' read failed: {e.Message}");
                        return null;
                    }
                }

                var p = t.GetProperty(memberName, flags);
                if (p != null && p.CanRead)
                {
                    try
                    {
                        // Some Unity properties (eg. 'rigidbody') throw NotSupportedException -> catch and ignore
                        return p.GetValue(target);
                    }
                    catch (NotSupportedException)
                    {
                        /* skip deprecated Unity shortcut property */
                        return null;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Debug.LogWarning($"Property '{memberName}' getter threw: {tie.InnerException?.Message ?? tie.Message}");
                        return null;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Property '{memberName}' read failed: {e.Message}");
                        return null;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

// Try reading value by dotted path (a.b.c). Safe against property getter exceptions.
        public static object GetMemberValueByPath(object root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('.');
            object cur = root;
            Type curType = root.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            for (int i = 0; i < parts.Length; i++)
            {
                var name = parts[i];
                if (cur == null) return null;

                // Try field first
                var f = curType.GetField(name, flags);
                if (f != null)
                {
                    try
                    {
                        cur = f.GetValue(cur);
                        curType = cur?.GetType() ?? f.FieldType;
                        continue;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"GetMemberValueByPath: reading field '{name}' failed: {e.Message}");
                        return null;
                    }
                }

                // Then try property, with protection for Unity deprecated ones
                var p = curType.GetProperty(name, flags);
                if (p != null && p.CanRead)
                {
                    try
                    {
                        cur = p.GetValue(cur);
                        curType = cur?.GetType() ?? p.PropertyType;
                        continue;
                    }
                    catch (NotSupportedException)
                    {
                        // Unity deprecated shortcut (like 'rigidbody') - skip
                        return null;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Debug.LogWarning($"GetMemberValueByPath: property '{name}' getter threw: {tie.InnerException?.Message ?? tie.Message}");
                        return null;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"GetMemberValueByPath: property '{name}' read failed: {e.Message}");
                        return null;
                    }
                }

                // not found
                return null;
            }

            return cur;
        }

// Returns owner object for a path and the MemberInfo (field or property) for last segment (safe)
        private static object GetMemberOwnerAndField(object root, string path, out MemberInfo member)
        {
            member = null;
            if (root == null || string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('.');
            object cur = root;
            Type curType = root.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var name = parts[i];
                if (cur == null) return null;

                var f = curType.GetField(name, flags);
                if (f != null)
                {
                    try
                    {
                        cur = f.GetValue(cur);
                        curType = cur?.GetType() ?? f.FieldType;
                        continue;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"GetMemberOwnerAndField: reading field '{name}' failed: {e.Message}");
                        return null;
                    }
                }

                var p = curType.GetProperty(name, flags);
                if (p != null && p.CanRead)
                {
                    try
                    {
                        cur = p.GetValue(cur);
                        curType = cur?.GetType() ?? p.PropertyType;
                        continue;
                    }
                    catch (NotSupportedException)
                    {
                        return null;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Debug.LogWarning($"GetMemberOwnerAndField: property '{name}' getter threw: {tie.InnerException?.Message ?? tie.Message}");
                        return null;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"GetMemberOwnerAndField: property '{name}' read failed: {e.Message}");
                        return null;
                    }
                }

                return null;
            }

            var last = parts.Last();
            var lf = curType.GetField(last, flags);
            if (lf != null)
            {
                member = lf;
                return cur;
            }

            var lp = curType.GetProperty(last, flags);
            if (lp != null)
            {
                member = lp;
                return cur;
            }

            return null;
        }

// Safe set by dotted path (uses GetMemberOwnerAndField); will log and return false on failure
        public static bool SetMemberValueByPath(object root, string path, object newValue)
        {
            if (root == null || string.IsNullOrEmpty(path)) return false;
            try
            {
                var parts = path.Split('.');
                if (parts.Length == 1)
                {
                    // direct on root
                    return SetMemberValue(root, path, newValue);
                }

                // find owner and member info
                var parentPath = string.Join(".", parts.Take(parts.Length - 1));
                var owner = GetMemberOwnerAndField(root, parentPath, out MemberInfo parentMember);
                if (owner == null || parentMember == null)
                {
                    Debug.LogWarning($"SetMemberValueByPath: cannot find owner for path '{path}'");
                    return false;
                }

                var lastName = parts.Last();
                var ownerType = owner.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                // set field on owner (the actual field we want)
                var lastField = ownerType.GetField(lastName, flags);
                if (lastField != null)
                {
                    try
                    {
                        var converted = ConvertIfNeeded(newValue, lastField.FieldType);
                        lastField.SetValue(owner, converted);

                        // if owner is value type, write back to its parent (handled via GetMemberOwnerAndField for parent)
                        return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"SetMemberValueByPath: setting field '{lastName}' failed: {e.Message}");
                        return false;
                    }
                }

                var lastProp = ownerType.GetProperty(lastName, flags);
                if (lastProp != null && lastProp.CanWrite)
                {
                    try
                    {
                        var converted = ConvertIfNeeded(newValue, lastProp.PropertyType);
                        lastProp.SetValue(owner, converted);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"SetMemberValueByPath: setting property '{lastName}' failed: {e.Message}");
                        return false;
                    }
                }

                Debug.LogWarning($"SetMemberValueByPath: member '{lastName}' not found on owner type {ownerType.Name}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SetMemberValueByPath exception: {e.Message}");
            }

            return false;
        }
    }
}
#endif