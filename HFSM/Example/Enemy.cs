using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour, IFSMInspectable
{
    public Transform player;
    public float detectRange = 5f;
    public float attackRange = 1.5f;
    public float health = 100f;

    public bool ReadyToPatrol { get; set; }

    private HFSM _hfsm;

    private void Start()
    {
        InitFSM();
    }

    private void Update()
    {
        _hfsm.OnUpdate();
    }
    
    private void FixedUpdate()
    {
        _hfsm.OnFixedUpdate();
    }

    private void InitFSM()
    {
        var combat = new HierarchicalState("Combat");
        _hfsm = new HFSMBuilder()
            .State(new IdleState(this), out var idle)
            .State(new PatrolState(this), out var patrol)
            .State(new DeadState(this), out var dead)

            .HFSM(combat)
                .SubState(new ChaseState(this), out var chase)
                .SubState(new AttackState(this), out var attack)
                .Transition(chase, attack, () => IsPlayerInAttackRange(), () => $"Dist {DistanceToPlayer:0.0} < {attackRange}")
                .Transition(attack, chase, () => !IsPlayerInAttackRange(), () => $"Dist {DistanceToPlayer:0.0} > {attackRange}")
                .Start(chase)
            .EndHFSM()

            .Transition(idle, patrol, () => ReadyToPatrol, () => $"Pos Patrol {transform.position:0.0}")
            .Transition(patrol, combat, () => IsPlayerDetected(), () => $"Dist {DistanceToPlayer:0.0} > {detectRange}")
            .Transition(combat, patrol, () => !IsPlayerDetected(), () => $"Dist {DistanceToPlayer:0.0} < {detectRange}")
            .AnyTransition(dead, () => health <= 0, () => $"Health {health:0} <= 0")
           // .StopAllIf(() => health <= 0)
            .Start(idle)
            .Build();
    }

    public float DistanceToPlayer => Vector3.Distance(transform.position, player.position);

    public bool IsPlayerDetected()
    {
        if (player == null) return false;
        return DistanceToPlayer <= detectRange;
    }

    public bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        return DistanceToPlayer <= attackRange;
    }

    public void Patrol()
    {
        transform.Translate(Vector3.forward * Time.deltaTime);
    }

    public void ChasePlayer()
    {
        if (player == null) return;
        transform.position = Vector3.MoveTowards(transform.position, player.position, Time.deltaTime * 2f);
    }

    public void AttackPlayer()
    {
        Debug.Log("Enemy attacking player!");
    }

    public HFSM GetFSM()
    {
        return _hfsm;
    }

    public string GetFSMName()
    {
        return "Enemy FSM";
    }
}
