using UnityEngine;

namespace OSK.AIBT
{
    public class Node_Wait : Node
    {
        private float _waitTime;
        private float _startTime;
        private bool _started;

        public Node_Wait(float waitTime)
        {
            _waitTime = waitTime;
        }

        public override NodeState Evaluate()
        {
            if (!_started)
            {
                _startTime = Time.time;
                _started = true;
            }

            if (Time.time - _startTime >= _waitTime)
            {
                _started = false;
                return State = NodeState.SUCCESS;
            }

            return State = NodeState.RUNNING;
        }
    }
}