using UnityEngine;
using OSK.AITreeBehavior;

namespace OSK.AI.Enemy
{
    public class Node_ChasePlayer : Node
    {
        private Transform _enemy;
        private Transform _player;
        private float _speed;
        private float _stopDistance;

        public Node_ChasePlayer(Transform enemy, Transform player, float speed, float stopDistance)
        {
            _enemy = enemy;
            _player = player;
            _speed = speed;
            _stopDistance = stopDistance;
        }

        public override NodeState Evaluate()
        {
            if (_player == null)
                return State = NodeState.FAILURE;

            float dist = Vector3.Distance(_enemy.position, _player.position);

            if (dist <= _stopDistance)
                return State = NodeState.SUCCESS;

            _enemy.position = Vector3.MoveTowards(_enemy.position, _player.position, _speed * Time.deltaTime);
            return State = NodeState.RUNNING;
        }
    }
}
