 using UnityEngine;
 
namespace OSK.AI.TreeBehavior
{
    public class Node_RandomSelector : Node
    {
        private bool _pickOnce;
        private Node _chosen;

        public Node_RandomSelector(bool pickOnce = false, params Node[] nodes)
        {
            foreach (var n in nodes) Attach(n);
            _pickOnce = pickOnce;
        }

        public override NodeState Evaluate()
        {
            if (_chosen == null || !_pickOnce)
            {
                int index = Random.Range(0, Children.Count);
                _chosen = Children[index];
            }

            return State = _chosen.Evaluate();
        }
    }
}