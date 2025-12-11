namespace OSK.AIFSM
{
    public interface IFSMInspectableFinal
    {
        AIFSM.FinalStateMachine GetFinalFSM();
        string GetFSMName();
    }
}