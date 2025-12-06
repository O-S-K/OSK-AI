#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace OSK.AIFSM
{
    public class TransitionDataDrawer : OdinValueDrawer<TransitionData>
    {
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
            td.hideToField = false; // Luôn hiện cột To

            bool isExitList = path.Contains("exitTransitions");
            var compType = GetRealTargetType(td.targetObject);

            // 2. Grouping Header Logic
            bool isHeader = false;
            int myIndex = this.Property.Index;
            var list = this.Property.Parent.ValueEntry.WeakSmartValue as IList;

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

            // 3. DRAWING
            
            // --- A. HEADER SECTION ---
            if (isHeader)
            {
                GUILayout.Space(12);
                var stateName = string.IsNullOrEmpty(td.fromFieldName) ? "Any" : td.fromFieldName;
                var colorHash = Mathf.Abs(stateName.GetHashCode()) % 100;
                var headerColor = Color.HSVToRGB((colorHash / 100f) * 0.8f, 0.35f, 0.7f);
                if (td.hideFromField) headerColor = new Color(0.3f, 0.3f, 0.3f);
                headerColor.a = 0.6f;

                SirenixEditorGUI.BeginHorizontalToolbar(28); 
                {
                    var rect = GUIHelper.GetCurrentLayoutRect();
                    EditorGUI.DrawRect(rect, headerColor);
                    GUILayout.Space(5);
                    var iconName = td.hideFromField ? "NetworkView Icon" : "Folder Icon";
                    GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(20), GUILayout.Height(18));
                    
                    GUIHelper.PushColor(Color.white);
                    GUILayout.Label("FROM:", SirenixGUIStyles.BoldLabel, GUILayout.Width(45));
                    
                    if (!td.hideFromField)
                    {
                        var stateNames = GetStateFieldNamesForType(compType);
                        int fromIdx = Array.IndexOf(stateNames, td.fromFieldName);
                        var headerDropStyle = new GUIStyle(GUI.skin.button); 
                        headerDropStyle.normal.textColor = Color.white;
                        headerDropStyle.fontStyle = FontStyle.Bold;
                        headerDropStyle.alignment = TextAnchor.MiddleLeft;

                        int newIdx = EditorGUILayout.Popup(fromIdx, stateNames, headerDropStyle, GUILayout.Width(200));
                        if (newIdx != fromIdx && newIdx >= 0)
                        {
                            td.fromFieldName = stateNames[newIdx];
                            EditorUtility.SetDirty(td.targetObject as UnityEngine.Object); 
                        }
                    }
                    else
                    {
                        GUILayout.Label(isExitList ? "EXIT TRANSITIONS" : "ANY TRANSITIONS", SirenixGUIStyles.BoldLabel);
                    }
                    GUIHelper.PopColor();
                }
                SirenixEditorGUI.EndHorizontalToolbar();
            }

            // --- B. BODY SECTION ---
            SirenixEditorGUI.BeginHorizontalToolbar();
            {
                if (!td.hideFromField) GUILayout.Space(24); 

                // Icon logic
                GUILayout.Label(EditorGUIUtility.IconContent("cs Script Icon"), GUILayout.Width(20), GUILayout.Height(18));

                // Condition
                var condNames = GetMethodListForType(compType);
                int condIdx = Array.IndexOf(condNames, td.conditionMethod);
                if (condIdx < 0) GUIHelper.PushColor(new Color(1f, 0.7f, 0.7f));
                int newCondIdx = SirenixEditorFields.Dropdown(condIdx, condNames, GUILayout.Width(110)); 
                if (condIdx < 0) GUIHelper.PopColor();
                if (newCondIdx != condIdx) td.conditionMethod = (newCondIdx >= 0 && condNames.Length > 0) ? condNames[newCondIdx] : null;

                td.invertCondition = GUILayout.Toggle(td.invertCondition, "!", GUILayout.Width(18));
                if (td.conditionMethod != null && td.conditionMethod.EndsWith("(param)"))
                    td.conditionParam = EditorGUILayout.TextField(td.conditionParam, GUILayout.Width(50));
                

                GUILayout.Label("➜", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.Width(20));

                // Target State
                if (!td.hideToField)
                {
                    var stateNamesTo = GetStateFieldNamesForType(compType);
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
                
                
                // priority
                GUILayout.Label("P:", GUILayout.Width(12));
                int newPriority = EditorGUILayout.IntField(td.priority, GUILayout.Width(30));
                if (newPriority != td.priority) td.priority = newPriority;
                
                GUILayout.FlexibleSpace(); // Đẩy các nút về bên phải

                // [NEW] NÚT THÊM (+) MÀU XANH - AUTO COPY FROM STATE
                // Bấm nút này sẽ tạo dòng mới có FromState giống dòng hiện tại
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_CreateAddNew"), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20)))
                {
                    if (list != null)
                    {
                        var newItem = new TransitionData();
                        newItem.fromFieldName = td.fromFieldName; // <--- COPY FROM STATE Ở ĐÂY
                        
                        // Chèn ngay xuống dưới dòng hiện tại
                        list.Insert(myIndex + 1, newItem);
                        GUIHelper.RequestRepaint();
                    }
                }

                GUILayout.Space(2);

                // NÚT XÓA (X) - XÓA DÒNG HIỆN TẠI
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

            this.ValueEntry.SmartValue = td;
        }

        // --- Helpers ---
        private Type GetRealTargetType(object target)
        {
            if (target == null) return null;
            return target.GetType();
        }

        private static string[] GetMethodListForType(Type compType)
        {
            if (compType == null) return new string[0];
            if (_methodListCache.TryGetValue(compType, out var list)) return list;
            try {
                var methods = compType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.ReturnType == typeof(bool) && (m.GetParameters().Length == 0 || m.GetParameters().Length == 1))
                    .Select(m => m.Name + (m.GetParameters().Length == 1 ? " (param)" : "")).OrderBy(n => n).ToArray();
                _methodListCache[compType] = methods; return methods;
            } catch { return new string[0]; }
        }

        private static string[] GetStateFieldNamesForType(Type compType)
        {
            if (compType == null) return new string[0];
            if (_stateFieldNamesCache.TryGetValue(compType, out var arr)) return arr;
            try {
                var fields = compType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(f => typeof(IState).IsAssignableFrom(f.FieldType)).Select(f => f.Name).OrderBy(n => n).ToArray();
                _stateFieldNamesCache[compType] = fields; return fields;
            } catch { return new string[0]; }
        }
    }
}
#endif