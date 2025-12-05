using OSK.AIFSM;
using UnityEngine;

public class EnemyFSM2 : FSMMono
{
    // --- Define states (private so AddAll reflection tìm thấy) ---
    private IState idleState, patrolState, chaseState, attackState, fleeState, knockback, deadState;

    // Example tunables
    public Transform player;
    public Transform[] patrolPoints;

    public float moveSpeed = 3.5f;
    public float rotateSpeed = 8f;
    public float detectionRange = 6f;
    public float attackRange = 1.5f;
    public float health = 100f;
    public float fleeHealthThreshold = 25f;
    public bool isKnockbacked = false;

    private Vector3 destination;
    private int patrolIndex;


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

    // --------------------------------------------------------
    // Example condition methods for transitions
    // --------------------------------------------------------
    private bool IsPlayerInDetectionRange() => player && (player.position - transform.position).sqrMagnitude <= detectionRange * detectionRange;
    private bool IsPlayerNotDetectionRange() => !(player && (player.position - transform.position).sqrMagnitude <= detectionRange * detectionRange);

    private bool IsPlayerInAttackRange() => player && (player.position - transform.position).sqrMagnitude <= attackRange * attackRange;
    private bool IsPlayerNotAttackRange() => !(player && (player.position - transform.position).sqrMagnitude <= attackRange * attackRange);

    private bool HasPatrolPoints() => patrolPoints != null && patrolPoints.Length > 0;

    private bool IsLowHP() => health <= fleeHealthThreshold && health > 0;
    private bool IsHealthy() => health > fleeHealthThreshold;
    private bool IsDead() => health <= 0;
    private bool Knocked() => isKnockbacked;

    public void SetDestination(Vector3 pos) => destination = pos;

    private bool ReachedDestination(float threshold = 0.6f)
    {
        Vector3 d = destination - transform.position;
        d.y = 0;
        return d.sqrMagnitude <= (threshold * threshold);
    }


    protected override void Update()
    {
        base.Update();
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
                Vector3 move = dir.normalized * moveSpeed * Time.deltaTime;
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // draw destination
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(destination, 0.12f);
    }

    // --------------------------------------------------------
    // States (compact)
    // --------------------------------------------------------
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
        private readonly EnemyFSM2 owner;

        public S_Patrol(EnemyFSM2 o)
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

        public Color GizmoState() => Color.cyan;
    }

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
        private readonly EnemyFSM2 owner;
        private Vector3 fleeDest;
        private float fleeTime = 2.0f;

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
        private readonly EnemyFSM2 owner;

        public S_Dead(EnemyFSM2 o)
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
        private readonly EnemyFSM2 owner;

        public S_Knockback(EnemyFSM2 o)
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
}