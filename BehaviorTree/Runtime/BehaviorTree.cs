using UnityEngine;

namespace OSK.AI.TreeBehavior
{
    public abstract class BehaviorTree : MonoBehaviour
    {
        protected Node root;

        protected virtual void Start()
        {
            root = SetupTree();
        }

        protected virtual void Update()
        {
            root?.Evaluate();
        }

        protected abstract Node SetupTree();
    }
}
