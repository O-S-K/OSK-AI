namespace OSK.AIHFSM
{
    using System;

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
        public Func<string> DebugDesc;
        public int Priority;

        public HFSMTransition(IState from, IState to, Func<bool> condition, Func<string> debugDesc = null,
            int priority = 0)
        {
            From = from;
            To = to;
            Condition = condition ?? (() => false);
            DebugDesc = debugDesc;
            Priority = priority;
        }
    }
}