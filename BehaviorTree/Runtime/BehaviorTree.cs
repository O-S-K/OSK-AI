using UnityEngine;

namespace OSK.AITreeBehavior
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
