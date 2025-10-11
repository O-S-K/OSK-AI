using System;
using System.Collections.Generic;
using UnityEngine;

public interface IState
{
    void OnEnter();
    void OnExit();
    void OnUpdate();
    void OnFixedUpdate();
}

public class HFSMTransition
{
    public IState From;
    public IState To;
    public Func<bool> Condition;
    public Func<string> DebugDesc; // ✅ dynamic description for editor

    public HFSMTransition(IState from, IState to, Func<bool> condition, Func<string> debugDesc = null)
    {
        From = from;
        To = to;
        Condition = condition;
        DebugDesc = debugDesc;
    }
}

