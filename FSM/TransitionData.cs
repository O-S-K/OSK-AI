using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OSK.AIFSM
{
    [Serializable]
    public class TransitionData
    {
        [SerializeReference, HideInInspector] public IState from;
        [SerializeReference, HideInInspector] public IState to;

        [LabelText("From (field name)")] public string fromFieldName;
        [LabelText("To (field name)")]   public string toFieldName;

        [ValueDropdown(nameof(ConditionMethodList)), LabelText("Condition Method")]
        public string conditionMethod;

        public bool invertCondition = false;
        [Tooltip("Param string...")]
        public string conditionParam;

        [Range(0,100)]
        public int priority = 0;

        [HideInInspector] public bool hideFromField = false;
        [HideInInspector] public bool hideToField = false;
        
        [NonSerialized] public object targetObject;
        [NonSerialized] public Expression<Func<bool>> cachedExpression;

        public IEnumerable<string> ConditionMethodList()
        {
            if (targetObject == null) return Enumerable.Empty<string>();
            var t = targetObject.GetType();
            return t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m =>
                {
                    if (m.ReturnType != typeof(bool)) return false;
                    var p = m.GetParameters();
                    return p.Length == 0 || p.Length == 1;
                })
                .Select(m => m.Name + (m.GetParameters().Length == 1 ? " (param)" : ""))
                .OrderBy(n => n);
        }
        public IEnumerable<string> GetStateFieldNames()
        {
            if (targetObject == null) return Enumerable.Empty<string>();
            var t = targetObject.GetType();
            return t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Where(f => typeof(IState).IsAssignableFrom(f.FieldType))
                .Select(f => f.Name)
                .OrderBy(n => n);
        }
    }
}