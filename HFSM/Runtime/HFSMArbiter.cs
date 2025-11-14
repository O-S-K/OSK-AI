namespace OSK.AIHFSM
{
    using System;
    using System.Collections.Generic;


    public class HFSMArbiter
    {
        // stored as (machine, priority)
        private readonly List<(HFSM machine, int priority)> _machines = new();

        // register HFSM with priority (higher number = higher precedence)
        public void Register(HFSM machine, int priority)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            _machines.Add((machine, priority));
            // keep sorted desc by priority
            _machines.Sort((a, b) => b.priority.CompareTo(a.priority));
        }

        // unregister if needed
        public void Unregister(HFSM machine)
        {
            _machines.RemoveAll(x => x.machine == machine);
        }

        // Called every frame
        public void Tick()
        {
            if (_machines.Count == 0) return;

            // Iterate in priority order high -> low
            foreach (var (machine, priority) in _machines)
            {
                // If the machine has StopAllIf and it returns true, skip ticking this machine (HFSM handles that internally).
                machine.OnUpdate();

                // After ticking, check if machine's current state blocks lower machines.
                var cur = machine.CurrentState;
                if (cur is IBlocksLower)
                {
                    // this state blocks lower-priority machines -> stop processing lower machines
                    break;
                }

                // Alternatively: if you want any other logic (like state property BlocksLower) you can add interface with property.
            }
        }

        // Fixed update pass-through
        public void FixedTick()
        {
            if (_machines.Count == 0) return;
            foreach (var (machine, priority) in _machines)
            {
                machine.OnFixedUpdate();
                var cur = machine.CurrentState;
                if (cur is IBlocksLower)
                    break;
            }
        }

        // Helper to debug current stack
        public IEnumerable<(HFSM machine, int priority, IState current)> GetStatus()
        {
            foreach (var (m, p) in _machines) yield return (m, p, m.CurrentState);
        }
    }
}