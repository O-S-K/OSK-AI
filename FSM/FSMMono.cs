using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using OSK.AIFSM;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract class FSMMono : MonoBehaviour
{
    // --- DỮ LIỆU TRỰC TIẾP TRÊN MONO ---
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
    public FinalStateMachine FSM => fsm;

    // --- LIFECYCLE ---
    protected virtual void Start() => BuildFSM();
    protected virtual void Update() => fsm?.Tick();
    protected virtual void FixedUpdate() => fsm?.FixedTick();

    protected abstract void CreateStates();
    protected virtual void OnFSMBuilt() { }
    
    protected virtual FSMBuilder CreateBuilder()
    {
        var builder = new FSMBuilder();
        builder.AddAll(this); // default behavior
        return builder;
    }
    
    protected virtual void RegisterEditorTransitions(FSMBuilder builder)
    {
        IState ResolveState(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var fi = GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            return (fi != null && typeof(IState).IsAssignableFrom(fi.FieldType))
                ? fi.GetValue(this) as IState
                : null;
        }

        foreach (var td in transitions)
        {
            if (td?.cachedExpression == null) continue;
            var from = ResolveState(td.fromFieldName);
            var to = ResolveState(td.toFieldName);
            if (from != null && to != null)
                builder.At(from, to, td.cachedExpression, td.priority);
        }

        foreach (var td in anyTransitions)
        {
            if (td?.cachedExpression == null) continue;
            var to = ResolveState(td.toFieldName);
            if (to != null)
                builder.Any(to, td.cachedExpression, td.priority);
        }

        foreach (var td in exitTransitions)
        {
            if (td?.cachedExpression == null) continue;
            var from = ResolveState(td.fromFieldName);
            var to = ResolveState(td.toFieldName);
            if (from != null)
                builder.Exit(from, to, td.cachedExpression, td.priority);
        }
    }

    protected virtual void OnBuildCustomFSM(FSMBuilder builder) {}
    protected virtual void FinalizeFSM(FSMBuilder builder)
    {
        if (IsStartState)
        {
            var start = GetType()
                .GetField(startStateField,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                ?.GetValue(this) as IState;

            if (start != null)
                builder.Init(start);
        }

        fsm = builder.Build();
        fsm.DebugLogs = debugLogs;
    }
    
    
    // --- EDITOR BUTTONS ---
    [FoldoutGroup("At Transitions")]
    [Button("New From", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    private void AddNewTransition()
    {
        if (transitions == null)  transitions = new List<TransitionData>();
        var newTrans = new TransitionData();
        transitions.Add(newTrans);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    [FoldoutGroup("At Transitions")]
    [Button("Sort by From State", ButtonSizes.Medium), GUIColor(0.6f, 1f, 0.6f)]
    private void SortTransitions()
    {
        if (transitions == null) return;
        transitions.Sort((a, b) => 
        {
            if (string.IsNullOrEmpty(a.fromFieldName)) return 1;
            if (string.IsNullOrEmpty(b.fromFieldName)) return -1;
            int c = string.Compare(a.fromFieldName, b.fromFieldName, StringComparison.Ordinal);
            return c == 0 ? b.priority.CompareTo(a.priority) : c;
        });
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    
    [FoldoutGroup("At Transitions")]
    [Button("Open FSM debug window", ButtonSizes.Medium), GUIColor(0.2f, .4f, 0.6f)]
    private void ShowWindow()
    {
#if UNITY_EDITOR
        FSMDebugWindow.ShowWindow(this);
#endif
    }
     

    // --- BUILD LOGIC ---
    private void BuildFSM()
    {
        CreateStates();

        foreach (var t in transitions) if (t != null) t.targetObject = this;
        foreach (var t in anyTransitions) if (t != null) t.targetObject = this;
        foreach (var t in exitTransitions) if (t != null) t.targetObject = this;

        GenerateAllExpressions();

        var builder = CreateBuilder();

        RegisterEditorTransitions(builder);   // editor-driven
        OnBuildCustomFSM(builder);             // ⭐ runtime custom
        FinalizeFSM(builder);

        OnFSMBuilt();
    }

    private void GenerateAllExpressions()
    {
        Expression<Func<bool>> Create(TransitionData td)
        {
            if (string.IsNullOrEmpty(td.conditionMethod)) return null;
            var mName = td.conditionMethod.Replace(" (param)", "").Trim();
            var mi = GetType().GetMethod(mName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return () => false;

            try
            {
                var instanceExpr = Expression.Constant(this);
                Expression callExpr;
                var pars = mi.GetParameters();
                if (pars.Length == 1)
                {
                    var pType = pars[0].ParameterType;
                    var val = ParseParam(td.conditionParam, pType);
                    callExpr = Expression.Call(instanceExpr, mi, Expression.Constant(val, pType));
                }
                else callExpr = Expression.Call(instanceExpr, mi);

                if (td.invertCondition) callExpr = Expression.Not(callExpr);
                return Expression.Lambda<Func<bool>>(callExpr);
            }
            catch { return () => false; }
        }

        foreach (var t in transitions) if (t != null) t.cachedExpression = Create(t);
        foreach (var t in anyTransitions) if (t != null) t.cachedExpression = Create(t);
        foreach (var t in exitTransitions) if (t != null) t.cachedExpression = Create(t);
    }

    private object ParseParam(string s, Type pType)
    {
        if (string.IsNullOrEmpty(s)) return pType.IsValueType ? Activator.CreateInstance(pType) : null;
        try {
            if (pType == typeof(string)) return s;
            if (pType == typeof(int)) return int.Parse(s);
            if (pType == typeof(float)) return float.Parse(s);
            if (pType == typeof(bool)) return bool.Parse(s);
            if (pType.IsEnum) return Enum.Parse(pType, s);
            return Convert.ChangeType(s, pType);
        } catch { return pType.IsValueType ? Activator.CreateInstance(pType) : null; }
    }
    
    protected IEnumerable<string> GetStateFieldNames()
    {
        return GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
            .Where(f => typeof(IState).IsAssignableFrom(f.FieldType))
            .Select(f => f.Name)
            .OrderBy(n => n);
    }
}