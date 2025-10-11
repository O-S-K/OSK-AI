using UnityEngine;
using System.Linq;

public class PlayerFSM : MonoBehaviour, IFSMInspectable
{
    public float health = 100f;
    public bool attackPressed;
    public bool movePressed;
    public bool[] skillKeys = new bool[3];

    private HFSM _hfsm;

    void Start() => InitFSM();

    void Update()
    {
        // Input test
        movePressed = Input.GetKey(KeyCode.W);
        attackPressed = Input.GetKey(KeyCode.U);
        skillKeys[0] = Input.GetKeyDown(KeyCode.Alpha1);
        skillKeys[1] = Input.GetKeyDown(KeyCode.Alpha2);
        skillKeys[2] = Input.GetKeyDown(KeyCode.Alpha3);

        _hfsm.OnUpdate();
    }

    private void InitFSM()
    {
        var attack = new HierarchicalState("Attack");
        _hfsm = new HFSMBuilder()
            .State(new PlayerIdleState(this), out var idle)
            .State(new PlayerMoveState(this), out var move)
            .State(new PlayerDeadState(this), out var dead)

            .HFSM(attack)
                .SubState(new PlayerSkill1State(this), out var skill1)
                .SubState(new PlayerSkill2State(this), out var skill2)
                .SubState(new PlayerSkill3State(this), out var skill3)

                .Transition(skill1, skill2, () => skillKeys[1], () => "Switch → Skill2")
                .Transition(skill1, skill3, () => skillKeys[2], () => "Switch → Skill3")
                .Transition(skill2, skill1, () => skillKeys[0], () => "Switch → Skill1")
                .Transition(skill2, skill3, () => skillKeys[2], () => "Switch → Skill3")
                .Transition(skill3, skill1, () => skillKeys[0], () => "Switch → Skill1")
                .Transition(skill3, skill2, () => skillKeys[1], () => "Switch → Skill2")

                .Start(skill1)
            .EndHFSM()

            .Transition(idle, move, () => movePressed, () => "W Pressed")
            .Transition(move, attack, () => attackPressed, () => "Attack Pressed")
            .Transition(attack, idle, () => !attackPressed, () => "All Skills Done")

            .AnyTransition(dead, () => health <= 0, () => $"Health {health:0} <= 0")
            .Start(idle)
            .Build();
    }


    public HFSM GetFSM() => _hfsm;
    public string GetFSMName() => "PlayerFSM";
}
