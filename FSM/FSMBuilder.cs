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

        // --- Auto Register States ---
        
        /// <summary>
        /// Add All IState fields from owner to FSM
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public FSMBuilder AddAll(object owner)
        {
            var type = owner.GetType();
            if (!cachedStateFields.TryGetValue(type, out var fields))
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                cachedStateFields[type] = fields;
            }

            foreach (var f in fields)
            {
                if (typeof(IState).IsAssignableFrom(f.FieldType) && f.GetValue(owner) is IState state)
                {
                    _fsm.Add(state);
                }
            }
            return this;
        }
        
        /// <summary>
        /// Add State to FSM
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public FSMBuilder Add(IState state)
        {
            _fsm.Add(state);
            return this;
        }

        // --- Core Transitions (All use Expression) ---

        /// <summary>
        /// Transition: From -> To (Auto description via Expression)
        /// </summary>
        public FSMBuilder At(IState from, IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            _fsm.At(from, to, expr, priority);
            return this;
        }

        /// <summary>
        /// Global Transition: Any -> To
        /// </summary>
        public FSMBuilder Any(IState to, Expression<Func<bool>> expr, int priority = 0)
        {
            _fsm.Any(to, expr, priority);
            return this;
        }

        // --- Exit Transitions ---

        /// <summary>
        /// Exit with Target: From -> To (Interruption)
        /// </summary>
        public FSMBuilder Exit(IState from, IState to, Expression<Func<bool>> expr, int priority = 100)
        {
            _fsm.Exit(from, to, expr, priority);
            return this;
        }

        /// <summary>
        /// Pure Exit: From -> Null (Disable/Reset State)
        /// </summary>
        public FSMBuilder Exit(IState from, Expression<Func<bool>> expr, int priority = 100)
        {
            _fsm.Exit(from, null, expr, priority);
            return this;
        }

        // --- Init & Build ---

        /// <summary>
        /// Initialize FSM with Start State
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public FSMBuilder Init(IState start)
        {
            _fsm.Init(start);
            return this;
        }

        // -- Final Build ---
        public FinalStateMachine Build() => _fsm;
    }
}