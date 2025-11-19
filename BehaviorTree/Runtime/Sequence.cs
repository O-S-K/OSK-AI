using System.Collections.Generic;

namespace OSK.AIBT
{
    public class Sequence : Node
    {
        public Sequence(params Node[] nodes)
        {
            foreach (var n in nodes) Attach(n);
        }

        public override NodeState Evaluate()
        {
            foreach (var child in Children)
            {
                var result = child.Evaluate();

                if (result == NodeState.FAILURE)
                    return State = NodeState.FAILURE;

                if (result == NodeState.RUNNING)
                    return State = NodeState.RUNNING;
            }
            return State = NodeState.SUCCESS;
        }
    }
}
