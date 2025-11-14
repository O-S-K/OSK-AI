using UnityEngine;
using OSK.AITreeBehavior;

namespace OSK.AI.Enemy
{
    public class Node_IsPlayerInRange : Node
    {
        private Transform _enemy;
        private Transform _player;
        private float _range;

        public Node_IsPlayerInRange(Transform enemy, Transform player, float range)
        {
            _enemy = enemy;
            _player = player;
            _range = range;
        }

        public override NodeState Evaluate()
        {
            if (_player == null)
                return State = NodeState.FAILURE;

            float dist = Vector3.Distance(_enemy.position, _player.position);
            return State = (dist <= _range) ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
