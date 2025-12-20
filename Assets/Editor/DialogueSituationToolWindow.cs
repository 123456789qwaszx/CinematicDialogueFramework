#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

public sealed class DialogueSituationToolWindow : EditorWindow
{
    [MenuItem("Tools/Dialogue/Situation Tool")]
    public static void Open()
    {
        var w = GetWindow<DialogueSituationToolWindow>();
        w.titleContent = new GUIContent("Dialogue Situation Tool");
        w.Show();
    }

    [SerializeField] private SituationSpecSO targetSituation;

    private SerializedObject _so;
    private SerializedProperty _situationKeyProp;
    private SerializedProperty _nodesProp;

    private ReorderableList _nodesList;
    private ReorderableList _stepsList;
    private ReorderableList _commandsList;

    private int _selectedNode = -1;
    private int _selectedStep = -1;
    private SearchField _searchField;
    // UI
    private Vector2 _rightScroll;

    // Search
    private string _search = "";

    // ------------------------------
    // Unity callbacks
    // ------------------------------
    private void OnEnable()
    {
        wantsMouseMove = true;
        _searchField = new SearchField();
        RebuildIfNeeded(force: true);
    }

    private void OnSelectionChange()
    {
        // 선택한 SO가 SituationSpecSO면 자동으로 연결
        if (Selection.activeObject is SituationSpecSO so)
        {
            targetSituation = so;
            RebuildIfNeeded(force: true);
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (targetSituation == null)
        {
            EditorGUILayout.HelpBox("Assign a SituationSpecSO (drag & drop) or select one in Project.", MessageType.Info);
            return;
        }

        RebuildIfNeeded(force: false);

        if (_so == null)
        {
            EditorGUILayout.HelpBox("Failed to create SerializedObject.", MessageType.Error);
            return;
        }

        _so.Update();

        DrawHeader();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawNodesPanel();
            DrawRightPanel();
        }

        _so.ApplyModifiedProperties();
    }

    // ------------------------------
    // UI
    // ------------------------------
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            targetSituation = (SituationSpecSO)EditorGUILayout.ObjectField(targetSituation, typeof(SituationSpecSO), false);
            if (EditorGUI.EndChangeCheck())
                RebuildIfNeeded(force: true);

            GUILayout.FlexibleSpace();

            // ✅ 안전한 Toolbar SearchField
            _search = _searchField != null ? _searchField.OnToolbarGUI(_search ?? "") : (_search ?? "");

            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(50)) && targetSituation != null)
                EditorGUIUtility.PingObject(targetSituation);

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)) && targetSituation != null)
            {
                EditorUtility.SetDirty(targetSituation);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Situation", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_situationKeyProp, new GUIContent("situationKey"));

            int nodeCount = _nodesProp != null ? _nodesProp.arraySize : 0;
            EditorGUILayout.LabelField($"Nodes: {nodeCount}");

            if (string.IsNullOrWhiteSpace(_situationKeyProp.stringValue))
                EditorGUILayout.HelpBox("situationKey is empty. Route resolution will fail.", MessageType.Warning);

            if (nodeCount == 0)
                EditorGUILayout.HelpBox("No nodes. Use 'Add Node' or 'Insert Talk Node'.", MessageType.Warning);
        }
    }

    private void DrawNodesPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(320)))
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);

                if (_nodesList != null)
                    _nodesList.DoLayoutList();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Node"))
                        AddNode();

                    if (GUILayout.Button("Insert Talk Node"))
                        InsertTalkNode();

                    if (GUILayout.Button("Duplicate Node"))
                        DuplicateSelectedNode();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Delete Node"))
                        DeleteSelectedNode();

                    if (GUILayout.Button("Move Up"))
                        MoveSelectedNode(-1);

                    if (GUILayout.Button("Move Down"))
                        MoveSelectedNode(+1);
                }
            }

            DrawMiniDiagnostics();
        }
    }

    private void DrawMiniDiagnostics()
    {
        if (_nodesProp == null) return;

        int nodeCount = _nodesProp.arraySize;
        if (nodeCount <= 0) return;

        // 아주 가벼운 진단: ShowLine이 전혀 없는 노드 수 표시
        int missingLineNodes = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            var node = _nodesProp.GetArrayElementAtIndex(i);
            if (!NodeHasAnyShowLine(node))
                missingLineNodes++;
        }

        if (missingLineNodes > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox($"{missingLineNodes} node(s) have no ShowLine command. ViewModel may be empty.", MessageType.Warning);

            if (GUILayout.Button("Quick Fix: Add ShowLine to empty nodes"))
                QuickFix_AddShowLineToEmptyNodes();
        }
    }

    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (_nodesProp == null || _nodesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Create at least one node.", MessageType.Info);
                return;
            }

            if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize)
            {
                EditorGUILayout.HelpBox("Select a node to edit.", MessageType.Info);
                return;
            }

            var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
            var stepsProp = nodeProp.FindPropertyRelative("steps");

            if (stepsProp == null || !stepsProp.isArray)
            {
                EditorGUILayout.HelpBox("DialogueNodeSpec must have List<DialogueStepSpec> steps.", MessageType.Error);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_rightScroll))
            {
                _rightScroll = scroll.scrollPosition;

                DrawNodeEditor(nodeProp, stepsProp);
            }
        }
    }

    private void DrawNodeEditor(SerializedProperty nodeProp, SerializedProperty stepsProp)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField($"Node {_selectedNode}", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Step", GUILayout.Width(80)))
                    AddStep(stepsProp);

                if (GUILayout.Button("Insert Talk Step", GUILayout.Width(140)))
                    InsertTalkStep(stepsProp);

                if (GUILayout.Button("Duplicate Step", GUILayout.Width(140)))
                    DuplicateSelectedStep(stepsProp);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Delete Step"))
                    DeleteSelectedStep(stepsProp);

                if (GUILayout.Button("Step Up"))
                    MoveSelectedStep(stepsProp, -1);

                if (GUILayout.Button("Step Down"))
                    MoveSelectedStep(stepsProp, +1);
            }
        }

        EditorGUILayout.Space(6);

        // Steps list
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Steps", EditorStyles.boldLabel);

            EnsureStepsList(nodeProp, stepsProp);
            _stepsList?.DoLayoutList();
        }

        EditorGUILayout.Space(6);

        // Selected step detail
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize)
        {
            EditorGUILayout.HelpBox("Select a step to edit Gate + Commands.", MessageType.Info);
            return;
        }

        var stepProp = stepsProp.GetArrayElementAtIndex(_selectedStep);
        DrawStepDetail(stepProp);
    }

    private void DrawStepDetail(SerializedProperty stepProp)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField($"Step {_selectedStep}", EditorStyles.boldLabel);

            // Gate
            var gateProp = stepProp.FindPropertyRelative("gate");
            if (gateProp != null)
            {
                EditorGUILayout.PropertyField(gateProp, new GUIContent("Gate (after this step)"), includeChildren: true);

                // Gate가 default여도 런타임에서 Input으로 보정되지만, 제작자가 헷갈릴 수 있어서 안내
                if (IsStructDefault(gateProp))
                    EditorGUILayout.HelpBox("Gate looks default. Runtime planner will treat default as Input (recommended to set explicitly for readability).", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("DialogueStepSpec must have GateToken gate.", MessageType.Error);
            }

            EditorGUILayout.Space(6);

            // Commands
            var commandsProp = stepProp.FindPropertyRelative("commands");
            if (commandsProp == null || !commandsProp.isArray)
            {
                EditorGUILayout.HelpBox("DialogueStepSpec must have List<NodeCommandSpec> commands.", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Command", GUILayout.Width(120)))
                    AddCommand(commandsProp);

                if (GUILayout.Button("Insert ShowLine", GUILayout.Width(140)))
                    InsertShowLine(commandsProp, speaker: "System", text: "(write dialogue...)");

                if (GUILayout.Button("Duplicate Command", GUILayout.Width(160)))
                    DuplicateSelectedCommand(commandsProp);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Delete Command"))
                    DeleteSelectedCommand(commandsProp);

                if (GUILayout.Button("Cmd Up"))
                    MoveSelectedCommand(commandsProp, -1);

                if (GUILayout.Button("Cmd Down"))
                    MoveSelectedCommand(commandsProp, +1);
            }

            EditorGUILayout.Space(6);

            EnsureCommandsList(stepProp, commandsProp);
            _commandsList?.DoLayoutList();
        }
    }

    // ------------------------------
    // Rebuild / Lists
    // ------------------------------
    private void RebuildIfNeeded(bool force)
    {
        if (!force && _so != null && _so.targetObject == targetSituation) return;

        if (targetSituation == null)
        {
            _so = null;
            _nodesList = null;
            _stepsList = null;
            _commandsList = null;
            _selectedNode = -1;
            _selectedStep = -1;
            return;
        }

        _so = new SerializedObject(targetSituation);
        _situationKeyProp = _so.FindProperty("situationKey");
        _nodesProp = _so.FindProperty("nodes");

        _selectedNode = Mathf.Clamp(_selectedNode, 0, (_nodesProp?.arraySize ?? 0) - 1);
        _selectedStep = -1;

        BuildNodesList();
        _stepsList = null;
        _commandsList = null;
    }

    private void BuildNodesList()
    {
        if (_nodesProp == null) return;

        _nodesList = new ReorderableList(_so, _nodesProp, draggable: true, displayHeader: true, displayAddButton: false, displayRemoveButton: false);
        _nodesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Nodes (Reorderable)");
        _nodesList.onSelectCallback = list =>
        {
            _selectedNode = list.index;
            _selectedStep = -1;
            _stepsList = null;
            _commandsList = null;
            Repaint();
        };

        _nodesList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(index);

            string label = $"Node {index}";
            if (!string.IsNullOrWhiteSpace(_search))
            {
                bool hit = NodeMatchesSearch(nodeProp, _search);
                if (!hit)
                    GUI.enabled = false;
                else
                    label += "  (match)";
            }

            EditorGUI.LabelField(rect, label);

            GUI.enabled = true;
        };
    }

    private void EnsureStepsList(SerializedProperty nodeProp, SerializedProperty stepsProp)
    {
        if (_stepsList != null && _stepsList.serializedProperty == stepsProp) return;

        _selectedStep = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);

        _stepsList = new ReorderableList(_so, stepsProp, draggable: true, displayHeader: true, displayAddButton: false, displayRemoveButton: false);
        _stepsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Steps (Reorderable)");

        _stepsList.onSelectCallback = list =>
        {
            _selectedStep = list.index;
            _commandsList = null;
            Repaint();
        };

        _stepsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var stepProp = stepsProp.GetArrayElementAtIndex(index);
            var gateProp = stepProp.FindPropertyRelative("gate");
            var commandsProp = stepProp.FindPropertyRelative("commands");

            string gateSummary = gateProp != null ? SummarizeGate(gateProp) : "(no gate)";
            int cmdCount = commandsProp != null && commandsProp.isArray ? commandsProp.arraySize : 0;

            EditorGUI.LabelField(rect, $"Step {index}  | cmds={cmdCount}  | gate={gateSummary}");
        };
    }

    private void EnsureCommandsList(SerializedProperty stepProp, SerializedProperty commandsProp)
    {
        if (_commandsList != null && _commandsList.serializedProperty == commandsProp) return;

        _commandsList = new ReorderableList(_so, commandsProp, draggable: true, displayHeader: true, displayAddButton: false, displayRemoveButton: false);

        _commandsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Commands (Reorderable)");

        _commandsList.onSelectCallback = list =>
        {
            // noop: index는 list.index로 접근
            Repaint();
        };

        _commandsList.elementHeightCallback = (index) =>
        {
            if (index < 0 || index >= commandsProp.arraySize)
                return EditorGUIUtility.singleLineHeight;

            var element = commandsProp.GetArrayElementAtIndex(index);

            // includeChildren: true 가 핵심 (foldout 펼치면 높이가 커짐)
            float h = EditorGUI.GetPropertyHeight(element, includeChildren: true);

            // 위/아래 여백
            return h + 6f;
        };

        _commandsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index < 0 || index >= commandsProp.arraySize) return;

            var element = commandsProp.GetArrayElementAtIndex(index);

            // 약간 패딩
            rect.y += 2f;
            rect.height -= 2f;

            // ✅ label에 요약을 넣어서 foldout도 그대로 유지
            var label = new GUIContent(SummarizeCommand(element, index));
            EditorGUI.PropertyField(rect, element, label, includeChildren: true);
        };
    }

    // ------------------------------
    // Node operations
    // ------------------------------
    private void AddNode()
    {
        if (_nodesProp == null) return;

        Undo.RecordObject(targetSituation, "Add Node");
        _nodesProp.arraySize++;
        _selectedNode = _nodesProp.arraySize - 1;
        _selectedStep = -1;

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void InsertTalkNode()
    {
        AddNode();

        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return;
        var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
        var stepsProp = nodeProp.FindPropertyRelative("steps");
        if (stepsProp == null || !stepsProp.isArray) return;

        Undo.RecordObject(targetSituation, "Insert Talk Node");

        stepsProp.arraySize = 1;
        _selectedStep = 0;

        var step0 = stepsProp.GetArrayElementAtIndex(0);
        var cmds = step0.FindPropertyRelative("commands");
        if (cmds != null && cmds.isArray)
        {
            cmds.arraySize = 0;
            InsertShowLine(cmds, speaker: "System", text: "(write dialogue...)");
        }

        // gate는 runtime이 default를 Input으로 보정하므로 여기선 강제하지 않음

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void DuplicateSelectedNode()
    {
        if (_nodesProp == null) return;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Duplicate Node");

        // InsertArrayElementAtIndex는 대부분 해당 요소 복제
        _nodesProp.InsertArrayElementAtIndex(_selectedNode);

        // 복제본이 선택 인덱스에 들어옴 → 선택 유지
        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void DeleteSelectedNode()
    {
        if (_nodesProp == null) return;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return;

        if (!EditorUtility.DisplayDialog("Delete Node", $"Delete Node {_selectedNode}?", "Delete", "Cancel"))
            return;

        Undo.RecordObject(targetSituation, "Delete Node");
        _nodesProp.DeleteArrayElementAtIndex(_selectedNode);

        _selectedNode = Mathf.Clamp(_selectedNode, 0, _nodesProp.arraySize - 1);
        _selectedStep = -1;

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void MoveSelectedNode(int dir)
    {
        if (_nodesProp == null) return;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return;

        int dst = _selectedNode + dir;
        if (dst < 0 || dst >= _nodesProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Move Node");
        _nodesProp.MoveArrayElement(_selectedNode, dst);
        _selectedNode = dst;

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    // ------------------------------
    // Step operations
    // ------------------------------
    private void AddStep(SerializedProperty stepsProp)
    {
        if (stepsProp == null || !stepsProp.isArray) return;

        Undo.RecordObject(targetSituation, "Add Step");
        stepsProp.arraySize++;
        _selectedStep = stepsProp.arraySize - 1;

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void InsertTalkStep(SerializedProperty stepsProp)
    {
        AddStep(stepsProp);

        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return;

        var step = stepsProp.GetArrayElementAtIndex(_selectedStep);
        var cmds = step.FindPropertyRelative("commands");
        if (cmds != null && cmds.isArray)
        {
            cmds.arraySize = 0;
            InsertShowLine(cmds, speaker: "System", text: "(write dialogue...)");
        }

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void DuplicateSelectedStep(SerializedProperty stepsProp)
    {
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Duplicate Step");
        stepsProp.InsertArrayElementAtIndex(_selectedStep);
        EditorUtility.SetDirty(targetSituation);

        _stepsList = null;
        _commandsList = null;
    }

    private void DeleteSelectedStep(SerializedProperty stepsProp)
    {
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return;

        if (!EditorUtility.DisplayDialog("Delete Step", $"Delete Step {_selectedStep}?", "Delete", "Cancel"))
            return;

        Undo.RecordObject(targetSituation, "Delete Step");
        stepsProp.DeleteArrayElementAtIndex(_selectedStep);
        _selectedStep = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    private void MoveSelectedStep(SerializedProperty stepsProp, int dir)
    {
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return;

        int dst = _selectedStep + dir;
        if (dst < 0 || dst >= stepsProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Move Step");
        stepsProp.MoveArrayElement(_selectedStep, dst);
        _selectedStep = dst;

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    // ------------------------------
    // Command operations
    // ------------------------------
    private void AddCommand(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        Undo.RecordObject(targetSituation, "Add Command");
        commandsProp.arraySize++;

        EditorUtility.SetDirty(targetSituation);
        _commandsList = null;
    }

    private void InsertShowLine(SerializedProperty commandsProp, string speaker, string text)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        Undo.RecordObject(targetSituation, "Insert ShowLine");

        int idx = commandsProp.arraySize;
        commandsProp.arraySize++;

        var cmd = commandsProp.GetArrayElementAtIndex(idx);

        // kind = ShowLine (enumName 기반으로 찾음)
        var kind = cmd.FindPropertyRelative("kind");
        if (kind != null && kind.propertyType == SerializedPropertyType.Enum)
        {
            int showLineIndex = FindEnumIndex(kind, "ShowLine");
            if (showLineIndex >= 0)
                kind.enumValueIndex = showLineIndex;
        }

        // line.speakerId / line.text 기본값 세팅 (필드명이 다르면 너 코드에 맞게 수정)
        var line = cmd.FindPropertyRelative("line");
        if (line != null)
        {
            var speakerId = line.FindPropertyRelative("speakerId");
            var body = line.FindPropertyRelative("text");
            if (speakerId != null) speakerId.stringValue = speaker ?? "System";
            if (body != null) body.stringValue = text ?? "";
        }

        EditorUtility.SetDirty(targetSituation);
        _commandsList = null;
    }

    private void DuplicateSelectedCommand(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;
        if (_commandsList == null) return;

        int idx = _commandsList.index;
        if (idx < 0 || idx >= commandsProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Duplicate Command");
        commandsProp.InsertArrayElementAtIndex(idx);
        EditorUtility.SetDirty(targetSituation);

        _commandsList = null;
    }

    private void DeleteSelectedCommand(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;
        if (_commandsList == null) return;

        int idx = _commandsList.index;
        if (idx < 0 || idx >= commandsProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Delete Command");
        commandsProp.DeleteArrayElementAtIndex(idx);
        EditorUtility.SetDirty(targetSituation);

        _commandsList = null;
    }

    private void MoveSelectedCommand(SerializedProperty commandsProp, int dir)
    {
        if (commandsProp == null || !commandsProp.isArray) return;
        if (_commandsList == null) return;

        int idx = _commandsList.index;
        if (idx < 0 || idx >= commandsProp.arraySize) return;

        int dst = idx + dir;
        if (dst < 0 || dst >= commandsProp.arraySize) return;

        Undo.RecordObject(targetSituation, "Move Command");
        commandsProp.MoveArrayElement(idx, dst);
        _commandsList.index = dst;

        EditorUtility.SetDirty(targetSituation);
        _commandsList = null;
    }

    // ------------------------------
    // Quick Fix
    // ------------------------------
    private void QuickFix_AddShowLineToEmptyNodes()
    {
        if (_nodesProp == null) return;

        Undo.RecordObject(targetSituation, "Quick Fix: Add ShowLine");

        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var node = _nodesProp.GetArrayElementAtIndex(i);
            if (NodeHasAnyShowLine(node)) continue;

            var steps = node.FindPropertyRelative("steps");
            if (steps == null || !steps.isArray) continue;

            if (steps.arraySize == 0)
                steps.arraySize = 1;

            var step0 = steps.GetArrayElementAtIndex(0);
            var cmds = step0.FindPropertyRelative("commands");
            if (cmds != null && cmds.isArray)
                InsertShowLine(cmds, "System", "(write dialogue...)");
        }

        EditorUtility.SetDirty(targetSituation);
        _stepsList = null;
        _commandsList = null;
    }

    // ------------------------------
    // Search helpers
    // ------------------------------
    private bool NodeMatchesSearch(SerializedProperty nodeProp, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        query = query.Trim();

        var stepsProp = nodeProp.FindPropertyRelative("steps");
        if (stepsProp == null || !stepsProp.isArray) return false;

        for (int si = 0; si < stepsProp.arraySize; si++)
        {
            var step = stepsProp.GetArrayElementAtIndex(si);
            var commands = step.FindPropertyRelative("commands");
            if (commands == null || !commands.isArray) continue;

            for (int ci = 0; ci < commands.arraySize; ci++)
            {
                var cmd = commands.GetArrayElementAtIndex(ci);
                string summary = SummarizeCommand(cmd, ci);
                if (!string.IsNullOrEmpty(summary) && summary.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        return false;
    }

    // ------------------------------
    // Summaries
    // ------------------------------
    private string SummarizeGate(SerializedProperty gateProp)
    {
        // GateToken 내부 구조를 몰라도, 기본은 타입 이름/기본 상태만 알려준다.
        if (gateProp == null) return "(null)";

        // 흔히 gate.type enum이 있을 가능성이 높으니 있으면 표시
        var typeProp = gateProp.FindPropertyRelative("type");
        if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
        {
            string t = typeProp.enumDisplayNames[typeProp.enumValueIndex];
            if (t == "Delay")
            {
                var sec = gateProp.FindPropertyRelative("seconds");
                if (sec != null && sec.propertyType == SerializedPropertyType.Float)
                    return $"Delay({sec.floatValue:0.###}s)";
            }
            if (t == "Signal")
            {
                var key = gateProp.FindPropertyRelative("signalKey");
                if (key != null && key.propertyType == SerializedPropertyType.String)
                    return $"Signal('{key.stringValue}')";
            }
            return t;
        }

        return IsStructDefault(gateProp) ? "Default(->Input)" : gateProp.type;
    }

    private string SummarizeCommand(SerializedProperty cmdProp, int index)
    {
        if (cmdProp == null) return $"#{index} (null)";

        string kindName = "(kind?)";
        var kind = cmdProp.FindPropertyRelative("kind");
        if (kind != null && kind.propertyType == SerializedPropertyType.Enum)
        {
            kindName = kind.enumDisplayNames[kind.enumValueIndex];
        }

        // ShowLine이면 speaker/text 요약
        if (string.Equals(kindName, "ShowLine", StringComparison.OrdinalIgnoreCase))
        {
            var line = cmdProp.FindPropertyRelative("line");
            if (line != null)
            {
                string speaker = line.FindPropertyRelative("speakerId")?.stringValue ?? "";
                string text = line.FindPropertyRelative("text")?.stringValue ?? "";
                text = Short(text, 42);
                return $"#{index} ShowLine  [{speaker}] {text}";
            }
        }

        return $"#{index} {kindName}";
    }

    private static string Short(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    // ------------------------------
    // ShowLine diagnostics
    // ------------------------------
    private bool NodeHasAnyShowLine(SerializedProperty nodeProp)
    {
        var stepsProp = nodeProp.FindPropertyRelative("steps");
        if (stepsProp == null || !stepsProp.isArray) return false;

        for (int i = 0; i < stepsProp.arraySize; i++)
        {
            var step = stepsProp.GetArrayElementAtIndex(i);
            var commands = step.FindPropertyRelative("commands");
            if (commands == null || !commands.isArray) continue;

            for (int j = 0; j < commands.arraySize; j++)
            {
                var cmd = commands.GetArrayElementAtIndex(j);
                var kind = cmd.FindPropertyRelative("kind");
                if (kind != null && kind.propertyType == SerializedPropertyType.Enum)
                {
                    string k = kind.enumNames[kind.enumValueIndex];
                    if (k == "ShowLine") return true;
                }
            }
        }

        return false;
    }

    // ------------------------------
    // Misc helpers
    // ------------------------------
    private static int FindEnumIndex(SerializedProperty enumProp, string enumName)
    {
        var names = enumProp.enumNames;
        if (names == null) return -1;

        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == enumName) return i;
        }
        return -1;
    }

    private static bool IsStructDefault(SerializedProperty prop)
    {
#if UNITY_2021_2_OR_NEWER
        try
        {
            // boxedValue는 struct default 판단에 꽤 유용 (Unity 버전 의존)
            object v = prop.boxedValue;
            if (v == null) return true;
            var t = v.GetType();
            object def = Activator.CreateInstance(t);
            return v.Equals(def);
        }
        catch { return false; }
#else
        return false;
#endif
    }
}
#endif
