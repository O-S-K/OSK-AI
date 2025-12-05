// Assets/Scripts/FSMMono.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OSK.AIFSM;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract class FSMMono : MonoBehaviour
{
    [FoldoutGroup("At Transitions"), ListDrawerSettings(Expanded = true)]
    [SerializeField] protected List<TransitionData> transitions = new();

    [FoldoutGroup("Any Transitions"), ListDrawerSettings(Expanded = false)]
    [SerializeField] protected List<TransitionData> anyTransitions = new();

    [FoldoutGroup("Exit Transitions"), ListDrawerSettings(Expanded = false)]
    [SerializeField] protected List<TransitionData> exitTransitions = new();

    [LabelText("Start State (field)")]
    public bool IsStartState = true;

    [ValueDropdown("@GetStateFieldNames()")]
    [ShowIf(nameof(IsStartState))]
    [SerializeField] protected string startStateField;

    [SerializeField] protected bool debugLogs = true;

    protected FinalStateMachine fsm;

    protected virtual void Start() => BuildFSM();

    protected virtual void Update()
    {
        fsm?.Tick();
    }

    protected abstract void CreateStates();
    protected virtual void OnFSMBuilt() { }
    
    [FoldoutGroup("At Transitions")]
    [Button("Sort by From State", ButtonSizes.Medium), GUIColor(0.6f, 1f, 0.6f)]
    private void SortTransitions()
    {
        if (transitions == null) return;
        
        // Sắp xếp list dựa trên tên From Field
        transitions.Sort((a, b) => 
        {
            // Đưa null hoặc rỗng xuống dưới cùng
            if (string.IsNullOrEmpty(a.fromFieldName)) return 1;
            if (string.IsNullOrEmpty(b.fromFieldName)) return -1;
            
            // So sánh tên State
            int nameCompare = string.Compare(a.fromFieldName, b.fromFieldName, StringComparison.Ordinal);
            
            // Nếu cùng State, so sánh priority (Priority cao lên trước)
            if (nameCompare == 0)
                return b.priority.CompareTo(a.priority); 
                
            return nameCompare;
        });
        
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    
    // --- THÊM NÚT NÀY VÀO DƯỚI NÚT SORT ---
    [FoldoutGroup("At Transitions")]
    [Button("Add New Transition", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    private void AddNewTransition()
    {
        if (transitions == null) transitions = new List<TransitionData>();
        
        // Tạo transition mới
        var newTrans = new TransitionData();
        
        // Tự động điền FromState giống cái cuối cùng (tiện lợi)
        if (transitions.Count > 0)
        {
            var last = transitions[^1];
            newTrans.fromFieldName = last.fromFieldName; 
        }
        
        transitions.Add(newTrans);
        
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Editor call to validate all transitions and fill ResolvedFromType/ResolvedToType.
    /// Returns tuple (errors, warnings).
    /// </summary>
    public (int errors, int warnings) EditorValidateAll()
    {
        int errors = 0, warnings = 0;

        // ensure states instantiated so Resolve works
        CreateStates();

        // set targetObject for lists
        foreach (var t in transitions) if (t != null) t.targetObject = this;
        foreach (var t in anyTransitions) if (t != null) t.targetObject = this;
        foreach (var t in exitTransitions) if (t != null) t.targetObject = this;

        // helper to resolve and mark preview strings
        IState ResolveState(TransitionData td, bool wantFrom)
        {
            var explicitState = wantFrom ? td.from : td.to;
            if (explicitState != null)
            {
                if (wantFrom) td.ResolvedFromType = explicitState.GetType().Name;
                else td.ResolvedToType = explicitState.GetType().Name;
                return explicitState;
            }

            var name = wantFrom ? td.fromFieldName : td.toFieldName;
            if (!string.IsNullOrEmpty(name))
            {
                var fi = GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (fi != null && typeof(IState).IsAssignableFrom(fi.FieldType))
                {
                    var val = fi.GetValue(this) as IState;
                    if (wantFrom) td.ResolvedFromType = val?.GetType().Name ?? "<null>";
                    else td.ResolvedToType = val?.GetType().Name ?? "<null>";
                    return val;
                }

                // field name provided but not found -> error
                if (wantFrom) td.ResolvedFromType = "<invalid>";
                else td.ResolvedToType = "<invalid>";
                errors++;
                return null;
            }

            // empty: not an error per se
            if (wantFrom) td.ResolvedFromType = "<none>";
            else td.ResolvedToType = "<none>";
            warnings++;
            return null;
        }

        // Validate method + cache delegate for one TransitionData
        void ValidateAndCache(TransitionData td)
        {
            td.cachedCondition = null;

            if (td == null) return;

            // resolve states (for preview only)
            ResolveState(td, true);
            ResolveState(td, false);

            // condition validation
            if (string.IsNullOrEmpty(td.conditionMethod))
            {
                warnings++;
                return;
            }

            var methodName = td.conditionMethod.Replace(" (param)", "").Trim();
            var type = td.targetObject?.GetType();
            if (type == null)
            {
                errors++;
                return;
            }

            var mi = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
            {
                errors++;
                if (debugLogs) Debug.LogWarning($"[FSMMono.Validate] condition method '{methodName}' not found on {type.Name}");
                return;
            }

            var pars = mi.GetParameters();
            if (mi.ReturnType != typeof(bool) || pars.Length > 1)
            {
                errors++;
                if (debugLogs) Debug.LogWarning($"[FSMMono.Validate] condition '{methodName}' has invalid signature");
                return;
            }

            // try to build cached delegate now (0-param or 1-param)
            try
            {
                if (pars.Length == 0)
                {
                    var d = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), this, mi);
                    td.cachedCondition = td.invertCondition ? () => !d() : d;
                }
                else
                {
                    var pType = pars[0].ParameterType;
                    object parsed = null;
                    var s = td.conditionParam ?? string.Empty;
                    try
                    {
                        if (pType == typeof(string)) parsed = s;
                        else if (pType == typeof(int)) parsed = int.Parse(s);
                        else if (pType == typeof(float)) parsed = float.Parse(s);
                        else if (pType == typeof(bool)) parsed = bool.Parse(s);
                        else parsed = Convert.ChangeType(s, pType);
                    }
                    catch (Exception e)
                    {
                        errors++;
                        if (debugLogs) Debug.LogWarning($"[FSMMono.Validate] Failed to parse param for '{methodName}': {e.Message}");
                        return;
                    }

                    // create expression to call: () => (bool)this.Method((T)parsed)
                    var instanceExpr = System.Linq.Expressions.Expression.Constant(this);
                    var paramExpr = System.Linq.Expressions.Expression.Constant(parsed, pType);
                    var callExpr = System.Linq.Expressions.Expression.Call(instanceExpr, mi, paramExpr);
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<bool>>(callExpr);
                    var compiled = lambda.Compile();
                    td.cachedCondition = td.invertCondition ? () => !compiled() : compiled;
                }
            }
            catch (Exception e)
            {
                errors++;
                if (debugLogs) Debug.LogWarning($"[FSMMono.Validate] Failed to create delegate for '{methodName}': {e.Message}");
                td.cachedCondition = null;
            }
        }

        foreach (var t in transitions) ValidateAndCache(t);
        foreach (var t in anyTransitions) ValidateAndCache(t);
        foreach (var t in exitTransitions) ValidateAndCache(t);

        return (errors, warnings);
    }

    // Build FSM using cachedCondition where possible (fallback to dynamic MakeCond if needed)
    private void BuildFSM()
    {
        // 1) create states
        CreateStates();

        // set targetObject + hide flags
        foreach (var t in transitions) if (t != null) { t.targetObject = this; t.hideFromField = false; t.hideToField = false; }
        foreach (var t in anyTransitions) if (t != null) { t.targetObject = this; t.hideFromField = true; t.hideToField = false; }
        foreach (var t in exitTransitions) if (t != null) { t.targetObject = this; t.hideFromField = false; t.hideToField = true; }

        // validate & cache delegates
        EditorValidateAll_Internal(); // internal non-Editor version (we reuse code below)
        
        var builder = new FSMBuilder().AddAll(this);

        // helper resolve state (same as earlier)
        IState ResolveState(TransitionData td, bool wantFrom)
        {
            var explicitState = wantFrom ? td.from : td.to;
            if (explicitState != null)
            {
                if (wantFrom) td.ResolvedFromType = explicitState.GetType().Name;
                else td.ResolvedToType = explicitState.GetType().Name;
                return explicitState;
            }

            var name = wantFrom ? td.fromFieldName : td.toFieldName;
            if (!string.IsNullOrEmpty(name))
            {
                var fi = GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (fi != null && typeof(IState).IsAssignableFrom(fi.FieldType))
                {
                    var val = fi.GetValue(this) as IState;
                    if (wantFrom) td.ResolvedFromType = val?.GetType().Name ?? "<null>";
                    else td.ResolvedToType = val?.GetType().Name ?? "<null>";
                    return val;
                }
            }

            if (wantFrom) td.ResolvedFromType = "<null>";
            else td.ResolvedToType = "<null>";
            return null;
        }

        // register transitions (use cachedCondition if present)
        foreach (var td in transitions)
        {
            if (td == null) continue;
            var from = ResolveState(td, true);
            var to = ResolveState(td, false);
            if (from == null || to == null)
            {
                if (debugLogs) Debug.LogWarning($"[FSMMono] skipping At: from or to null (fromField='{td.fromFieldName}' toField='{td.toFieldName}')");
                continue;
            }
            var cond = td.cachedCondition ?? MakeCondFallback(td); // fallback uses reflection per-call
            builder.At(from, to, cond, priority: td.priority);
            if (debugLogs) Debug.Log($"[FSMMono] At {from.GetType().Name} -> {to.GetType().Name} ({td.conditionMethod}) pr={td.priority}");
        }

        foreach (var td in anyTransitions)
        {
            if (td == null) continue;
            var to = ResolveState(td, false);
            if (to == null)
            {
                if (debugLogs) Debug.LogWarning($"[FSMMono] skipping Any: to null (toField='{td.toFieldName}')");
                continue;
            }
            var cond = td.cachedCondition ?? MakeCondFallback(td);
            builder.Any(to, cond, priority: td.priority);
            if (debugLogs) Debug.Log($"[FSMMono] Any -> {to.GetType().Name} ({td.conditionMethod}) pr={td.priority}");
        }

        foreach (var td in exitTransitions)
        {
            if (td == null) continue;
            var from = ResolveState(td, true);
            if (from == null)
            {
                if (debugLogs) Debug.LogWarning($"[FSMMono] skipping Exit: from null (fromField='{td.fromFieldName}')");
                continue;
            }
            var cond = td.cachedCondition ?? MakeCondFallback(td);
            builder.Exit(from, cond, priority: td.priority);
            if (debugLogs) Debug.Log($"[FSMMono] Exit {from.GetType().Name} ({td.conditionMethod}) pr={td.priority}");
        }

        if (IsStartState)
        {
            var startState = ResolveStart();
            builder.Init(startState);
        }

        fsm = builder.Build();
        if (debugLogs) Debug.Log($"[FSMMono] FSM built for {name}. Current = {fsm?.GetCurrentState()?.GetType().Name ?? "null"}");

        OnFSMBuilt();
    }

    // small internal Validate used by BuildFSM to cache delegates (non-editor public entry)
    private void EditorValidateAll_Internal()
    {
        // same logic as EditorValidateAll but without returning tuple; reuse code to set cachedCondition fields
        // (we call CreateStates already above)
        foreach (var t in transitions) if (t != null) t.targetObject = this;
        foreach (var t in anyTransitions) if (t != null) t.targetObject = this;
        foreach (var t in exitTransitions) if (t != null) t.targetObject = this;

        Func<TransitionData, Func<bool>> buildCached = (td) =>
        {
            if (td == null || string.IsNullOrEmpty(td.conditionMethod)) return null;
            var methodName = td.conditionMethod.Replace(" (param)", "").Trim();
            var type = td.targetObject?.GetType();
            if (type == null) return null;
            var mi = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return null;
            var pars = mi.GetParameters();
            if (mi.ReturnType != typeof(bool) || pars.Length > 1) return null;
            try
            {
                if (pars.Length == 0)
                {
                    var d = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), this, mi);
                    return td.invertCondition ? (Func<bool>)(() => !d()) : d;
                }
                else
                {
                    var pType = pars[0].ParameterType;
                    object parsed = null;
                    var s = td.conditionParam ?? string.Empty;
                    if (pType == typeof(string)) parsed = s;
                    else if (pType == typeof(int)) parsed = int.Parse(s);
                    else if (pType == typeof(float)) parsed = float.Parse(s);
                    else if (pType == typeof(bool)) parsed = bool.Parse(s);
                    else parsed = Convert.ChangeType(s, pType);
                    var instanceExpr = System.Linq.Expressions.Expression.Constant(this);
                    var paramExpr = System.Linq.Expressions.Expression.Constant(parsed, pType);
                    var callExpr = System.Linq.Expressions.Expression.Call(instanceExpr, mi, paramExpr);
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<bool>>(callExpr);
                    var compiled = lambda.Compile();
                    return td.invertCondition ? (Func<bool>)(() => !compiled()) : compiled;
                }
            }
            catch
            {
                return null;
            }
        };

        foreach (var td in transitions) if (td != null) td.cachedCondition = buildCached(td);
        foreach (var td in anyTransitions) if (td != null) td.cachedCondition = buildCached(td);
        foreach (var td in exitTransitions) if (td != null) td.cachedCondition = buildCached(td);
    }

    // Fallback MakeCond (safe but slower) used only if we couldn't cache delegate
    private Func<bool> MakeCondFallback(TransitionData td)
    {
        var raw = td.conditionMethod;
        if (string.IsNullOrEmpty(raw)) return () => false;
        var methodName = raw.Replace(" (param)", "").Trim();
        var target = td.targetObject ?? this;
        var mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi == null) return () => false;
        var pars = mi.GetParameters();
        if (pars.Length == 0)
        {
            var d = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), target, mi);
            return td.invertCondition ? () => !d() : d;
        }
        else if (pars.Length == 1)
        {
            var pType = pars[0].ParameterType;
            return () =>
            {
                object parsed = null;
                try
                {
                    var s = td.conditionParam ?? string.Empty;
                    if (pType == typeof(string)) parsed = s;
                    else if (pType == typeof(int)) parsed = int.Parse(s);
                    else if (pType == typeof(float)) parsed = float.Parse(s);
                    else if (pType == typeof(bool)) parsed = bool.Parse(s);
                    else parsed = Convert.ChangeType(s, pType);
                }
                catch { return false; }
                try
                {
                    var res = (bool)mi.Invoke(target, new object[] { parsed });
                    return td.invertCondition ? !res : res;
                }
                catch { return false; }
            };
        }
        return () => false;
    }

    IState ResolveStart()
    {
        if (!string.IsNullOrEmpty(startStateField))
        {
            var fi = GetType().GetField(startStateField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (fi != null && typeof(IState).IsAssignableFrom(fi.FieldType))
            {
                var val = fi.GetValue(this) as IState;
                if (val != null) return val;
            }
        }

        var any = GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(f => typeof(IState).IsAssignableFrom(f.FieldType));
        if (any != null) return any.GetValue(this) as IState;

        if (debugLogs) Debug.LogWarning("[FSMMono] No start state resolved.");
        return null;
    }

    // Odin helper
    protected IEnumerable<string> GetStateFieldNames()
    {
        return GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
            .Where(f => typeof(IState).IsAssignableFrom(f.FieldType))
            .Select(f => f.Name)
            .OrderBy(n => n);
    }
}
