#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class SequenceStepImportWindow : EditorWindow
{
    private SequenceSpecSO _source;
    private SequenceSpecSO _target;

    private int _sourceNodeIndex;
    private Vector2 _sourceScroll;
    private bool[] _stepSelection;

    private int _targetNodeIndex;

    [MenuItem("Tools/Sequence/Sequence Step Importer")]
    public static void Open()
    {
        GetWindow<SequenceStepImportWindow>("Sequence Step Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source / Target", EditorStyles.boldLabel);

        _source = (SequenceSpecSO)EditorGUILayout.ObjectField(
            "Source Sequence", _source, typeof(SequenceSpecSO), false);
        _target = (SequenceSpecSO)EditorGUILayout.ObjectField(
            "Target Sequence", _target, typeof(SequenceSpecSO), false);

        if (_source == null || _target == null)
        {
            EditorGUILayout.HelpBox("Assign both Source and Target Sequence assets.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        DrawSourceSection();
        EditorGUILayout.Space();
        DrawTargetSection();
        EditorGUILayout.Space();
        DrawImportButtons();
    }

    private void DrawSourceSection()
    {
        EditorGUILayout.LabelField("Source Steps", EditorStyles.boldLabel);

        if (_source.nodes == null || _source.nodes.Count == 0)
        {
            EditorGUILayout.HelpBox("Source has no nodes.", MessageType.Info);
            return;
        }

        string[] nodeNames = _source.nodes.ConvertAll(n => n.editorName).ToArray();
        _sourceNodeIndex = Mathf.Clamp(_sourceNodeIndex, 0, nodeNames.Length - 1);
        _sourceNodeIndex = EditorGUILayout.Popup("Source Node", _sourceNodeIndex, nodeNames);

        NodeSpec node = _source.nodes[_sourceNodeIndex];
        if (node == null || node.steps == null || node.steps.Count == 0)
        {
            EditorGUILayout.HelpBox("Selected node has no steps.", MessageType.Info);
            return;
        }

        if (_stepSelection == null || _stepSelection.Length != node.steps.Count)
            _stepSelection = new bool[node.steps.Count];

        _sourceScroll = EditorGUILayout.BeginScrollView(_sourceScroll, GUILayout.Height(200));
        for (int i = 0; i < node.steps.Count; i++)
        {
            StepSpec step = node.steps[i];
            if (step == null) continue;

            _stepSelection[i] = EditorGUILayout.ToggleLeft(
                $"{i:00}: {step.editorName}",
                _stepSelection[i]);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        if (_target.nodes == null || _target.nodes.Count == 0)
        {
            EditorGUILayout.HelpBox("Target has no nodes.", MessageType.Info);
            return;
        }

        string[] nodeNames = _target.nodes.ConvertAll(n => n.editorName).ToArray();
        _targetNodeIndex = Mathf.Clamp(_targetNodeIndex, 0, nodeNames.Length - 1);
        _targetNodeIndex = EditorGUILayout.Popup("Target Node", _targetNodeIndex, nodeNames);
    }

    private void DrawImportButtons()
    {
        using (new EditorGUI.DisabledScope(_source == null || _target == null))
        {
            if (GUILayout.Button("Append Selected Steps to Target Node"))
            {
                AppendSelectedSteps();
            }
        }
    }

    private void AppendSelectedSteps()
    {
        NodeSpec srcNode = _source.nodes[_sourceNodeIndex];
        NodeSpec dstNode = _target.nodes[_targetNodeIndex];

        if (srcNode == null || dstNode == null || srcNode.steps == null)
            return;

        if (dstNode.steps == null)
            dstNode.steps = new List<StepSpec>();

        Undo.RegisterCompleteObjectUndo(_target, "Import Steps");

        for (int i = 0; i < srcNode.steps.Count; i++)
        {
            if (_stepSelection == null || i >= _stepSelection.Length || !_stepSelection[i])
                continue;

            StepSpec srcStep = srcNode.steps[i];
            if (srcStep == null) continue;

            var clonedStep = new StepSpec
            {
                editorName = srcStep.editorName + " (imported)",
                gate       = srcStep.gate,
                commands   = StepPaletteCloneUtil.CloneCommands(srcStep.commands)
            };

            dstNode.steps.Add(clonedStep);
        }

        EditorUtility.SetDirty(_target);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported selected steps into target node.");
    }
}
#endif