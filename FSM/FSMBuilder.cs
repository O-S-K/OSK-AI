using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace OSK.AIFSM
{
    public class FSMBuilder
    {
        private readonly FinalStateMachine _fsm = new FinalStateMachine();
        private readonly Dictionary<Type, FieldInfo[]> cachedStateFields = new();

        public FSMBuilder() { }

        /// <summary>Add one or multiple states to the FSM.</summary>
        public FSMBuilder Add(params IState[] states)
        {
            if (states == null) return this;
            _fsm.Add(states);
            return this;
        }
        
        public  FSMBuilder AddAll(object owner, bool debug = false)
        {
            var type = owner.GetType();
            if (!cachedStateFields.TryGetValue(type, out var fields))
            {
                fields = type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.FlattenHierarchy);

                cachedStateFields[type] = fields;
            }

            foreach (var f in fields)
            {
                // Chỉ lấy field là IState hoặc subclass của nó
                if (!typeof(IState).IsAssignableFrom(f.FieldType))
                    continue;

                if (f.GetValue(owner) is not IState state)
                    continue;

                _fsm.Add(state);
                if (debug)
                    Debug.Log($"[FSM AutoAdd] {type.Name} → Add state: <color=#00ff88>{state.GetType().Name}</color>  (field: {f.Name})");
            }

            return this;
        }

        /// <summary>Add a transition using a plain predicate and optional description/priority.</summary>
        public FSMBuilder At(IState from, IState to, Func<bool> predicate, string description = null, int priority = 0)
        {
            _fsm.At(from, to, predicate, description, priority);
            return this;
        }

        /// <summary>Add a transition using an expression; builder will compile expression and pass to FSM.</summary>
        public FSMBuilder AtExpr(IState from, IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            if (expr == null) return this;
            _fsm.At(from, to, expr, priority);
            return this;
        }

        /// <summary>Add an 'any' transition (from any state). Returns TransitionBuilder so you can chain .Exit(...) nicely.</summary>
        public FSMBuilder Any(IState to, Func<bool> predicate, string description = null, int priority = 0)
        {
            _fsm.Any(to, predicate, description, priority);
            return this;
        }

        /// <summary>Any overload with expression (auto description).</summary>
        public FSMBuilder AnyExpr(IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            _fsm.Any(to, expr, priority);
            return this;
        }

        /// <summary>Initialize FSM start state (will call OnEnter immediately when Init is called).</summary>
        public FSMBuilder Init(IState start)
        {
            if (start != null) _fsm.Init(start);
            return this;
        }

        /// <summary>Register an exit predicate for a state (semantic: force-exit when predicate true).</summary>
        public FSMBuilder Exit(IState from, Func<bool> predicate, string description = null, int priority = 0)
        {
            _fsm.Exit(from, predicate, description, priority);
            return this;
        }

        /// <summary>Exit using Expression (auto description).</summary>
        public FSMBuilder ExitExpr(IState from, Expression<Func<bool>> expr, int priority = 0)
        {
            if (expr == null) return this;
            _fsm.Exit(from, expr, priority);
            return this;
        }

        /// <summary>Return built FinalStateMachine instance.</summary>
        public FinalStateMachine Build() => _fsm;

        /// <summary>Expose internal FSM while building.</summary>
        public FinalStateMachine Fsm => _fsm;
    }
}
