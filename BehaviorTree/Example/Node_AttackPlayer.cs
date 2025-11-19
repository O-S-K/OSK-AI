using UnityEngine;
using OSK.AIBT;

namespace OSK.AI.Enemy
{
    public class Node_AttackPlayer : Node
    {
        private Transform _enemy;
        private Transform _player;
        private float _range;
        private float _cooldown = 2f;
        private float _nextAttackTime;

        public Node_AttackPlayer(Transform enemy, Transform player, float range)
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
            if (dist > _range)
                return State = NodeState.FAILURE;

            if (Time.time >= _nextAttackTime)
            {
                Debug.Log($"⚔️ {_enemy.name} attacks {_player.name}!");
                _nextAttackTime = Time.time + _cooldown;
                return State = NodeState.SUCCESS;
            }

            return State = NodeState.RUNNING;
        }
    }
}
