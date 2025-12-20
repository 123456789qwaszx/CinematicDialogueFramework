#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SituationSpecSO))]
public class SituationSpecSOEditor : Editor
{
    private SerializedProperty _situationKey;
    private SerializedProperty _nodes;

    private void OnEnable()
    {
        _situationKey = serializedObject.FindProperty("situationKey");
        _nodes = serializedObject.FindProperty("nodes");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_situationKey);

        if (string.IsNullOrWhiteSpace(_situationKey.stringValue))
            EditorGUILayout.HelpBox("situationKey is empty. This will break route -> situation resolution.", MessageType.Warning);

        EditorGUILayout.Space(6);
        DrawToolbar();

        EditorGUILayout.Space(6);
        EditorGUILayout.PropertyField(_nodes, includeChildren: true);

        EditorGUILayout.Space(6);
        DrawValidationPanel();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Node"))
                AddNode(emptyOnly: true);

            if (GUILayout.Button("Insert Daily Talk Node"))
                AddNode(emptyOnly: false, insertShowLine: true);

            if (GUILayout.Button("Duplicate Last Node"))
                DuplicateLastNode();
        }
    }

    private void DrawValidationPanel()
    {
        if (_nodes == null) return;

        if (_nodes.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No nodes in this situation.", MessageType.Warning);
            if (GUILayout.Button("Quick Fix: Add 1 Daily Talk Node"))
                AddNode(emptyOnly: false, insertShowLine: true);
            return;
        }

        // 간단 검증: ShowLine이 하나도 없는 노드가 있는지 검사
        int missingLineCount = CountNodesMissingShowLine();
        if (missingLineCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{missingLineCount} node(s) have no ShowLine command. ViewModel may appear empty.",
                MessageType.Warning);

            if (GUILayout.Button("Quick Fix: Add ShowLine to missing nodes"))
                AddShowLineToAllMissingNodes();
        }
    }

    private void AddNode(bool emptyOnly, bool insertShowLine = false)
    {
        if (_nodes == null) return;

        Undo.RecordObject(target, "Add Dialogue Node");
        int index = _nodes.arraySize;
        _nodes.InsertArrayElementAtIndex(index);

        var nodeProp = _nodes.GetArrayElementAtIndex(index);

        // 기본적으로는 "빈 노드"를 만들고,
        // 옵션으로 ShowLine 하나를 넣어준다.
        if (!emptyOnly && insertShowLine)
            EnsureShowLineCommand(nodeProp);

        EditorUtility.SetDirty(target);
    }

    private void DuplicateLastNode()
    {
        if (_nodes == null || _nodes.arraySize == 0) return;

        Undo.RecordObject(target, "Duplicate Dialogue Node");

        int last = _nodes.arraySize - 1;

        // last 요소를 복제해서 last 위치에 삽입 → arraySize가 1 늘고, 복제본이 last에 들어감
        _nodes.InsertArrayElementAtIndex(last);

        EditorUtility.SetDirty(target);
    }

    private int CountNodesMissingShowLine()
    {
        int missing = 0;

        for (int i = 0; i < _nodes.arraySize; i++)
        {
            var nodeProp = _nodes.GetArrayElementAtIndex(i);
            if (!HasShowLine(nodeProp))
                missing++;
        }

        return missing;
    }

    private void AddShowLineToAllMissingNodes()
    {
        Undo.RecordObject(target, "Add ShowLine Commands");

        for (int i = 0; i < _nodes.arraySize; i++)
        {
            var nodeProp = _nodes.GetArrayElementAtIndex(i);
            if (!HasShowLine(nodeProp))
                EnsureShowLineCommand(nodeProp);
        }

        EditorUtility.SetDirty(target);
    }

    private bool HasShowLine(SerializedProperty nodeProp)
    {
        var commands = nodeProp.FindPropertyRelative("commands");
        if (commands == null || !commands.isArray) return false;

        for (int i = 0; i < commands.arraySize; i++)
        {
            var cmd = commands.GetArrayElementAtIndex(i);
            var kind = cmd.FindPropertyRelative("kind");
            if (kind != null && kind.propertyType == SerializedPropertyType.Enum)
            {
                // enum 이름이 ShowLine이면 true
                if (kind.enumDisplayNames != null &&
                    kind.enumValueIndex >= 0 &&
                    kind.enumValueIndex < kind.enumDisplayNames.Length &&
                    kind.enumDisplayNames[kind.enumValueIndex] == "ShowLine")
                    return true;
            }
        }
        return false;
    }

    private void EnsureShowLineCommand(SerializedProperty nodeProp)
    {
        var commands = nodeProp.FindPropertyRelative("commands");
        if (commands == null || !commands.isArray) return;

        int idx = commands.arraySize;
        commands.InsertArrayElementAtIndex(idx);

        var cmd = commands.GetArrayElementAtIndex(idx);
        var kind = cmd.FindPropertyRelative("kind");
        if (kind != null && kind.propertyType == SerializedPropertyType.Enum)
        {
            // enum에 "ShowLine"이 있으면 그 인덱스로 세팅
            int showLineIndex = FindEnumIndex(kind, "ShowLine");
            if (showLineIndex >= 0)
                kind.enumValueIndex = showLineIndex;
        }

        // line 기본값 세팅(필드명이 다르면 수정)
        var line = cmd.FindPropertyRelative("line");
        if (line != null)
        {
            var speakerId = line.FindPropertyRelative("speakerId");
            var text = line.FindPropertyRelative("text");
            if (speakerId != null) speakerId.stringValue = "System";
            if (text != null) text.stringValue = "(write dialogue...)";
        }
    }

    private int FindEnumIndex(SerializedProperty enumProp, string enumName)
    {
        var names = enumProp.enumNames;
        if (names == null) return -1;

        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == enumName)
                return i;
        }
        return -1;
    }
}
#endif
