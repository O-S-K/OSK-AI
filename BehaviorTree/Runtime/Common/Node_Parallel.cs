using OSK.AITreeBehavior;
using System.Collections.Generic;

namespace OSK.AITreeBehavior
{
    public class Node_Parallel : Node
    {
        public Node_Parallel(params Node[] nodes)
        {
            foreach (var n in nodes) Attach(n);
        }

        public override NodeState Evaluate()
        {
            bool allSuccess = true;

            foreach (var child in Children)
            {
                var result = child.Evaluate();

                if (result == NodeState.FAILURE)
                    return State = NodeState.FAILURE;

                if (result == NodeState.RUNNING)
                    allSuccess = false;
            }

            return State = allSuccess ? NodeState.SUCCESS : NodeState.RUNNING;
        }
    }
}