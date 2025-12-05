using OSK.AIFSM;
using UnityEngine;
using OSK.AIFSM.Editor;
using Sirenix.OdinInspector;

namespace FSM_Example
{
    public class EnemyFSM : MonoBehaviour, IFSMInspectableFinal
    {
        [Header("AI Targets & Patrol")]
        public Transform[] patrolPoints;

        public Transform player;

        [Header("Stats")]
        public float detectionRange = 8f;

        public float attackRange = 1.6f;
        public float fleeHealthThreshold = 20f;
        public float maxHealth = 100f;
        public float moveSpeed = 3.5f;
        public float rotateSpeed = 8f;

        [Header("Debug")]
        public bool debugLogs = true;
        public bool isKnockbacked = false;

        [Button]
        private void ContextOpenFSMDebugger()
        {
#if UNITY_EDITOR
            // open and auto-select this owner in the Final FSM Debugger
            FSMDebug.OpenFor(this);
#endif
        }

        // runtime
        [ReadOnly]
        public float health;

        private FinalStateMachine fsm;

        // expose for debug window/reflection
        public FinalStateMachine FinalFsm => fsm;
        public FinalStateMachine GetFinalFSM() => fsm;
        public string GetFSMName() => gameObject.name + ".FinalFSM";

        // state refs
        [SerializeReference]
        private IState idleState, patrolState, chaseState, attackState, fleeState, knockback, deadState;

        // movement helper
        private Vector3 currentDestination;
        private int patrolIndex = 0;

        void Awake()
        {
            health = maxHealth;
            BuildFSM();
        }

        void Update()
        {
            // test damage
            if (Input.GetKeyDown(KeyCode.T))
            {
                TakeDamage(30f);
            }

            // drive FSM
            fsm?.Tick();

            // simple movement: move towards currentDestination if we have one
            HandleMovement();
        }

        void FixedUpdate()
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
            if ((currentDestination - transform.position).sqrMagnitude > 0.01f)
            {
                Vector3 dir = (currentDestination - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Vector3 move = dir.normalized * moveSpeed * Time.deltaTime;
                    if (move.sqrMagnitude > dir.sqrMagnitude) move = dir; // dont overshoot
                    transform.position += move;

                    // smooth look
                    Quaternion want = Quaternion.LookRotation(dir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * rotateSpeed);
                }
            }
        }

        private void SetDestination(Vector3 dest)
        {
            currentDestination = dest;
        }

        private bool ReachedDestination(float threshold = 0.6f)
        {
            Vector3 d = currentDestination - transform.position;
            d.y = 0;
            return d.sqrMagnitude <= (threshold * threshold);
        }

        // =====================================================
        // FSM build (uses FSMBuilder for fluency)
        // =====================================================
        void BuildFSM()
        {
            // create states
            idleState = new S_Idle(this);
            patrolState = new S_Patrol(this);
            chaseState = new S_Chase(this);
            attackState = new S_Attack(this);
            fleeState = new S_Flee(this);
            knockback = new S_Knockback(this);
            deadState = new S_Dead(this);

            // build with builder for clearer code
            var builder = new FSMBuilder()
            //builder.Add(idleState, patrolState, chaseState, attackState, fleeState, knockback, deadState)
                .AddAll(this)
                .Any(deadState, () => health <= 0f, priority: 100)
                // flee if low health
                .Any(fleeState, () => health > 0f && health <= fleeHealthThreshold, priority: 50)
                .Any(knockback , () => isKnockbacked, priority: 30) // placeholder for knockback trigger
                .Exit(knockback , () => !isKnockbacked, priority: 5) // placeholder to exit knockback

                // Normal transitions (use expression variety)
                .At(idleState, patrolState, () => HasPatrolPoints(), priority: 0)
                .At(patrolState, idleState, () => false, priority: 0) // placeholder if you want timer
                .At(idleState, chaseState, () => IsPlayerInRange(detectionRange), priority: 10)
                .At(patrolState, chaseState, () => IsPlayerInRange(detectionRange), priority: 10)
                .At(chaseState, attackState, () => IsPlayerInRange(attackRange), priority: 20)
                .At(attackState, chaseState, () => !IsPlayerInRange(attackRange), priority: 5)
                .At(chaseState, patrolState, () => !IsPlayerInRange(detectionRange) && HasPatrolPoints(), priority: 0)
                .At(fleeState, idleState, () => health > fleeHealthThreshold, priority: 0)
                 .Init(idleState);
            
            fsm = builder.Build();

            if (debugLogs)
                Debug.Log($"[{name}] FSM built. Start state: {fsm.GetCurrentState()?.GetType().Name ?? "null"}");
        }

        bool HasPatrolPoints() => patrolPoints != null && patrolPoints.Length > 0;

        bool IsPlayerInRange(float r)
        {
            if (player == null) return false;
            return Vector3.SqrMagnitude(player.position - transform.position) <= (r * r);
        }

        // =====================================================
        // States
        // =====================================================

        public class S_Idle : IState
        {
            private readonly EnemyFSM owner;
            private float waitTime;

            public S_Idle(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Idle";

            public void OnEnter()
            {
                waitTime = Random.Range(1f, 3f);
                owner.SetDestination(owner.transform.position); // clear dest
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Idle (wait {waitTime:0.0}s)");
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

            public Color GizmoState() => Color.green;
        }

        public class S_Patrol : IState
        {
            private readonly EnemyFSM owner;

            public S_Patrol(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Patrol";

            public void OnEnter()
            {
                if (!owner.HasPatrolPoints()) return;
                owner.patrolIndex = Mathf.Clamp(owner.patrolIndex, 0, Mathf.Max(0, owner.patrolPoints.Length - 1));
                var dest = owner.patrolPoints[owner.patrolIndex].position;
                owner.SetDestination(dest);
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Patrol -> idx {owner.patrolIndex}");
            }

            public void Tick()
            {
                if (!owner.HasPatrolPoints()) return;
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

            public Color GizmoState() => Color.cyan;
        }

        public class S_Chase : IState
        {
            private readonly EnemyFSM owner;

            public S_Chase(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Chase";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Chase");
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
            }

            public Color GizmoState() => Color.yellow;
        }

        public class S_Attack : IState
        {
            private readonly EnemyFSM owner;
            private float attackTimer;
            private float attackInterval = 1.0f;

            public S_Attack(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Attack";

            public void OnEnter()
            {
                attackTimer = 0f;
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Attack");
                owner.SetDestination(owner.transform.position); // stop moving
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
            }

            public Color GizmoState() => Color.red;
        }

        public class S_Flee : IState
        {
            private readonly EnemyFSM owner;
            private Vector3 fleeDest;
            private float fleeTime = 2.0f;

            public S_Flee(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Flee";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Flee (hp={owner.health})");
                if (owner.player != null)
                    fleeDest = owner.transform.position +
                               (owner.transform.position - owner.player.position).normalized * 4f;
                else
                    fleeDest = owner.transform.position - owner.transform.forward * 4f;
                owner.SetDestination(fleeDest);
                fleeTime = 2.0f;
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
            }

            public Color GizmoState() => Color.magenta;
        }

        public class S_Dead : IState
        {
            private readonly EnemyFSM owner;

            public S_Dead(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Dead";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Dead");
                // optional: play animation, disable collider, etc.
                owner.enabled = false; // stop Update & movement
            }

            public void Tick()
            {
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
            }

            public Color GizmoState() => Color.black;
        }
        
        public class S_Knockback : IState
        {
            private readonly EnemyFSM owner;

            public S_Knockback(EnemyFSM o)
            {
                owner = o;
            }

            public string StateName => "Knockback";

            public void OnEnter()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} ENTER Knockback");
            }

            public void Tick()
            {
                
            }

            public void FixedTick()
            {
            }

            public void OnExit()
            {
                if (owner.debugLogs) Debug.Log($"{owner.name} EXIT Knockback");
            }

            public Color GizmoState() => Color.white;
        } 
        
        // gizmos
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // draw destination
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentDestination, 0.12f);
        }
    }
}