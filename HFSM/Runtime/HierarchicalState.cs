namespace OSK.AIHFSM
{
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

        public void AddSub(IState state) => _subStates.Add(state);
        public void At(IState from, IState to, Func<bool> condition, Func<string> description, int priority = 0)
        {
            _transitions.Add(new HFSMTransition(from, to, condition, description, priority));
        }
        public void Start(IState state) => _startState = state;

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
                if (t != null && t.From == _currentSubState && t.Condition())
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
}