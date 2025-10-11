namespace OSK.AI.FSM
{
    public interface ISubStateMachine : IState
    {
        public string GetCurrentStateName();
    }
}