using System;

namespace OSK.AITreeBehavior
{
    public class ActionNode : Node
    {
        private readonly Func<NodeState> _action;

        public ActionNode(Func<NodeState> action)
        {
            _action = action;
        }

        public override NodeState Evaluate() => State = _action.Invoke();
    }
}
