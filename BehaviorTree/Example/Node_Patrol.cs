using UnityEngine;
using OSK.AI.TreeBehavior;

namespace OSK.AI.Enemy
{
    public class Node_Patrol : Node
    {
        private Transform _enemy;
        private Vector3 _origin;
        private Vector3 _target;
        private float _radius;
        private float _speed;

        public Node_Patrol(Transform enemy, float radius, float speed)
        {
            _enemy = enemy;
            _origin = enemy.position;
            _radius = radius;
            _speed = speed;
            _target = GetNewPatrolPoint();
        }

        private Vector3 GetNewPatrolPoint()
        {
            return _origin + new Vector3(Random.Range(-_radius, _radius), 0, Random.Range(-_radius, _radius));
        }

        public override NodeState Evaluate()
        {
            _enemy.position = Vector3.MoveTowards(_enemy.position, _target, _speed * Time.deltaTime);

            if (Vector3.Distance(_enemy.position, _target) < 0.3f)
                _target = GetNewPatrolPoint();

            return State = NodeState.RUNNING;
        }
    }
}
