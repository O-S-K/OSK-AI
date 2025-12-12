namespace OSK.AIFSM
{
    public interface IState
    {
        public string StateName { get; }
        public void OnEnter();
        public void Tick();
        public void FixedTick();
        public void OnExit();
    }
}