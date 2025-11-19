using System;

namespace OSK.AIBT
{
    public class ConditionNode : Node
    {
        private readonly Func<bool> _condition;

        public ConditionNode(Func<bool> condition)
        {
            _condition = condition;
        }

        public override NodeState Evaluate()
        {
            bool success = _condition.Invoke();
            return State = success ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
