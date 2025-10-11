using UnityEngine;

public class IdleState : IState
{
    private readonly Enemy _enemy;
    private float _timer;

    public IdleState(Enemy e) => _enemy = e;

    public void OnEnter()
    {
        Debug.Log("Idle: Enter");
        _timer = 2f;
    }

    public void OnExit() => Debug.Log("Idle: Exit");
    public void OnUpdate()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0)
            _enemy.ReadyToPatrol = true;
    }

    public void OnFixedUpdate()
    {
         
    }
}

public class PatrolState : IState
{
    private readonly Enemy _enemy;
    public PatrolState(Enemy e) => _enemy = e;

    public void OnEnter() => Debug.Log("Patrol: Enter");
    public void OnExit() => Debug.Log("Patrol: Exit");
    public void OnUpdate()
    {
        _enemy.Patrol();
    }
    public void OnFixedUpdate() { }
    
}

public class ChaseState : IState
{
    private readonly Enemy _enemy;
    public ChaseState(Enemy e) => _enemy = e;

    public void OnEnter() => Debug.Log("Chase: Enter");
    public void OnExit() => Debug.Log("Chase: Exit");
    public void OnUpdate() => _enemy.ChasePlayer();
    public void OnFixedUpdate() { }
    
}

public class AttackState : IState
{
    private readonly Enemy _enemy;
    public AttackState(Enemy e) => _enemy = e;

    public void OnEnter() => Debug.Log("Attack: Enter");
    public void OnExit() => Debug.Log("Attack: Exit");
    public void OnUpdate() => _enemy.AttackPlayer();
    public void OnFixedUpdate() { }
    
}

public class DeadState : IState
{
    private readonly Enemy _enemy;
    public DeadState(Enemy e) => _enemy = e;

    public void OnEnter() => Debug.Log("Dead: Enemy is dead");
    public void OnExit() { }
    public void OnUpdate() { }
    public void OnFixedUpdate() { }
    
}