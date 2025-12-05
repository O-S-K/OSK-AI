#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OSK.AIFSM;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace OSK.AIFSM.Editor
{
    public class TransitionDataDrawer : OdinValueDrawer<TransitionData>
    {
        // UI Constants
        const int ARROW_WIDTH = 25;

        // Caches
        private static readonly Dictionary<Type, string[]> _methodListCache = new();
        private static readonly Dictionary<Type, string[]> _stateFieldNamesCache = new();

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var td = this.ValueEntry.SmartValue;
            if (td == null) return;

            // 1. Setup Data
            if (td.targetObject == null) td.targetObject = this.Property.Tree.WeakTargets.FirstOrDefault();
            var path = this.Property.Path ?? string.Empty;
            td.hideFromField = path.Contains("anyTransitions");
            td.hideToField = path.Contains("exitTransitions");

            // 2. Grouping Logic
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

            // --- CHECK ACTIVE STATE (NEW LOGIC) ---
            bool isActiveState = false;
            if (Application.isPlaying && !td.hideFromField && !string.IsNullOrEmpty(td.fromFieldName))
            {
                isActiveState = IsStateActive(td.targetObject, td.fromFieldName);
            }

            // 3. DRAWING

            // --- A. HEADER SECTION ---
            if (isHeader)
            {
                GUILayout.Space(12);

                var stateName = string.IsNullOrEmpty(td.fromFieldName) ? "Any" : td.fromFieldName;

                // --- MÀU SẮC HEADER ---
                Color headerColor;
                if (isActiveState)
                {
                    // [ACTIVE] Màu Xanh Lá Đậm nổi bật
                    headerColor = new Color(0.2f, 0.7f, 0.3f, 0.9f);
                }
                else
                {
                    // [NORMAL] Màu Pastel nhẹ nhàng dựa trên hash tên
                    var colorHash = Mathf.Abs(stateName.GetHashCode()) % 100;
                    headerColor = Color.HSVToRGB((colorHash / 100f) * 0.8f, 0.35f, 0.7f);
                    if (td.hideFromField) headerColor = new Color(0.3f, 0.3f, 0.3f);
                    headerColor.a = 0.6f;
                }

                SirenixEditorGUI.BeginHorizontalToolbar(28);
                {
                    var rect = GUIHelper.GetCurrentLayoutRect();
                    EditorGUI.DrawRect(rect, headerColor);

                    // Nếu Active -> Vẽ thêm viền vàng cho nổi bần bật
                    if (isActiveState)
                    {
                        SirenixEditorGUI.DrawBorders(rect, 2, new Color(1f, 0.9f, 0.4f, 0.8f));
                    }

                    GUILayout.Space(5);

                    // Icon
                    var iconName = "Folder Icon";
                    if (td.hideFromField) iconName = "d_ViewToolOrbit On"; // Icon con mắt/Orbit nhìn rất hợp monitoring
                    else if (isActiveState) iconName = "d_PlayButton On";

                    GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(20), GUILayout.Height(18));

                    // Label FROM
                    GUIHelper.PushColor(Color.white);

                    string labelStr = "FROM:";
                    if (td.hideFromField) labelStr = "GLOBAL:"; // Đổi tên cho nguy hiểm
                    else if (isActiveState) labelStr = "RUNNING:";

                    GUILayout.Label(labelStr, SirenixGUIStyles.BoldLabel, GUILayout.Width(isActiveState || td.hideFromField ? 70 : 45));

                    if (!td.hideFromField)
                    {
                        var stateNames = GetStateFieldNamesForType(td.targetObject?.GetType());
                        int fromIdx = Array.IndexOf(stateNames, td.fromFieldName);

                        var headerDropStyle = new GUIStyle(GUI.skin.button);
                        headerDropStyle.normal.textColor = Color.white;
                        headerDropStyle.fontStyle = FontStyle.Bold;
                        headerDropStyle.alignment = TextAnchor.MiddleLeft;

                        // Dropdown
                        int newIdx = EditorGUILayout.Popup(fromIdx, stateNames, headerDropStyle, GUILayout.Width(200));
                        if (newIdx != fromIdx && newIdx >= 0)
                        {
                            td.fromFieldName = stateNames[newIdx];
                            EditorUtility.SetDirty(td.targetObject as UnityEngine.Object);
                        }

                        // Nếu Active -> Hiện thêm nút Pause/Stop giả lập (hoặc chỉ để trang trí)
                        if (isActiveState)
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(EditorGUIUtility.IconContent("d_WaitSpin05"), GUILayout.Width(20)); // Icon xoay xoay
                        }
                        // Nếu không Active -> Hiện nút Play để ép chuyển sang
                        else if (Application.isPlaying && !string.IsNullOrEmpty(td.fromFieldName))
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"), GUILayout.Width(24), GUILayout.Height(20)))
                            {
                                ForcePlayState(td);
                            }
                        }
                    }
                    else
                    {
                        GUILayout.Label(td.hideToField ? "EXIT TRANSITIONS" : "ANY TRANSITIONS", SirenixGUIStyles.BoldLabel);
                    }

                    GUIHelper.PopColor();
                }
                SirenixEditorGUI.EndHorizontalToolbar();
            }

            // --- B. BODY SECTION (Nếu Active thì tô nền nhẹ cho cả body để dễ nhìn) ---
            if (isActiveState) GUIHelper.PushColor(new Color(0.8f, 1f, 0.8f, 1f)); // Tint xanh nhẹ

            SirenixEditorGUI.BeginHorizontalToolbar();
            {
                if (!td.hideFromField) GUILayout.Space(24);

                GUILayout.Label(EditorGUIUtility.IconContent("cs Script Icon"), GUILayout.Width(20), GUILayout.Height(18));

                var condNames = GetMethodListForType(td.targetObject?.GetType());
                int condIdx = Array.IndexOf(condNames, td.conditionMethod);

                if (condIdx < 0) GUIHelper.PushColor(new Color(1f, 0.7f, 0.7f));
                int newCondIdx = SirenixEditorFields.Dropdown(condIdx, condNames, GUILayout.Width(200));
                if (condIdx < 0) GUIHelper.PopColor();

                if (newCondIdx != condIdx) td.conditionMethod = (newCondIdx >= 0 && condNames.Length > 0) ? condNames[newCondIdx] : null;

                td.invertCondition = GUILayout.Toggle(td.invertCondition, "!", GUILayout.Width(18));
                if (td.conditionMethod != null && td.conditionMethod.EndsWith("(param)"))
                    td.conditionParam = EditorGUILayout.TextField(td.conditionParam, GUILayout.Width(50));

                GUILayout.Label("➜", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.Width(20));

                if (!td.hideToField)
                {
                    var stateNamesTo = GetStateFieldNamesForType(td.targetObject?.GetType());
                    int toIdx = Array.IndexOf(stateNamesTo, td.toFieldName);

                    var chipColor = toIdx >= 0 ? new Color(0.8f, 0.9f, 1f) : new Color(1f, 0.6f, 0.6f);
                    GUIHelper.PushColor(chipColor);
                    int newToIdx = SirenixEditorFields.Dropdown(toIdx, stateNamesTo, GUILayout.MinWidth(100));
                    GUIHelper.PopColor();
                    if (newToIdx != toIdx) td.toFieldName = (newToIdx >= 0 && stateNamesTo.Length > 0) ? stateNamesTo[newToIdx] : null;

                    if (Application.isPlaying && !string.IsNullOrEmpty(td.toFieldName))
                    {
                        if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"), GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            ForcePlayState(td);
                        }
                    }
                }
                else
                {
                    GUILayout.Label("(Exit)", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.Width(80));
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUIStyle.none, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    td.fromFieldName = null;
                    td.toFieldName = null;
                    td.conditionMethod = null;
                }
            }
            SirenixEditorGUI.EndHorizontalToolbar();

            if (isActiveState) GUIHelper.PopColor(); // Pop Active Tint

            this.ValueEntry.SmartValue = td;
        }

      private static void ForcePlayState(TransitionData td)
        {
            if (td == null || td.targetObject == null) return;

            var compType = td.targetObject.GetType();
            
            // LOGIC XÁC ĐỊNH STATE CẦN CHẠY:
            // 1. Nếu là Any State (hideFromField = true) -> Chạy State Đích (To)
            // 2. Các trường hợp còn lại -> Chạy State Nguồn (From)
            bool useToState = td.hideFromField;

            // Lấy tên field và instance tương ứng
            string targetFieldName = useToState ? td.toFieldName : td.fromFieldName;
            IState targetState = useToState ? td.to : td.from;

            // Nếu instance chưa có (null), dùng Reflection tìm lại cho chắc
            if (targetState == null && !string.IsNullOrEmpty(targetFieldName))
            {
                var fField = compType.GetField(targetFieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (fField != null)
                    targetState = fField.GetValue(td.targetObject) as IState;
            }

            if (targetState != null)
            {
                // --- QUAN TRỌNG: GỌI QUA FSM ĐỂ ĐẢM BẢO LOGIC (EXIT CŨ -> ENTER MỚI) ---
                // Thay vì gọi trực tiếp targetState.OnEnter(), ta ép FSM chuyển state
                // Điều này giúp state cũ chạy hàm OnExit() đàng hoàng, tránh lỗi logic game.
                
                var fsmField = compType.GetField("fsm", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (fsmField != null)
                {
                    var fsmInstance = fsmField.GetValue(td.targetObject);
                    if (fsmInstance != null)
                    {
                        // Tìm hàm chuyển state (ChangeState hoặc StateTransition)
                        var method = fsmInstance.GetType().GetMethod("StateTransition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) 
                                     ?? fsmInstance.GetType().GetMethod("ChangeState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (method != null)
                        {
                            method.Invoke(fsmInstance, new object[] { targetState });
                            Debug.Log($"<color=green>[FSM Editor]</color> Forced Transition to <b>{targetFieldName}</b>");
                            return; // Xong
                        }
                    }
                }

                // Fallback: Nếu không tìm thấy FSM thì mới gọi OnEnter thủ công (như code cũ của bạn)
                Debug.LogWarning($"[FSM Editor] Warning: Forcing OnEnter directly on '{targetFieldName}' (FSM logic bypassed)");
                targetState.OnEnter();
            }
            else
            {
                Debug.LogWarning($"[FSM Editor] Cannot find state instance for field: {targetFieldName}");
            }
        }

        // --- HELPER CHECK ACTIVE STATE ---
        private bool IsStateActive(object targetMono, string stateFieldName)
        {
            if (targetMono == null || string.IsNullOrEmpty(stateFieldName)) return false;

            // 1. Lấy FSM từ Mono
            var monoType = targetMono.GetType();
            var fsmField = monoType.GetField("fsm", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (fsmField == null) return false;

            var fsmInstance = fsmField.GetValue(targetMono);
            if (fsmInstance == null) return false;

            // 2. Lấy CurrentState từ FSM (Giả sử hàm GetCurrentState())
            var getCurrentStateMethod = fsmInstance.GetType().GetMethod("GetCurrentState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCurrentStateMethod == null) return false;

            var currentState = getCurrentStateMethod.Invoke(fsmInstance, null);
            if (currentState == null) return false;

            // 3. Lấy State Instance từ Field Name (của dòng này)
            var stateField = monoType.GetField(stateFieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (stateField == null) return false;

            var thisStateInstance = stateField.GetValue(targetMono);

            // 4. So sánh Reference
            return ReferenceEquals(currentState, thisStateInstance);
        }


        // Cache Helpers (Giữ nguyên)
        private static string[] GetMethodListForType(Type compType)
        {
            if (compType == null) return new string[0];
            if (_methodListCache.TryGetValue(compType, out var list)) return list;
            try
            {
                var methods = compType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.ReturnType == typeof(bool) && (m.GetParameters().Length == 0 || m.GetParameters().Length == 1))
                    .Select(m => m.Name + (m.GetParameters().Length == 1 ? " (param)" : "")).OrderBy(n => n).ToArray();
                _methodListCache[compType] = methods;
                return methods;
            }
            catch
            {
                _methodListCache[compType] = new string[0];
                return new string[0];
            }
        }

        private static string[] GetStateFieldNamesForType(Type compType)
        {
            if (compType == null) return new string[0];
            if (_stateFieldNamesCache.TryGetValue(compType, out var arr)) return arr;
            try
            {
                var fields = compType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(f => typeof(IState).IsAssignableFrom(f.FieldType)).Select(f => f.Name).OrderBy(n => n).ToArray();
                _stateFieldNamesCache[compType] = fields;
                return fields;
            }
            catch
            {
                _stateFieldNamesCache[compType] = new string[0];
                return new string[0];
            }
        }
    }
}
#endif