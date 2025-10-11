using System;
using System.Collections.Generic;
using UnityEngine;

namespace OSK.AI.FSM
{
    public class FSMManager : MonoBehaviour
    {
        public Dictionary<string, StateMachine> k_GroupStateMachines = new Dictionary<string, StateMachine>();

 
        public StateMachine Create(string groupName)
        {
            var stateMachine = new StateMachine();

            if (HasGroup(groupName))
                return GetGroup(groupName);

            k_GroupStateMachines.Add(groupName, stateMachine);
            return stateMachine;
        }

        private bool HasGroup(string groupName)
        {
            return k_GroupStateMachines.ContainsKey(groupName);
        }

        public StateMachine GetGroup(string groupName)
        {
            if (HasGroup(groupName))
                return k_GroupStateMachines[groupName];
            return null;
        }

        public void Remove(string groupName)
        {
            if (k_GroupStateMachines.ContainsKey(groupName))
            {
                GetGroup(groupName).RemoveAll();
                k_GroupStateMachines.Remove(groupName);
            }
        }

        public void Exit(string groupName)
        {
            if(GetGroup(groupName) != null)
                GetGroup(groupName).Exit();
        }
    }
}