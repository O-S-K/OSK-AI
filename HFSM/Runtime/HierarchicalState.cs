using System;
using System.Collections.Generic;

public class HierarchicalState : IState
{
    public string Name;
    private IState _currentSubState;
    private List<IState> _subStates = new();
    private List<HFSMTransition> _transitions = new();
    private IState _startState;
    
    public IState CurrentSubState => _currentSubState;

    public HierarchicalState(string name)
    {
        Name = name;
    }

    public void AddSubState(IState state) => _subStates.Add(state);
    public void AddTransition(IState from, IState to, Func<bool> condition, Func<string> description)
    {
        _transitions.Add(new HFSMTransition(from, to, condition, description));
    }
    public void SetStart(IState state) => _startState = state;

    public void OnEnter()
    {
        _currentSubState = _startState;
        _currentSubState?.OnEnter();
    }

    public void OnExit()
    {
         
        _currentSubState?.OnExit();
    }

    public void OnUpdate()
    {
        foreach (var t in _transitions)
        {
            if (t.From == _currentSubState && t.Condition())
            {
                _currentSubState.OnExit();
                _currentSubState = t.To;
                _currentSubState.OnEnter();
                return;
            }
        }

        _currentSubState?.OnUpdate();
    }
    
    public void OnFixedUpdate()
    {
        _currentSubState?.OnFixedUpdate();
    }
}