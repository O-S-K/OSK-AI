using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;

namespace OSK.AIFSM
{
    /// <summary>
    /// FSM TEMPLATE 1: Add(S…) → Any(A,enter) → Exit(exit) → At(A,B,cond) → Init(A) → Build()
    /// FSM TEMPLATE 2:
    /// ┌ builder = new FSMBuilder()
    /// ├ .Add(S…)                  // register states
    /// ├ .Any(A, ()=> Enter)        // any state → A
    /// ├ .Exit(()=> Interrupt)      // exit current state
    /// ├ .At(A, B, ()=>ExitCond)   // transition A → B
    /// ├ .Init(A)                  // start at A
    /// └ _fsm = builder.Build();    // get FinalStateMachine and use it
    /// </summary>
    public class FinalStateMachine
    {
        private readonly Dictionary<IState, List<Transition>> _transitions = new Dictionary<IState, List<Transition>>();
        private readonly List<Transition> _anyTransitions = new List<Transition>();
        private readonly List<Transition> _exitTransitions = new List<Transition>(); // <- exit transitions
        private static readonly List<Transition> EmptyTransitions = new List<Transition>(0);
        private List<Transition> _currentTransitions = new List<Transition>();
        private IState _currentState;
        public bool DebugLogs { get; set; } = false;

        // Tick checks exit transitions first
        public void Tick()
        {
            // 1) check exit transitions for current state
            if (_currentState != null)
            {
                var exitForCurrent = _exitTransitions
                    .Where(t => t.From == _currentState)
                    .ToList();

                if (exitForCurrent.Count > 0)
                {
                    var evalExit = EvaluateTransitions(exitForCurrent).Where(t => t.Result == true).ToList();
                    if (evalExit.Count > 0)
                    {
                        // pick highest priority exit
                        var chosenExit = evalExit.OrderByDescending(t => t.Priority).First();
#if UNITY_EDITOR
                        if (DebugLogs)
                        {
                            Debug.Log($"[FSM] EXIT TRIGGERED: '{chosenExit.GetDescription()}' on '{chosenExit.FromName}' at t={Time.time}");
                        }
#endif
                        // force exit: call OnExit and clear current state (do not transition to any specific state)
                        _currentState?.OnExit();
                        _currentState = null;
                        _currentTransitions = EmptyTransitions;
                        chosenExit.MarkTriggered(Time.time);
                        return; // stop this tick; let external logic or subsequent ticks decide next state
                    }
                }
            }

            // 2) normal transitions (any first)
            var transition = GetTransition();
            if (transition != null)
            {
#if UNITY_EDITOR
                if (DebugLogs)
                {
                    Debug.Log($"[FSM] Transition TRIGGERED: '{transition.GetDescription()}' from '{transition.FromName}' -> '{transition.ToName}' (priority {transition.Priority}) at t={Time.time}");
                }
#endif
                Set(transition.To);
                transition.MarkTriggered(Time.time);
            }

            _currentState?.Tick();
        }

        public void FixedTick()
        {
            _currentState?.FixedTick();
        }

        public void Init(IState state) => Set(state);

        public IState GetCurrentState() => _currentState;
        public Color GetGizmoColor() => _currentState?.GizmoState() ?? Color.black;
        public string GetCurrentStateName() => _currentState?.GetType().Name ?? "No State";
        public List<IState> GetStates() => _transitions.Keys.ToList();

        public List<Transition> GetTransitionsForState(IState state)
        {
            if (state == null) return new List<Transition>();
            return _transitions.TryGetValue(state, out var list) ? new List<Transition>(list) : new List<Transition>();
        }

        public List<Transition> GetAnyTransitions() => new List<Transition>(_anyTransitions);
        public List<Transition> GetExitTransitions() => new List<Transition>(_exitTransitions);

        public void Remove(IState state)
        {
            if (state == null) return;
            _transitions.Remove(state);
            _anyTransitions.RemoveAll(t => t.To == state);
            _exitTransitions.RemoveAll(t => t.From == state);
            foreach (var kv in _transitions.Values)
            {
                kv.RemoveAll(t => t.To == state || t.From == state);
            }
        }

        public void RemoveAll()
        {
            _transitions.Clear();
            _anyTransitions.Clear();
            _exitTransitions.Clear();
            _currentTransitions.Clear();
            _currentState = null;
        }

        public void Add(IState[] states)
        {
            foreach (var state in states) Add(state);
        }

        public void Add(IState state)
        {
            if (state == null) return;
            if (_transitions.ContainsKey(state)) return;
            _transitions.Add(state, new List<Transition>());
        }

        public void At(IState from, IState to, Func<bool> predicate, string description = null, int priority = 0)
        {
            if (from == null || to == null || predicate == null) return;
            if (!_transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                _transitions[from] = list;
            }

            list.Add(new Transition(from, to, predicate, description, priority));
        }

        public void At(IState from, IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            if (from == null || to == null || expr == null) return;
            var compiled = expr.Compile();
            var description = expr.Body.ToString();
            if (!_transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                _transitions[from] = list;
            }

            list.Add(new Transition(from, to, compiled, description, priority, expr));
        }

        public void Any(IState state, Func<bool> predicate, string description = null, int priority = 0)
        {
            if (state == null || predicate == null) return;
            _anyTransitions.Add(new Transition(null, state, predicate, description, priority));
        }

        public void Any(IState state, Expression<Func<bool>> expr, int priority = 0)
        {
            if (state == null || expr == null) return;
            var compiled = expr.Compile();
            var description = expr.Body.ToString();
            _anyTransitions.Add(new Transition(null, state, compiled, description, priority, expr));
        }

        // ---------------- Exit registration ----------------
        public void Exit(IState from, Func<bool> predicate, string description = null, int priority = 0)
        {
            if (from == null || predicate == null) return;
            _exitTransitions.Add(new Transition(from, null, predicate, description, priority));
        }

        public void Exit(IState from, Expression<Func<bool>> expr, int priority = 0)
        {
            if (from == null || expr == null) return;
            var compiled = expr.Compile();
            var description = expr.Body.ToString();
            _exitTransitions.Add(new Transition(from, null, compiled, description, priority, expr));
        }

        // ----------------- private state mgmt ----------------
        private void Set(IState state)
        {
            _currentState?.OnExit();
            _currentState = state;

            if (_currentState != null)
            {
                _transitions.TryGetValue(_currentState, out _currentTransitions);
                _currentTransitions ??= EmptyTransitions;
                _currentState.OnEnter();
            }
            else
            {
                _currentTransitions = EmptyTransitions;
            }
        }

        public void Exit<T>() where T : IState
        {
            if (_currentState is T)
            {
                _currentState?.OnExit();
                _currentState = null;
                _currentTransitions = EmptyTransitions;
            }
            else
            {
                Debug.LogError($"[FSM] Invalid State: {typeof(T).Name}");
            }
        }

        public void Exit()
        {
            _currentState?.OnExit();
            _currentState = null;
            _currentTransitions = EmptyTransitions;
        }

        // ---------------- transition resolution ----------------
        private Transition GetTransition()
        {
            var anyTrue = EvaluateTransitions(_anyTransitions)
                .Where(t => t.Result == true && t.To != _currentState)
                .ToList();
            if (anyTrue.Count > 0)
            {
                var chosenAny = anyTrue.OrderByDescending(t => t.Priority).First();
                return chosenAny;
            }

            if (_currentTransitions == null || _currentTransitions.Count == 0) return null;

            var currTrue = EvaluateTransitions(_currentTransitions)
                .Where(t => t.Result == true)
                .ToList();

            if (currTrue.Count == 0) return null;

            var chosen = currTrue.OrderByDescending(t => t.Priority).First();
            return chosen;
        }

        private IEnumerable<Transition> EvaluateTransitions(IEnumerable<Transition> list)
        {
            foreach (var t in list)
            {
                bool ok = false;
                try
                {
                    ok = t.Condition();
                }
                catch (Exception e)
                {
                    if (DebugLogs)
                    {
#if UNITY_EDITOR
                        Debug.LogError($"[FSM] Transition condition threw exception: {e.Message}\nTransition: {t.GetDescription()}");
#endif
                    }

                    ok = false;
                }

                t.MarkEvaluated(Time.time, ok);

#if UNITY_EDITOR
                if (DebugLogs)
                {
                    Debug.Log($"[FSM] Eval '{t.GetDescription()}' (from '{t.FromName}' -> '{t.ToName}') => {ok} (priority {t.Priority}) at t={Time.time}");
                }
#endif

                yield return t;
            }
        }

        // TRANSITION NESTED TYPE -----------------------------------------------------

        /// <summary>
        /// Transition metadata class — public so Editor can read it.
        /// </summary>
        // --- Replace the old Transition nested class with this new one ---
        // Transition class unchanged (kept as before)
        public class Transition
        {
            public IState From { get; }
            public IState To { get; }
            public Func<bool> Condition { get; }
            public string Description { get; private set; }
            public Expression<Func<bool>> Expr { get; }
            public int Priority { get; }

            public float LastEvaluatedTime { get; private set; } = -1f;
            public bool? Result { get; private set; } = null;
            public float LastTriggeredTime { get; private set; } = -1f;

            public Transition(IState from, IState to, Func<bool> condition, string description = null, int priority = 0,
                Expression<Func<bool>> expr = null)
            {
                From = from;
                To = to;
                Condition = condition ?? throw new ArgumentNullException(nameof(condition));
                Expr = expr;
                Priority = priority;
                Description = !string.IsNullOrWhiteSpace(description)
                    ? description
                    : (expr != null ? SimplifyExpression(expr) : "Lambda");
            }

            public void MarkEvaluated(float time, bool result)
            {
                LastEvaluatedTime = time;
                Result = result;
            }

            public void MarkTriggered(float time)
            {
                LastTriggeredTime = time;
            }

            public string FromName => From?.GetType().Name ?? "Any";
            public string ToName => To?.GetType().Name ?? "Null";
            public string GetDescription() => Description ?? (Expr != null ? SimplifyExpression(Expr) : "Lambda");

            public override string ToString() => $"[{Priority}] {GetDescription()} ({FromName} -> {ToName})";

            // -------------------- Pretty/Runtime-aware expression description --------------------
            // Try to produce friendly string like "dist 0.32 < 5"
            private static string SimplifyExpression(Expression<Func<bool>> expr)
            {
                try
                {
                    var body = expr.Body;

                    // If binary comparison, try get left/right and operator
                    if (body is BinaryExpression be && IsComparisonOp(be.NodeType))
                    {
                        var op = NodeTypeToOp(be.NodeType);

                        // try evaluate left and right values (safe)
                        var leftStr = ExprPartToString(be.Left);
                        var rightStr = ExprPartToString(be.Right);

                        // if both sides produced something, show "leftVal op rightVal" or "leftName leftVal op rightName rightVal"
                        // prefer showing readable names when possible
                        return $"{leftStr} {op} {rightStr}";
                    }

                    // If method call like IsPlayerInRange(x) just show it compact
                    if (body is MethodCallExpression mce)
                    {
                        var name = mce.Method.Name;
                        var args = mce.Arguments.Select(a => ExprPartToString(a)).ToArray();
                        return $"{name}({string.Join(", ", args)})";
                    }

                    // fallback to body.ToString() but clean parameter prefixes (closure names)
                    return CleanExpressionString(body.ToString());
                }
                catch
                {
                    return "[expr]";
                }
            }

            private static bool IsComparisonOp(ExpressionType t)
            {
                return t == ExpressionType.GreaterThan
                       || t == ExpressionType.GreaterThanOrEqual
                       || t == ExpressionType.LessThan
                       || t == ExpressionType.LessThanOrEqual
                       || t == ExpressionType.Equal
                       || t == ExpressionType.NotEqual;
            }

            private static string NodeTypeToOp(ExpressionType t)
            {
                return t switch
                {
                    ExpressionType.GreaterThan => ">",
                    ExpressionType.GreaterThanOrEqual => ">=",
                    ExpressionType.LessThan => "<",
                    ExpressionType.LessThanOrEqual => "<=",
                    ExpressionType.Equal => "==",
                    ExpressionType.NotEqual => "!=",
                    _ => t.ToString()
                };
            }

            // Try to produce "name value" or just "value" or "name" for expression part
            private static string ExprPartToString(Expression part)
            {
                // If constant, show the constant
                if (part is ConstantExpression ce)
                {
                    return ConstantToShortString(ce.Value);
                }

                // If MemberAccess to a captured variable or field/property, try compile sub-expression to get runtime value
                try
                {
                    // Create lambda that returns object for the part (convert to object)
                    var objectExpr = Expression.Convert(part, typeof(object));
                    var lambda = Expression.Lambda<Func<object>>(objectExpr);
                    Func<object> getter = null;
                    try
                    {
                        getter = lambda.Compile();
                    }
                    catch
                    {
                        // compilation may fail for expressions referencing parameters; fallback
                    }

                    string name = ShortNameOfExpression(part);

                    if (getter != null)
                    {
                        object val = null;
                        try
                        {
                            val = getter();
                        }
                        catch
                        {
                            val = null;
                        }

                        if (val != null)
                        {
                            // format numbers with limited precision
                            return $"{name} {ConstantToShortString(val)}";
                        }
                    }

                    // fallback: return short name only
                    return name;
                }
                catch
                {
                    // final fallback: the raw ToString cleaned
                    return CleanExpressionString(part.ToString());
                }
            }

            private static string ShortNameOfExpression(Expression e)
            {
                // If MemberExpression like this.health or someVar, return member.Name
                if (e is MemberExpression me)
                {
                    return me.Member.Name;
                }

                if (e is MethodCallExpression mc)
                {
                    return mc.Method.Name;
                }

                // fallback: clean whole expression
                return CleanExpressionString(e.ToString());
            }

            private static string CleanExpressionString(string s)
            {
                if (string.IsNullOrEmpty(s)) return s;
                // remove closure prefixes like value(<>c__DisplayClass...).x => x
                // common pattern: Convert(value(Program+<>c__DisplayClass...).field)
                // We'll keep it simple: remove text before last '.' if it contains '<'
                var parts = s.Split('.');
                if (parts.Length > 1)
                {
                    var last = parts.Last();
                    if (last.Contains("<") || last.Contains(">") || last.Contains("DisplayClass")) // heuristics
                    {
                        // try return last token that looks like identifier
                        for (int i = parts.Length - 1; i >= 0; i--)
                        {
                            var p = parts[i];
                            if (!p.Contains("<") && !p.Contains(">") && !p.Contains("DisplayClass"))
                            {
                                return p;
                            }
                        }
                    }

                    return parts.Last();
                }

                return s;
            }

            private static string ConstantToShortString(object val)
            {
                if (val == null) return "null";
                if (val is float f) return f.ToString("F2");
                if (val is double d) return d.ToString("F2");
                if (val is int or long or short or byte) return val.ToString();
                if (val is bool b) return b ? "true" : "false";
                return val.ToString();
            }
        }
    }
}