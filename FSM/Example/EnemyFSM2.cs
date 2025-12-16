using System.Collections;
using OSK.AIFSM;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FSM_Example
{
    public class EnemyFSM2 : FSMMono
    {
        // --- Define states (private so AddAll reflection tìm thấy) ---
        private IState idleState, patrolState, chaseState, attackState, fleeState, knockback, deadState;

        // Example tunables
        public Transform player;
        public Transform[] patrolPoints;

        public float moveSpeed = 3.5f;
        public float rotateSpeed = 8f;
        public float detectionChaseRange = 5f;
        public float attackRange = 1.5f;
        public float health = 100f;
        public float fleeHealthThreshold = 25f;
        public bool isKnockbacked = false;

        private Vector3 destination;
        private int patrolIndex;
        private Renderer renderer;
        private Coroutine knockbackCoroutine;

        private void Awake()
        {
            renderer = GetComponentInChildren<Renderer>();
        }

        // --------------------------------------------------------
        // CREATE STATES (the only thing subclasses implement)
        // --------------------------------------------------------
        protected override void CreateStates()
        {
            idleState = new S_Idle(this);
            patrolState = new S_Patrol(this);
            chaseState = new S_Chase(this);
            attackState = new S_Attack(this);
            fleeState = new S_Flee(this);
            knockback = new S_Knockback(this);
            deadState = new S_Dead(this);
        }

        protected override void OnBuildCustomFSM(FSMBuilder builder)
        {
            base.OnBuildCustomFSM(builder);
        }

        // --------------------------------------------------------
        // Example condition methods for transitions
        // --------------------------------------------------------

        private bool CanSeePlayer()
        {
            if (player == null) return false;
            Vector3 d = player.position - transform.position;
            d.y = 0;
            return d.sqrMagnitude <= (detectionChaseRange * detectionChaseRange);
        }

        private bool CanAttackPlayer()
        {
            if (player == null) return false;
            Vector3 d = player.position - transform.position;
            d.y = 0;
            return d.sqrMagnitude <= (attackRange * attackRange);
        }

        private bool IsLowHP() => health <= fleeHealthThreshold && health > 0;
        private bool IsDead() => health <= 0;
        private bool Knocked() => isKnockbacked;

        public void SetDestination(Vector3 pos) => destination = pos;

        public void SetKnockBack()
        {
            if (knockbackCoroutine != null)
                StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = StartCoroutine(TimeKnockback());
        }

        private IEnumerator TimeKnockback()
        {
            isKnockbacked = true;
            yield return new WaitForSeconds(0.5f);
            isKnockbacked = false;
            knockbackCoroutine = null;
        }

        public void SetColor(Color color)
        {
            renderer.material.color = color;
        }

        private bool ReachedDestination(float threshold = 0.6f)
        {
            Vector3 d = destination - transform.position;
            d.y = 0;
            return d.sqrMagnitude <= (threshold * threshold);
        }


        protected override void Update()
        {
            base.Update();

            if (!isKnockbacked)
                HandleMovement();
        }

        [Button]
        public void TestKnockback()
        {
            if (isKnockbacked)
                return;
            isKnockbacked = true;
            TakeDamage(10f);
        }

        [Button]
        private void TestAddHealth()
        {
            health += 20f;
            if (health > 100f) health = 100f;
        }

        [Button]
        private void TestDie()
        {
            TakeDamage(999f);
        }

        protected void FixedUpdate()
        {
            fsm?.FixedTick();
        }

        void TakeDamage(float amount)
        {
            health -= amount;
            if (debugLogs) Debug.Log($"{name} took {amount} dmg => hp={health}");
            if (health <= 0f) health = 0f;
        }

        // =====================================================
        // Movement helper (no NavMesh)
        // =====================================================
        private void HandleMovement()
        {
            // if destination valid and not too close, move
            if ((destination - transform.position).sqrMagnitude > 0.01f)
            {
                Vector3 dir = (destination - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Vector3 move = dir.normalized * (moveSpeed * Time.deltaTime);
                    if (move.sqrMagnitude > dir.sqrMagnitude) move = dir; // dont overshoot
                    transform.position += move;

                    // smooth look
                    Quaternion want = Quaternion.LookRotation(dir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * rotateSpeed);
                }
            }
        }

        // gizmos
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectionChaseRange);

            // draw destination
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, destination);
        }

        // --------------------------------------------------------
        // States (compact)
        // --------------------------------------------------------
        [System.Serializable] 
        public class S_Idle : IState
        {
            private readonly EnemyFSM2 owner;
            private float waitTime;

            public S_Idle(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Idle";

            public void OnEnter()
            {
                waitTime = Random.Range(1f, 3f);
                owner.SetDestination(owner.transform.position); // clear dest
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Idle (wait {waitTime:0.0}s)");
                owner.SetColor(Color.white);
            }

            public void Tick()
            {
                waitTime -= Time.deltaTime;
                // nothing else; transitions drive next
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Idle");
            }
        }
        
        [System.Serializable] 
        public class S_Patrol : IState
        {
            private readonly EnemyFSM2 owner;

            public S_Patrol(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Patrol";

            public void OnEnter()
            {
                owner.patrolIndex = Mathf.Clamp(owner.patrolIndex, 0, Mathf.Max(0, owner.patrolPoints.Length - 1));
                var dest = owner.patrolPoints[owner.patrolIndex].position;
                owner.SetDestination(dest);
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Patrol -> idx {owner.patrolIndex}");
            }

            public void Tick()
            {
                //if (!owner.HasPatrolPoints()) return;
                if (owner.ReachedDestination(0.6f))
                {
                    owner.patrolIndex = (owner.patrolIndex + 1) % owner.patrolPoints.Length;
                    owner.SetDestination(owner.patrolPoints[owner.patrolIndex].position);
                }
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Patrol");
            }
        }
        
        [System.Serializable] 
        public class S_Chase : IState
        {
            private readonly EnemyFSM2 owner;

            public S_Chase(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Chase";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Chase");
                owner.SetColor(Color.yellow);
            }

            public void Tick()
            {
                if (owner.player == null) return;
                owner.SetDestination(owner.player.position);
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Chase");
                owner.SetColor(Color.white);
            }
        }
        
        [System.Serializable] 
        public class S_Attack : IState
        {
            private readonly EnemyFSM2 owner;
            private float attackTimer;
            private float attackInterval = 1.0f;

            public S_Attack(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Attack";

            public void OnEnter()
            {
                attackTimer = 0f;
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Attack");
                owner.SetDestination(owner.transform.position); // stop moving
                owner.SetColor(Color.green);
            }

            public void Tick()
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = attackInterval;
                    if (owner.player != null &&
                        Vector3.SqrMagnitude(owner.player.position - owner.transform.position) <=
                        owner.attackRange * owner.attackRange)
                    {
                        if (owner.debugLogs) Debug.Log($"{owner.name} attacks player!");
                        // In real game: apply damage
                    }
                }
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Attack");
                owner.SetColor(Color.white);
            }
        }

        [System.Serializable] 
        public class S_Flee : IState
        {
            private readonly EnemyFSM2 owner;
            private Vector3 fleeDest;
            private float fleeTime = 5.0f;

            public S_Flee(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Flee";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Flee (hp={owner.health})");
                if (owner.player != null)
                    fleeDest = owner.transform.position +
                               (owner.transform.position - owner.player.position).normalized * 10f;
                else
                    fleeDest = owner.transform.position - owner.transform.forward * 10f;
                owner.SetDestination(fleeDest);
                fleeTime = 2.0f;
                owner.SetColor(Color.magenta);
            }

            public void Tick()
            {
                fleeTime -= Time.deltaTime;
                if (fleeTime <= 0f)
                {
                    // reached flee time, stop moving
                    owner.SetDestination(owner.transform.position);
                }
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Flee");
                owner.SetColor(Color.white);
            }
        }

        [System.Serializable] 
        public class S_Knockback : IState
        {
            private readonly EnemyFSM2 owner;
            private Vector3 knockBackDest;

            public S_Knockback(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Knockback";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Flee (hp={owner.health})");
                owner.SetKnockBack();
                owner.SetColor(Color.blue);
            }

            public void Tick()
            {
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Flee");
                owner.SetColor(Color.white);
            }
        }

        [System.Serializable] 
        public class S_Dead : IState
        {
            private readonly EnemyFSM2 owner;

            public S_Dead(EnemyFSM2 o)
            {
                owner = o;
            }

            public string StateName => "Dead";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Dead");
                owner.enabled = false; // stop Update & movement
                owner.SetColor(Color.black);
            }

            public void Tick()
            {
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Dead");
            }
        }
    }
}