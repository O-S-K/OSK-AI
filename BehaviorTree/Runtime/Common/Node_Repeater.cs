 
namespace OSK.AIBT
{
    public class Node_Repeater : Node
    {
        private Node _child;
        private int _repeatCount;
        private int _currentCount;

        public Node_Repeater(Node child, int repeatCount = -1)
        {
            _child = child;
            _repeatCount = repeatCount;
            Attach(child);
        }

        public override NodeState Evaluate()
        {
            if (_repeatCount < 0)
            {
                _child.Evaluate();
                return State = NodeState.RUNNING;
            }

            if (_currentCount < _repeatCount)
            {
                var result = _child.Evaluate();
                if (result != NodeState.RUNNING)
                    _currentCount++;

                return State = NodeState.RUNNING;
            }

            _currentCount = 0;
            return State = NodeState.SUCCESS;
        }
    }
}