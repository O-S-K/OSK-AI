using System;

namespace OSK.AI.TreeBehavior
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
