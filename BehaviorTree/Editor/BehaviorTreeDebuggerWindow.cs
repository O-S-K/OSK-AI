#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using OSK.AI.TreeBehavior;

namespace OSK.AI.EditorTools
{
    public class BehaviorTreeDebuggerWindow : EditorWindow
    {
        private BehaviorTree _activeTree;
        private Vector2 _scroll;
        private GUIStyle _successStyle;
        private GUIStyle _failureStyle;
        private GUIStyle _runningStyle;
        private GUIStyle _defaultStyle;
        private double _lastRepaint;

        [MenuItem("OSK-AI/AI/Behavior Tree Debugger")]
        public static void Open()
        {
            GetWindow<BehaviorTreeDebuggerWindow>("Behavior Tree Debugger");
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                _successStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } };
                _failureStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };
                _runningStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } };
                _defaultStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.white } };
            }
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                _activeTree = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🧠 Behavior Tree Debugger", EditorStyles.boldLabel);
            _activeTree = (BehaviorTree)EditorGUILayout.ObjectField("Active Tree", _activeTree, typeof(BehaviorTree), true);

            if (_activeTree == null)
            {
                EditorGUILayout.HelpBox("Assign a BehaviorTree (EnemyAI, NPC, etc.) to visualize.", MessageType.Info);
                return;
            }

            Node root = GetRoot(_activeTree);
            if (root == null)
            {
                EditorGUILayout.HelpBox("Root node not initialized (did you run Play mode?).", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawNodeRecursive(root, 0);
            EditorGUILayout.EndScrollView();

            // auto refresh every 0.25s
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - _lastRepaint > 0.25)
            {
                _lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private Node GetRoot(BehaviorTree tree)
        {
            var field = typeof(BehaviorTree).GetField("root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(tree) as Node;
        }

        private void DrawNodeRecursive(Node node, int indent)
        {
            if (node == null) return;

            GUIStyle style = _defaultStyle;
            switch (node.State)
            {
                case NodeState.SUCCESS: style = _successStyle; break;
                case NodeState.FAILURE: style = _failureStyle; break;
                case NodeState.RUNNING: style = _runningStyle; break;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 20);
            EditorGUILayout.LabelField($"• {node.GetType().Name}  [{node.State}]", style);
            EditorGUILayout.EndHorizontal();

            var childrenField = typeof(Node).GetField("Children", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var children = childrenField?.GetValue(node) as List<Node>;
            if (children != null)
            {
                foreach (var child in children)
                    DrawNodeRecursive(child, indent + 1);
            }
        }
    }
}
#endif
