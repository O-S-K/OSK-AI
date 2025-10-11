using System;
using System.Collections.Generic;

public class HFSM
{
    private IState _currentState;
    private List<HFSMTransition> _transitions = new();
    private List<(IState to, Func<bool> cond, Func<string> desc)> _anyTransitions = new();
    private Func<bool> _stopAllIf;
    
    // ✅ danh sách state duy nhất (cho Editor)
    private HashSet<IState> _allStates = new HashSet<IState>();
    public IReadOnlyCollection<IState> AllStates => _allStates;
    
    public IState CurrentState => _currentState;

    public void SetStart(IState start)
    {
        _currentState = start;
        _allStates.Add(start);
    }

    public void AddTransition(IState from, IState to, Func<bool> condition, Func<string> desc)
    {
        _transitions.Add(new HFSMTransition(from, to, condition, desc));
        
        _allStates.Add(from);
        _allStates.Add(to);
    }

    public void AddAnyTransition(IState to, Func<bool> condition, Func<string> desc)
    {
        _anyTransitions.Add((to, condition, desc));
        _allStates.Add(to); 
    }
    
    public void AddHFSMState(HierarchicalState hs)
    {
        _allStates.Add(hs);
    }

    public void SetStopAllIf(Func<bool> condition) => _stopAllIf = condition;

    public void OnUpdate()
    {
        if (_stopAllIf?.Invoke() == true)
            return;
        foreach (var t in _anyTransitions)
        {
            if (t.Item2())
            {
                SwitchState(t.Item1);
                return;
            }
        }
        foreach (var t in _transitions)
        {
            if (t.From == _currentState && t.Condition())
            {
                SwitchState(t.To);
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

    private void SwitchState(IState newState)
    {
        if (_currentState == newState) return;
        _currentState?.OnExit();
        _currentState = newState;
        _currentState?.OnEnter();
    }
}