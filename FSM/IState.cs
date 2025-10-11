using UnityEngine;

namespace OSK.AI.FSM
{
    public interface IState
    {
        public void OnEnter();
        public void Tick();
        public void FixedTick();
        public void OnExit();

        public Color GizmoState() => Color.clear;
    }
}