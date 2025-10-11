using OSK.AI.Enemy;
using UnityEngine;

namespace OSK.AI.TreeBehavior.Enemy
{
    public class EnemyAI : BehaviorTree
    {
        public Transform player;

        [Header("Settings")]
        public float detectRange = 8f;
        public float attackRange = 1.5f;
        public float moveSpeed = 3f;
        public float patrolRadius = 5f;
        public float patrolSpeed = 1.5f;

        protected override Node SetupTree()
        {
            // Define base actions
            var isPlayerInRange = new Node_IsPlayerInRange(transform, player, detectRange);
            var chasePlayer = new Node_ChasePlayer(transform, player, moveSpeed, attackRange);
            var attackPlayer = new Node_AttackPlayer(transform, player, attackRange);
            var patrol = new Node_Patrol(transform, patrolRadius, patrolSpeed);
            var waitIdle = new Node_Wait(2f);
            var randomWait = new Node_RandomWait(1f, 3f);

            // Behavior sequences
            var chaseAndAttack = new Sequence(isPlayerInRange, chasePlayer, new Node_RandomWait(0.5f, 1.5f), attackPlayer);
            var idleAndPatrol = new Sequence(randomWait, patrol);

            // Decorator: if NOT seeing player → patrol
            var notSeePlayer = new Node_Inverter(isPlayerInRange);
            var patrolIfLost = new Sequence(notSeePlayer, idleAndPatrol);

            // Random: 50% idle, 50% patrol when idle
            var idleVariation = new Node_RandomSelector(true, waitIdle, patrol);

            // Combine all
            var root = new Selector(chaseAndAttack, patrolIfLost, idleVariation);

            return root;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
