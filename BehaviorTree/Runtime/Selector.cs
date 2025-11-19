using System.Collections.Generic;

namespace OSK.AIBT
{
    public class Selector : Node
    {
        public Selector(params Node[] nodes)
        {
            foreach (var n in nodes) Attach(n);
        }

        public override NodeState Evaluate()
        {
            foreach (var child in Children)
            {
                var result = child.Evaluate();

                if (result == NodeState.SUCCESS)
                    return State = NodeState.SUCCESS;

                if (result == NodeState.RUNNING)
                    return State = NodeState.RUNNING;
            }
            return State = NodeState.FAILURE;
        }
    }
}
