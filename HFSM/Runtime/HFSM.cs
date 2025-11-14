namespace OSK.AIHFSM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class HFSM
    {
        private IState _currentState;

        private readonly List<HFSMTransition> _transitions = new List<HFSMTransition>();

        // store any transitions as HFSMTransition with From == null
        private readonly List<HFSMTransition> _anyTransitions = new List<HFSMTransition>();
        private Func<bool> _stopAllIf;

        // danh sách state duy nhất (cho Editor)
        private readonly HashSet<IState> _allStates = new HashSet<IState>();
        public IReadOnlyCollection<IState> AllStates => _allStates;

        public IState CurrentState => _currentState;

        // Start: set state and call OnEnter
        public void Start(IState start)
        {
            _currentState = start ?? throw new ArgumentNullException(nameof(start));
            _allStates.Add(start);
            _currentState.OnEnter();
        }

        // Add explicit transition (from -> to)
        public void Add(IState from, IState to, Func<bool> condition, Func<string> debugDesc = null, int priority = 0)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            var t = new HFSMTransition(from, to, condition ?? (() => false), debugDesc, priority);
            _transitions.Add(t);
            _allStates.Add(from);
            _allStates.Add(to);
        }

        // Any transition (From == null)
        public void Any(IState to, Func<bool> condition, Func<string> debugDesc = null, int priority = 0)
        {
            if (to == null) throw new ArgumentNullException(nameof(to));
            var t = new HFSMTransition(null, to, condition ?? (() => false), debugDesc, priority);
            _anyTransitions.Add(t);
            _allStates.Add(to);
        }

        public void AddHFSM(HierarchicalState hs)
        {
            if (hs == null) throw new ArgumentNullException(nameof(hs));
            _allStates.Add(hs);
        }

        public void StopAllIf(Func<bool> condition) => _stopAllIf = condition;

        public void OnUpdate()
        {
            if (_stopAllIf?.Invoke() == true)
                return;

            // check any transitions first: choose highest priority one which condition true
            HFSMTransition bestAny = null;
            foreach (var t in _anyTransitions)
            {
                try
                {
                    if (t.Condition())
                    {
                        if (bestAny == null || t.Priority > bestAny.Priority)
                            bestAny = t;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (bestAny != null)
            {
                // avoid switching to same state
                if (bestAny.To != _currentState)
                {
                    Switch(bestAny);
                    return;
                }
            }

            // find transition from current state (first match, but prefer higher priority)
            if (_currentState != null)
            {
                HFSMTransition bestLocal = null;
                foreach (var t in _transitions)
                {
                    if (t.From != _currentState) continue;
                    bool cond = false;
                    try
                    {
                        cond = t.Condition();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    if (!cond) continue;

                    if (bestLocal == null || t.Priority > bestLocal.Priority)
                        bestLocal = t;
                }

                if (bestLocal != null && bestLocal.To != _currentState)
                {
                    Switch(bestLocal);
                    return;
                }
            }

            _currentState?.OnUpdate();
        }

        public void OnFixedUpdate()
        {
            if (_stopAllIf?.Invoke() == true)
                return;
            _currentState?.OnFixedUpdate();
        }

        private void Switch(HFSMTransition transition)
        {
            if (transition == null) return;
            var newState = transition.To;
            if (newState == _currentState) return;

            try
            {
                _currentState?.OnExit();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            var prev = _currentState;
            _currentState = newState;

            // debug: show reason
            try
            {
                var reason = transition.DebugDesc?.Invoke();
                Debug.Log(
                    $"HFSM switch: {prev?.GetType().Name ?? "null"} -> {_currentState?.GetType().Name}  reason: {reason}  (priority={transition.Priority})");
            }
            catch
            {
                /* ignore debug desc exceptions */
            }

            try
            {
                _currentState.OnEnter();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // Force a switch without needing a transition object
        public void ForceSwitch(IState newState, string reason = null)
        {
            if (newState == null) throw new ArgumentNullException(nameof(newState));
            if (newState == _currentState) return;

            try
            {
                _currentState?.OnExit();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            var prev = _currentState;
            _currentState = newState;
            Debug.Log(
                $"HFSM ForceSwitch: {prev?.GetType().Name ?? "null"} -> {_currentState.GetType().Name}  reason: {reason}");
            try
            {
                _currentState.OnEnter();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void Exit()
        {
            try
            {
                _currentState?.OnExit();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            _currentState = null;
        }

        // Editor helper
        public IEnumerable<HFSMTransition> GetAllTransitions() => _transitions.Concat(_anyTransitions);
    }
}