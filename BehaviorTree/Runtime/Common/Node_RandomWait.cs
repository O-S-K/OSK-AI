using UnityEngine;
 
namespace OSK.AITreeBehavior
{
    public class Node_RandomWait : Node
    {
        private float _min;
        private float _max;
        private float _endTime;
        private bool _waiting;

        public Node_RandomWait(float min, float max)
        {
            _min = min;
            _max = max;
        }

        public override NodeState Evaluate()
        {
            if (!_waiting)
            {
                _waiting = true;
                _endTime = Time.time + Random.Range(_min, _max);
            }

            if (Time.time < _endTime)
                return State = NodeState.RUNNING;

            _waiting = false;
            return State = NodeState.SUCCESS;
        }
    }
}