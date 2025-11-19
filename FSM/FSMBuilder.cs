using System;
using System.Linq.Expressions;

namespace OSK.AIFSM
{
    /// <summary>
    /// Fluent builder for FinalStateMachine
    /// Usage:
    ///   var fsm = new FSMBuilder()
    ///               .Add(idle, patrol, chase)
    ///               .AtExpr(idle, patrol, () => HasPatrolPoints(), 0)
    ///               .Any(dead, () => health <= 0, "Health <= 0", 100)
    ///               .Init(idle)
    ///               .Build();
    /// </summary>
    public class FSMBuilder
    {
        private readonly FinalStateMachine _fsm = new FinalStateMachine();

        /// <summary>Add one or multiple states to the FSM.</summary>
        public FSMBuilder Add(params IState[] states)
        {
            if (states == null) return this;
            _fsm.Add(states);
            return this;
        }

        /// <summary>Add a transition using a plain predicate and optional description/priority.</summary>
        public FSMBuilder At(IState from, IState to, Func<bool> predicate, string description = null, int priority = 0)
        {
            _fsm.At(from, to, predicate, description);
            // NOTE: FinalStateMachine.At signature earlier had (IState, IState, Func<bool>)
            // if your FinalStateMachine.At also accepts description/priority overloads, adapt as needed.
            return this;
        }

        /// <summary>Add a transition using an expression; builder will compile expression and pass to FSM,
        /// keeping expression body as description for editor/runtime debugging.</summary>
        public FSMBuilder AtExpr(IState from, IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            if (expr == null) return this;
            _fsm.At(from, to, expr, priority);
            return this;
        }

        /// <summary>Add an 'any' transition (from any state) using predicate + optional description/priority.</summary>
        public FSMBuilder Any(IState to, Func<bool> predicate, string description = null, int priority = 0)
        {
            _fsm.Any(to, predicate, description, priority);
            return this;
        }

        /// <summary>Add an 'any' transition using an expression (auto description).</summary>
        public FSMBuilder AnyExpr(IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            if (expr == null) return this;
            _fsm.Any(to, expr, priority);
            return this;
        }

        /// <summary>Initialize FSM start state (will call OnEnter immediately when Init is called).</summary>
        public FSMBuilder Init(IState start)
        {
            if (start != null) _fsm.Init(start);
            return this;
        }

        /// <summary>Return built FinalStateMachine instance.</summary>
        public FinalStateMachine Build() => _fsm;

        /// <summary>Expose internal FSM while building (if you need to tweak it directly).</summary>
        public FinalStateMachine Fsm => _fsm;
    }
}
