using OSK.AIHFSM;
using UnityEngine;

namespace OSK.AI.HFSMExample
{
    public class PlayerIdleState : IState
    {
        private PlayerFSM p;
        public PlayerIdleState(PlayerFSM p) => this.p = p;
        public void OnEnter() => Debug.Log("Idle");

        public void OnExit()
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
    }

    public class PlayerMoveState : IState
    {
        private PlayerFSM p;
        public PlayerMoveState(PlayerFSM p) => this.p = p;
        public void OnEnter() => Debug.Log("Move");

        public void OnExit()
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
    }

    public class PlayerDeadState : IState
    {
        private PlayerFSM p;
        public PlayerDeadState(PlayerFSM p) => this.p = p;
        public void OnEnter() => Debug.Log("Dead");

        public void OnExit()
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
    }

    public class PlayerSkill1State : IState
    {
        private PlayerFSM p;
        private float timer;
        public PlayerSkill1State(PlayerFSM p) => this.p = p;

        public void OnEnter()
        {
            timer = 0;
            Debug.Log("Skill 1 ⚡");
        }

        public void OnExit()
        {
        }

        public void OnUpdate()
        {
            timer += Time.deltaTime;
            if (timer > 2f) p.attackPressed = false;
        }

        public void OnFixedUpdate()
        {
        }
    }

    public class PlayerSkill2State : IState
    {
        private PlayerFSM p;
        private float timer;
        public PlayerSkill2State(PlayerFSM p) => this.p = p;

        public void OnEnter()
        {
            timer = 0;
            Debug.Log("Skill 2 🔥");
        }

        public void OnExit()
        {
        }

        public void OnUpdate()
        {
            timer += Time.deltaTime;
            if (timer > 2f) p.attackPressed = false;
        }

        public void OnFixedUpdate()
        {
        }
    }

    public class PlayerSkill3State : IState
    {
        private PlayerFSM p;
        private float timer;
        public PlayerSkill3State(PlayerFSM p) => this.p = p;

        public void OnEnter()
        {
            timer = 0;
            Debug.Log("Skill 3 💥");
        }

        public void OnExit()
        {
        }

        public void OnUpdate()
        {
            timer += Time.deltaTime;
            if (timer > 2f) p.attackPressed = false;
        }

        public void OnFixedUpdate()
        {
        }
    }
}