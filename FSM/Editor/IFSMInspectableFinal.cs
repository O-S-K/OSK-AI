namespace OSK.AIFSM.Editor
{
    public interface IFSMInspectableFinal
    {
        AIFSM.FinalStateMachine GetFinalFSM();
        string GetFSMName();
    }
}