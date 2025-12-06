using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;

namespace OSK.AIFSM
{
    public class FinalStateMachine
    {
        private readonly Dictionary<IState, List<Transition>> _transitions = new Dictionary<IState, List<Transition>>();
        private readonly List<Transition> _anyTransitions = new List<Transition>();
        private readonly List<Transition> _exitTransitions = new List<Transition>();
        private static readonly List<Transition> EmptyTransitions = new List<Transition>(0);
        
        private List<Transition> _currentTransitions = new List<Transition>();
        private IState _currentState;
        private IState _startState;
        
        public bool DebugLogs { get; set; } = false;

        // -------------------------------------------------------------------------
        // CORE LOGIC
        // -------------------------------------------------------------------------

        public void Tick()
        {
            CheckStateExitTransitions();
            CheckStateNormalTransitions();
            _currentState?.Tick();
        }

        private void CheckStateNormalTransitions()
        {
            var transition = GetTransition();
            if (transition != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugLogs) Debug.Log($"[FSM] Transition: '{transition.GetDescription()}' ({transition.FromName} -> {transition.ToName})");
#endif
                Set(transition.To);
                transition.MarkTriggered(Time.time);
            }
        }

        public void FixedTick() => _currentState?.FixedTick();
        public void Init(IState state) 
        {
            _startState = state;
            Set(state);
        }

        private void CheckStateExitTransitions()
        {
            if (_currentState != null)
            {
                // Lọc các transition Exit thuộc về state hiện tại
                var exitForCurrent = _exitTransitions.Where(t => t.From == _currentState).ToList();

                if (exitForCurrent.Count > 0)
                {
                    var evalExit = EvaluateTransitions(exitForCurrent).Where(t => t.Result == true).ToList();
                    if (evalExit.Count > 0)
                    {
                        var chosenExit = evalExit.OrderByDescending(t => t.Priority).First();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (DebugLogs) Debug.Log($"[FSM] EXIT: '{chosenExit.GetDescription()}' on '{chosenExit.FromName}'");
#endif
                        chosenExit.MarkTriggered(Time.time);

                        if (chosenExit.To != null)
                        {
                            Set(chosenExit.To);
                        }
                        else
                        {
                            _currentState.OnExit();
                            _currentState = null;
                            _currentTransitions = EmptyTransitions;
                        }
                    }
                }
            }
        }

        // -------------------------------------------------------------------------
        // BUILDER API (Consolidated to use Expression only)
        // -------------------------------------------------------------------------

        public void Add(params IState[] states)
        {
            foreach (var state in states)
            {
                if (state == null || _transitions.ContainsKey(state)) continue;
                _transitions.Add(state, new List<Transition>());
            }
        }

        /// <summary>
        /// Normal Transition: From -> To
        /// </summary>
        public void At(IState from, IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            if (from == null || to == null || expr == null) return;
            
            // Compile 1 lần duy nhất lúc Init
            var compiled = expr.Compile();
            var t = new Transition(from, to, compiled, priority, expr);

            if (!_transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                _transitions[from] = list;
            }
            list.Add(t);
        }

        /// <summary>
        /// Global Transition: Any -> To
        /// </summary>
        public void Any(IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            if (to == null || expr == null) return;
            
            var compiled = expr.Compile();
            var t = new Transition(null, to, compiled, priority, expr);
            
            _anyTransitions.Add(t);
        }

        /// <summary>
        /// Exit Transition (Generic): Hỗ trợ cả (From -> To) và (From -> Null)
        /// </summary>
        public void Exit(IState from, IState to, Expression<Func<bool>> expr, int priority = 100)
        {
            if (from == null || expr == null) return;
            
            var compiled = expr.Compile();
            // Tạo transition với cờ IsExit = true (được set mặc định trong constructor logic hoặc ta set tay)
            var t = new Transition(from, to, compiled, priority, expr) 
            { 
                IsExit = true 
            };

            // Lưu vào list riêng _exitTransitions để Tick xử lý riêng
            _exitTransitions.Add(t);
        }

        // -------------------------------------------------------------------------
        // INTERNAL HELPERS
        // -------------------------------------------------------------------------

        private void Set(IState state)
        {
            _currentState?.OnExit();
            _currentState = state;

            if (_currentState != null)
            {
                // Cache transitions for current state optimization
                if (!_transitions.TryGetValue(_currentState, out _currentTransitions))
                    _currentTransitions = EmptyTransitions;
                
                _currentState.OnEnter();
            }
            else
            {
                _currentTransitions = EmptyTransitions;
            }
        }

        private bool SafeCheck(Transition t)
        {
            try { return t.Condition(); }
            catch { return false; }
        }

        private Transition GetTransition()
        {
            Transition bestAny = null;
            float now = Time.time;

            // 1. Any transitions
            for (int i = 0; i < _anyTransitions.Count; i++)
            {
                var t = _anyTransitions[i];
                bool ok = SafeCheck(t);

                t.MarkEvaluated(now, ok);

                if (!ok) continue;
                if (t.To == _currentState) continue; // tránh chuyển sang chính mình

                if (bestAny == null || t.Priority > bestAny.Priority)
                    bestAny = t;
            }

            if (bestAny != null)
                return bestAny;

            // 2. Transitions từ current state
            if (_currentTransitions == null || _currentTransitions.Count == 0)
                return null;

            Transition best = null;

            for (int i = 0; i < _currentTransitions.Count; i++)
            {
                var t = _currentTransitions[i];
                bool ok = SafeCheck(t);

                t.MarkEvaluated(now, ok);

                if (!ok) continue;

                if (best == null || t.Priority > best.Priority)
                    best = t;
            }

            return best;
        }


        private IEnumerable<Transition> EvaluateTransitions(List<Transition> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                bool ok = false;

                try { ok = t.Condition(); } 
                catch { ok = false; }

                t.MarkEvaluated(Time.time, ok);

                Debug.Log($"[FSM] Check: FROM={t.From.StateName} → TO={t.To.StateName} | Priority={t.Priority} | Result={ok}");

                yield return t;
            }
        }

        // GETTERS
        public List<IState> GetStates() => _transitions.Keys.ToList();
        public IState GetCurrentState() => _currentState;
        public List<Transition> GetTransitionsForState(IState state) => _transitions.TryGetValue(state, out var list) ? list : new List<Transition>();
        public List<Transition> GetAnyTransitions() => _anyTransitions;
        public List<Transition> GetExitTransitions() => _exitTransitions;
 
        public class Transition
        {
            public IState From { get; }
            public IState To { get; }
            public Func<bool> Condition { get; }
            public int Priority { get; }
            public bool IsExit { get; set; } = false;

            // Runtime Status
            public float LastEvaluatedTime { get; private set; } = -1f;
            public bool? Result { get; private set; } = null;
            public float LastTriggeredTime { get; private set; } = -1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug / Editor Only
            public string Description { get; private set; }
            public Expression<Func<bool>> Expr { get; }
#endif

            public Transition(IState from, IState to, Func<bool> condition, int priority, Expression<Func<bool>> expr)
            {
                From = from;
                To = to;
                Condition = condition;
                Priority = priority;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Expr = expr;
                if (expr != null) Description = SimplifyExpression(expr);
                else Description = "Lambda";
#endif
            }

            public void MarkEvaluated(float time, bool result) { LastEvaluatedTime = time; Result = result; }
            public void MarkTriggered(float time) { LastTriggeredTime = time; }

            public string FromName => From?.GetType().Name ?? "Any";
            public string ToName => To?.GetType().Name ?? (IsExit ? "EXIT(Null)" : "Null");

            public string GetDescription()
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Description;
#else
                return "Lambda"; // Release trả về string tĩnh
#endif
            }

            // ---------------------------------------------------------------------
            // DEBUG / EDITOR ONLY HELPERS (Stripped in Release)
            // ---------------------------------------------------------------------
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private static string SimplifyExpression(Expression<Func<bool>> expr)
            {
                try
                {
                    // Logic phân tích Expression Tree (Giữ nguyên logic cũ của bạn ở đây)
                    // ... (Code dài dòng phân tích BinaryExpression, MethodCall...)
                    // Để ngắn gọn tôi dùng tạm body.ToString, bạn hãy paste lại hàm cũ của bạn vào đây
                    return CleanExpressionString(expr.Body.ToString());
                }
                catch { return "[expr]"; }
            }

            private static string CleanExpressionString(string s) 
            {
                // Simple clean up logic
                if (s.Contains("value(")) return "Variable"; 
                return s;
            }
#endif
        }
    }
}