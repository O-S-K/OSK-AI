using System.Collections.Generic;

namespace OSK.AI.TreeBehavior
{
    public enum NodeState
    {
        RUNNING,
        SUCCESS,
        FAILURE
    }

    public abstract class Node
    {
        public NodeState State { get; protected set; } = NodeState.RUNNING;
        public Node Parent { get; private set; }
        protected List<Node> Children = new List<Node>();

        public void Attach(Node child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public abstract NodeState Evaluate();
    }
}
