using System;
using System.Collections.Generic;

public class HFSMBuilder
{
    private HFSM _hfsm = new();
    private Dictionary<string, IState> _namedStates = new();
    private Stack<HierarchicalState> _hfsmStack = new();

    public HFSMBuilder State(IState state, out IState refOut)
    {
        refOut = state;
        _namedStates[state.GetType().Name] = state;
        return this;
    }

    public HFSMBuilder At(IState from, IState to, Func<bool> condition, Func<string> description)
    {
        if (_hfsmStack.Count > 0)
            _hfsmStack.Peek().At(from, to, condition, description);
        else
            _hfsm.Add(from, to, condition, description);
        return this;
    }
    
    public HFSMBuilder Any(IState to, Func<bool> condition, Func<string> description)
    {
        _hfsm.Any(to, condition, description);
        _namedStates.TryAdd(to.GetType().Name, to);
        return this;
    }

    public HFSMBuilder HFSM(HierarchicalState state)
    {
        _hfsmStack.Push(state);
        _namedStates[state.Name] = state;
        _hfsm.AddHFSM(state);
        return this;
    }

    public HFSMBuilder SubState(IState sub, out IState refOut)
    {
        refOut = sub;
        _hfsmStack.Peek().AddSub(sub);
        _namedStates[sub.GetType().Name] = sub;
        return this;
    }

    public HFSMBuilder Start(IState start)
    {
        if (_hfsmStack.Count > 0)
            _hfsmStack.Peek().Start(start);
        else
            _hfsm.Start(start);
        return this;
    }

    public HFSMBuilder EndHFSM()
    {
        var done = _hfsmStack.Pop();
        if (_hfsmStack.Count > 0)
            _hfsmStack.Peek().AddSub(done);
        return this;
    }


    public HFSMBuilder StopAllIf(Func<bool> condition)
    {
        _hfsm.StopAllIf(condition);
        return this;
    }

    public HFSM Build() => _hfsm;
}