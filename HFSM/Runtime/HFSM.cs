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

    public void Start(IState start)
    {
        _currentState = start;
        _allStates.Add(start);
    }

    public void Add(IState from, IState to, Func<bool> condition, Func<string> desc)
    {
        _transitions.Add(new HFSMTransition(from, to, condition, desc));
        
        _allStates.Add(from);
        _allStates.Add(to);
    }

    public void Any(IState to, Func<bool> condition, Func<string> desc)
    {
        _anyTransitions.Add((to, condition, desc));
        _allStates.Add(to); 
    }
    
    public void AddHFSM(HierarchicalState hs)
    {
        _allStates.Add(hs);
    }

    public void StopAllIf(Func<bool> condition) => _stopAllIf = condition;

    public void OnUpdate()
    {
        if (_stopAllIf?.Invoke() == true)
            return;
        foreach (var t in _anyTransitions)
        {
            if (t.Item2())
            {
                Switch(t.Item1);
                return;
            }
        }
        foreach (var t in _transitions)
        {
            if (t.From == _currentState && t.Condition())
            {
                Switch(t.To);
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

    private void Switch(IState newState)
    {
        if (_currentState == newState) return;
        _currentState?.OnExit();
        _currentState = newState;
        _currentState?.OnEnter();
    }
}