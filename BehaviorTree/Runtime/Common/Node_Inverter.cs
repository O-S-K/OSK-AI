using OSK.AIBT;

namespace OSK.AIBT
{
    public class Node_Inverter : Node
    {
        private Node _child;

        public Node_Inverter(Node child)
        {
            _child = child;
            Attach(child);
        }

        public override NodeState Evaluate()
        {
            switch (_child.Evaluate())
            {
                case NodeState.SUCCESS: return State = NodeState.FAILURE;
                case NodeState.FAILURE: return State = NodeState.SUCCESS;
                default: return State = NodeState.RUNNING;
            }
        }
    }
}