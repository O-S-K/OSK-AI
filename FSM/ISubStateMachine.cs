namespace OSK.AIFSM
{
    public interface ISubStateMachine : IState
    {
        public string GetCurrentStateName();
    }
}