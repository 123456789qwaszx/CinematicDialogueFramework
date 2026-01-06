#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public sealed class SequenceSpecEditorWindow : EditorWindow
{
    [MenuItem("Tools/Sequence/Sequence Editor")]
    public static void Open()
    {
        var w = GetWindow<SequenceSpecEditorWindow>();
        w.titleContent = new GUIContent("SequenceSpec Editor");
        w.Show();
    }

    [SerializeField] private SequenceSpecSO targetSequence;

    private SerializedObject _so;
    private SerializedProperty _sequenceKeyProp;
    private SerializedProperty _nodesProp;

    private ReorderableList _nodesList;
    private ReorderableList _stepsList;
    private ReorderableList _commandsList;

    private int _selectedNode = -1;
    private int _selectedStep = -1;
    private SearchField _searchField;
    private Vector2 _rightScroll;

    private string _search = "";

    private bool _isDraggingSteps;

    private string _stepsPropPath;
    private string _commandsPropPath;

    private int _pendingCommandIndex = -1; // +Command 후 선택 유지용(선택사항이지만 같이 넣자)

    private readonly HashSet<string> _autoExpandedOnce = new();

    private float _nodesW;
    private float _stepsW;


    // ------------------------------
    // Polymorphic Command Support
    // ------------------------------
    private static List<Type> _cachedCommandTypes;

    // ✅ 네 프로젝트의 ShowLine Spec 타입 이름(클래스명)과 맞춰줘
    private const string DefaultShowLineTypeName = "ShowLineCommandSpec";

    private static void CacheCommandTypes()
    {
        if (_cachedCommandTypes != null) return;

        var types = TypeCache.GetTypesDerivedFrom<CommandSpecBase>();
        _cachedCommandTypes = types
            .Where(t => t != null && !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void DelayModify(string undoLabel, Action<SerializedObject> action)
    {
        EditorApplication.delayCall += () =>
        {
            if (targetSequence == null) return;

            Undo.RecordObject(targetSequence, undoLabel);

            var so = new SerializedObject(targetSequence);
            so.Update();

            action?.Invoke(so);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetSequence);

            RebuildIfNeeded(force: true);
            Repaint();
        };
    }

    // ------------------------------
    // Unity callbacks
    // ------------------------------
    private void OnEnable()
    {
        minSize = new Vector2(720f, 380f); // 원하는 최소 크기
        wantsMouseMove = true;
        _searchField = new SearchField();
        CacheCommandTypes();
        RebuildIfNeeded(force: true);
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is SequenceSpecSO so)
        {
            targetSequence = so;
            RebuildIfNeeded(force: true);
            Repaint();
        }
    }

    private void OnGUI()
    {
        _nodesW = Mathf.Clamp(position.width * 0.25f, 190f, 240f);
        _stepsW = Mathf.Clamp(position.width * 0.28f, 220f, 270f);

        DrawToolbar();
        // 드래그 도중 다른 영역에서 mouse up 되어도 확실히 해제
        if (Event.current.type == EventType.MouseUp)
            _isDraggingSteps = false;

        if (targetSequence == null)
        {
            EditorGUILayout.HelpBox("Assign a SituationSpecSO (drag & drop) or select one in Project.",
                MessageType.Info);
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
            targetSequence =
                (SequenceSpecSO)EditorGUILayout.ObjectField(targetSequence, typeof(SequenceSpecSO), false);
            if (EditorGUI.EndChangeCheck())
                RebuildIfNeeded(force: true);

            GUILayout.FlexibleSpace();

            _search = _searchField != null ? _searchField.OnToolbarGUI(_search ?? "") : (_search ?? "");

            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(50)) && targetSequence != null)
                EditorGUIUtility.PingObject(targetSequence);
        }
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("sequence", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_sequenceKeyProp, new GUIContent("sequenceKey"));

            int nodeCount = _nodesProp != null ? _nodesProp.arraySize : 0;
            EditorGUILayout.LabelField($"Nodes: {nodeCount}");

            if (string.IsNullOrWhiteSpace(_sequenceKeyProp.stringValue))
                EditorGUILayout.HelpBox("sequenceKey is empty. Route resolution will fail.", MessageType.Warning);

            if (nodeCount == 0)
                EditorGUILayout.HelpBox("No nodes. Use 'Add Node'.", MessageType.Warning);
        }
    }

    private void DrawNodesPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(_nodesW)))
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                _nodesList?.DoLayoutList();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Node", GUILayout.Height(24)))
                        AddNode();

                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    private void DrawRightPanel()
    {
        if (_nodesList != null && _nodesList.index != _selectedNode)
            _selectedNode = _nodesList.index;

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

            DrawNodeEditor(nodeProp, stepsProp);
        }
    }

    private void DrawNodeEditor(SerializedProperty nodeProp, SerializedProperty stepsProp)
    {
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(_stepsW)))
            {
                EnsureStepsList(nodeProp, stepsProp);
                _stepsList?.DoLayoutList();

                // ✅ Step 버튼을 Steps 아래쪽으로 이동
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Step", GUILayout.Height(24)))
                        AddStep(stepsProp);

                    GUILayout.FlexibleSpace();
                }
            }

            GUILayout.Space(6);

            // ---- Right: Selected Step Detail ----
            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (stepsProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No steps. Add a step first.", MessageType.Info);
                    return;
                }

                if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize)
                {
                    EditorGUILayout.HelpBox("Select a step on the left.", MessageType.Info);
                    return;
                }

                using (new EditorGUI.DisabledScope(_isDraggingSteps))
                using (var scroll = new EditorGUILayout.ScrollViewScope(_rightScroll))
                {
                    _rightScroll = scroll.scrollPosition;

                    var stepProp = stepsProp.GetArrayElementAtIndex(_selectedStep);
                    DrawStepDetail(stepProp);
                }
            }
        }
    }

    private void DrawStepDetail(SerializedProperty stepProp)
    {
        EditorGUILayout.LabelField($"Step {_selectedStep}", EditorStyles.boldLabel);

        // ✅ Step Name
        var stepNameProp = stepProp.FindPropertyRelative("editorName");
        if (stepNameProp != null)
        {
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Name", stepNameProp.stringValue ?? "");
            if (EditorGUI.EndChangeCheck())
                stepNameProp.stringValue = newName;
        }

        EditorGUILayout.Space(6);
        
        // Gate
        var gateProp = stepProp.FindPropertyRelative("gate");
        if (gateProp != null)
        {
            AutoExpandOnce(gateProp);
            EditorGUILayout.PropertyField(gateProp, new GUIContent("Gate(after this step)"), includeChildren: true);

            if (IsStructDefault(gateProp))
                EditorGUILayout.HelpBox(
                    "Gate looks default. Runtime planner will treat default as Input (recommended to set explicitly for readability).",
                    MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("DialogueStepSpec must have GateToken gate.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space(6);

        // Commands
        var commandsProp = stepProp.FindPropertyRelative("commands");
        if (commandsProp == null || !commandsProp.isArray)
        {
            EditorGUILayout.HelpBox(
                "DialogueStepSpec must have List<...> commands (SerializeReference polymorphic list).",
                MessageType.Error);
            return;
        }

        if (!IsSerializeReferenceCommandList(commandsProp))
        {
            EditorGUILayout.HelpBox(
                "This editor supports ONLY [SerializeReference] polymorphic commands.\n" +
                "Please migrate to: [SerializeReference] List<NodeCommandSpecBase>.",
                MessageType.Error);
            return;
        }

        commandsProp.isExpanded = true;
        // ✅ Commands 리스트 먼저
        EnsureCommandsList(stepProp, commandsProp);
        _commandsList?.DoLayoutList();

        // ✅ Command 버튼을 리스트 아래로 이동
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Command", GUILayout.Height(24)))
                AddCommand(commandsProp);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(commandsProp.arraySize == 0 || _commandsList == null ||
                                               _commandsList.index < 0 ||
                                               _commandsList.index >= commandsProp.arraySize))
            {
                if (GUILayout.Button("Delete Command", GUILayout.Width(110), GUILayout.Height(24)))
                    DeleteSelectedCommand(commandsProp); // (원하면 확인도 추가 가능)
            }
        }
    }

    // ------------------------------
    // Rebuild / Lists
    // ------------------------------
    private void RebuildIfNeeded(bool force)
    {
        if (!force && _so != null && _so.targetObject == targetSequence) return;

        if (targetSequence == null)
        {
            _so = null;
            _nodesList = null;
            _stepsList = null;
            _commandsList = null;
            _stepsPropPath = null;
            _commandsPropPath = null;
            _selectedNode = -1;
            _selectedStep = -1;
            return;
        }

        _autoExpandedOnce.Clear();

        // ✅ 이전 선택값 백업
        int prevNode = _selectedNode;
        int prevStep = _selectedStep;

        _so = new SerializedObject(targetSequence);
        _sequenceKeyProp = _so.FindProperty("sequenceKey");
        _nodesProp = _so.FindProperty("nodes");

        int nodeCount = _nodesProp?.arraySize ?? 0;
        _selectedNode = (nodeCount <= 0) ? -1 : Mathf.Clamp(prevNode, 0, nodeCount - 1);

        // ✅ Step 선택도 유지(단, 유효범위로 clamp)
        if (_selectedNode >= 0)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
            var stepsProp = nodeProp.FindPropertyRelative("steps");
            int stepCount = (stepsProp != null && stepsProp.isArray) ? stepsProp.arraySize : 0;

            if (prevStep < 0) _selectedStep = (stepCount > 0) ? 0 : -1; // 원하는 정책: -1 유지하고 싶으면 여기만 -1로.
            else _selectedStep = (stepCount <= 0) ? -1 : Mathf.Clamp(prevStep, 0, stepCount - 1);
        }
        else
        {
            _selectedStep = -1;
        }

        BuildNodesList();
        SyncNodeSelectionToList();
        // 리스트들은 재생성하도록 null
        _stepsList = null;
        _commandsList = null;

        // ✅ propertyPath 캐시도 무효화
        _stepsPropPath = null;
        _commandsPropPath = null;
    }


    private void BuildNodesList()
    {
        if (_nodesProp == null) return;

        _nodesList = new ReorderableList(_so, _nodesProp, draggable: true, displayHeader: true, displayAddButton: false,
            displayRemoveButton: false);
        _nodesList.onSelectCallback = list =>
        {
            _selectedNode = list.index;
            _selectedStep = -1;
            _stepsList = null;
            _commandsList = null;
            Repaint();
        };

        _nodesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Nodes");

        _nodesList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(index);

            // steps count
            int stepCount = 0;
            var stepsProp = nodeProp.FindPropertyRelative("steps");
            if (stepsProp != null && stepsProp.isArray)
                stepCount = stepsProp.arraySize;

            // name prop (DialogueNodeSpec.name)
            var nameProp = nodeProp.FindPropertyRelative("editorName");

            // 검색 hit 체크 (기존 기능 유지)
            bool hit = true;
            if (!string.IsNullOrWhiteSpace(_search))
                hit = NodeMatchesSearch(nodeProp, _search);

            // 레이아웃: [Name] [TextField....................] [(3)]
            const float labelW = 44f; // "Name" 폭
            const float countW = 44f; // "(123)" 폭

            var labelRect = new Rect(rect.x, rect.y + 1f, labelW, rect.height - 2f);
            var fieldRect = new Rect(rect.x + labelW + 2f, rect.y + 1f, rect.width - labelW - countW - 4f,
                rect.height - 2f);
            var countRect = new Rect(rect.x + rect.width - countW, rect.y, countW, rect.height);

// ✅ 왼쪽 라벨
            EditorGUI.LabelField(labelRect, $"Node {index}", EditorStyles.miniLabel);

// ✅ 항상 편집 가능한 TextField (기존 left -> fieldRect로만 변경)
            if (nameProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUI.TextField(fieldRect, nameProp.stringValue ?? "");
                if (EditorGUI.EndChangeCheck())
                    nameProp.stringValue = newName;

                // placeholder (원하면 유지)
                if (string.IsNullOrWhiteSpace(nameProp.stringValue))
                {
                    var ph = fieldRect;
                    ph.x += 4f;
                    EditorGUI.LabelField(ph, $"Node {index}", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                EditorGUI.LabelField(fieldRect, $"Node {index}");
            }

// ✅ 오른쪽 stepCount: 괄호로 감싸기
            EditorGUI.LabelField(countRect, $"({stepCount})", EditorStyles.miniLabel);

            // ✅ Node 우클릭 메뉴(Add/Duplicate/Delete) 유지
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                _selectedNode = index;
                _selectedStep = -1;
                _stepsList = null;
                _commandsList = null;
                Repaint();

                string nodesPath = _nodesProp.propertyPath;

                ShowContextMenu(menu =>
                {
                    menu.AddItem(new GUIContent("Add Node (Below)"), false, () =>
                    {
                        int insertAt = index + 1;

                        DelayModify("Add Node", so =>
                        {
                            var seq = (SequenceSpecSO)so.targetObject;
                            if (seq == null) return;

                            seq.nodes ??= new List<NodeSpec>();

                            insertAt = Mathf.Clamp(insertAt, 0, seq.nodes.Count);
                            seq.nodes.Insert(insertAt, CreateBlankNode()); // ✅ blank

                            _selectedNode = insertAt;
                            _selectedStep = -1;
                            _nodesList = null;
                            _stepsList = null;
                            _commandsList = null;
                        });
                    });

                    menu.AddItem(new GUIContent("Duplicate Node"), false, () =>
                    {
                        int srcIndex = index;
                        int insertAt = index + 1;

                        DelayModify("Duplicate Node", so =>
                        {
                            var seq = (SequenceSpecSO)so.targetObject;
                            if (seq == null) return;

                            seq.nodes ??= new List<NodeSpec>();
                            if (srcIndex < 0 || srcIndex >= seq.nodes.Count) return;

                            insertAt = Mathf.Clamp(insertAt, 0, seq.nodes.Count);
                            seq.nodes.Insert(insertAt, CloneNodeDeep(seq.nodes[srcIndex])); // ✅ deep

                            _selectedNode = insertAt;
                            _selectedStep = -1;
                            _nodesList = null;
                            _stepsList = null;
                            _commandsList = null;
                        });
                    });

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("Delete Node"), false, () =>
                    {
                        // ✅ 노드 이름 가져오기(비어있으면 Node {index})
                        string nodeName = $"Node {index}";
                        var node = _nodesProp.GetArrayElementAtIndex(index);
                        var nameProp = node != null ? node.FindPropertyRelative("name") : null;

                        if (nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue))
                            nodeName = nameProp.stringValue.Trim();

                        if (!EditorUtility.DisplayDialog(
                                "Delete Node",
                                $"Delete Node {index}: \"{nodeName}\" ?",
                                "Delete",
                                "Cancel"))
                            return;

                        DeleteArrayElementByPath("Delete Node", nodesPath, index, after: () =>
                        {
                            int newNode = Mathf.Clamp(_selectedNode, 0, _nodesProp.arraySize - 2);
                            _selectedNode = newNode;
                            _selectedStep = -1;

                            _stepsList = null;
                            _commandsList = null;
                        });
                    });
                });

                e.Use();
            }
        };
    }

    private void EnsureStepsList(SerializedProperty nodeProp, SerializedProperty stepsProp)
    {
        if (_stepsList != null && _stepsPropPath == stepsProp.propertyPath)
        {
            // 하이라이트/선택 안정화
            _stepsList.index = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);
            return;
        }

        _stepsPropPath = stepsProp.propertyPath;

        _selectedStep = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);

        _stepsList = new ReorderableList(_so, stepsProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false);

        _stepsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Steps");

        _stepsList.onSelectCallback = list =>
        {
            _selectedStep = list.index;
            _stepsList.index = _selectedStep; // ✅ 안정화
            _commandsList = null;
            Repaint();
        };

        _stepsList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 8f;

        _stepsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index < 0 || index >= stepsProp.arraySize) return;

            rect.y += 2f;
            rect.height -= 2f;

            bool selected = (_selectedStep == index);
            if (selected)
            {
                var c = EditorGUIUtility.isProSkin
                    ? new Color(0.24f, 0.49f, 0.90f, 0.35f)
                    : new Color(0.24f, 0.49f, 0.90f, 0.20f);
                EditorGUI.DrawRect(rect, c);
            }

            // ✅ 드래그 시작 중 오른쪽 패널 잠금 감지(행 전체)
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                _isDraggingSteps = true;

            var stepProp = stepsProp.GetArrayElementAtIndex(index);
            var gateProp = stepProp.FindPropertyRelative("gate");
            var commandsProp = stepProp.FindPropertyRelative("commands");

            string gateSummary = gateProp != null ? SummarizeGate(gateProp) : "(no gate)";
            int cmdCount = commandsProp != null && commandsProp.isArray ? commandsProp.arraySize : 0;

            // ✅ 기본 핸들이 이미 왼쪽에 그려지므로, 내용은 약간 오른쪽으로만 밀어주면 됨
            const float leftPad = 2f; // 24 -> 16 (더 촘촘)
            var contentRect = new Rect(rect.x + leftPad, rect.y, rect.width - leftPad, rect.height);

            var nameProp = stepProp.FindPropertyRelative("editorName");
            string stepName = (nameProp != null) ? (nameProp.stringValue ?? "") : "";
            stepName = stepName.Trim();

            string title = string.IsNullOrEmpty(stepName) ? $"Step {index}" : stepName;

            EditorGUI.LabelField(contentRect, $"{title} | gate={gateSummary} | ({cmdCount})");
            //EditorGUI.LabelField(contentRect, $"Step {index} | gate={gateSummary} | ({cmdCount}) ");

            // ✅ Step 우클릭 메뉴 (이 element rect에서 직접 처리)
            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                _selectedStep = index;
                _stepsList.index = index; // ✅ 우클릭한 Step을 확실히 선택 상태로
                _commandsList = null;
                Repaint();

                string stepsPath = stepsProp.propertyPath;

                ShowContextMenu(menu =>
                {
                    // -------------------------
                    // 2) ✅ 복사 Step 추가 (NEW)
                    // -------------------------
                    menu.AddItem(new GUIContent("Duplicate Step (Below)"), false, () =>
                    {
                        int nodeIndex = _selectedNode;
                        int srcIndex  = index;
                        int insertAt  = index + 1;

                        DelayModify("Duplicate Step", so =>
                        {
                            var seq = (SequenceSpecSO)so.targetObject;
                            if (seq == null) return;
                            if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

                            var node = seq.nodes[nodeIndex];
                            node.steps ??= new List<StepSpec>();

                            if (srcIndex < 0 || srcIndex >= node.steps.Count) return;

                            insertAt = Mathf.Clamp(insertAt, 0, node.steps.Count);
                            node.steps.Insert(insertAt, CloneStepDeep(node.steps[srcIndex]));  // ✅ deep copy

                            _selectedStep = insertAt;
                            _stepsList = null;
                            _commandsList = null;
                        });
                    });
                    // -------------------------
                    // 1) 빈 Step 추가(기존 기능 유지)
                    // -------------------------
                    menu.AddItem(new GUIContent("Add Step (Empty)"), false, () =>
                    {
                        int nodeIndex = _selectedNode;
                        int insertAt = index + 1;

                        DelayModify("Add Step", so =>
                        {
                            var seq = (SequenceSpecSO)so.targetObject;
                            if (seq == null) return;
                            if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

                            var node = seq.nodes[nodeIndex];
                            node.steps ??= new List<StepSpec>();

                            insertAt = Mathf.Clamp(insertAt, 0, node.steps.Count);
                            node.steps.Insert(insertAt, CreateBlankStep());   // ✅ new list 보장

                            _selectedStep = insertAt;
                            _stepsList = null;
                            _commandsList = null;
                        });
                    });


                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("Delete Step"), false, () =>
                    {
                        if (!EditorUtility.DisplayDialog("Delete Step", $"Delete Step {index}?", "Delete", "Cancel"))
                            return;

                        string stepsPath = stepsProp.propertyPath;

                        DeleteArrayElementByPath("Delete Step", stepsPath, index, after: () =>
                        {
                            _selectedStep = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 2);
                            _stepsList = null;
                            _commandsList = null;
                        });
                    });
                });

                e.Use(); // ✅ 전파 방지 (Nodes쪽에서 메뉴 뜨는 것 같은 현상 방지)
            }
        };


        _stepsList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            _selectedStep = newIndex;
            _commandsList = null;

            _so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetSequence);

            Repaint();
        };

        _stepsList.index = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);
    }

    private void EnsureCommandsList(SerializedProperty stepProp, SerializedProperty commandsProp)
    {
        if (_commandsList != null && _commandsPropPath == commandsProp.propertyPath)
        {
            if (_pendingCommandIndex >= 0)
            {
                _commandsList.index = Mathf.Clamp(_pendingCommandIndex, 0, commandsProp.arraySize - 1);
                _pendingCommandIndex = -1;
            }

            return;
        }

        _commandsPropPath = commandsProp.propertyPath;

        _commandsList = new ReorderableList(_so, commandsProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false);

        _commandsList.onSelectCallback = list => Repaint();

        _commandsList.elementHeightCallback = (index) =>
        {
            if (index < 0 || index >= commandsProp.arraySize)
                return EditorGUIUtility.singleLineHeight;

            var element = commandsProp.GetArrayElementAtIndex(index);

            float h = EditorGUI.GetPropertyHeight(element, includeChildren: true);
            return h + 6f;
        };
        _commandsList.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Commands", EditorStyles.boldLabel); };

        _commandsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index < 0 || index >= commandsProp.arraySize) return;

            var e = Event.current;

            // ✅ 우클릭은 ContextClick이 더 안정적 + PropertyField보다 먼저 처리해야 함
            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                // 우클릭한 커맨드를 선택으로 맞춤
                if (_commandsList != null) _commandsList.index = index;
                Repaint();

                string commandsPath = commandsProp.propertyPath;

                ShowContextMenu(menu =>
                {
                    CacheCommandTypes();
                    string propPath = commandsProp.propertyPath;

                    if (_cachedCommandTypes == null || _cachedCommandTypes.Count == 0)
                    {
                        menu.AddDisabledItem(new GUIContent("Add Command/No types found"));
                    }
                    else
                    {
                        foreach (var t in _cachedCommandTypes)
                        {
                            menu.AddItem(new GUIContent($"Add Command/{t.Name}"), false, () =>
                            {
                                DelayModify("Add Command", so =>
                                {
                                    var fresh = so.FindProperty(propPath);
                                    if (fresh == null || !fresh.isArray) return;

                                    int idx = fresh.arraySize;
                                    fresh.arraySize++;
                                    var el = fresh.GetArrayElementAtIndex(idx);
                                    el.managedReferenceValue = Activator.CreateInstance(t);

                                    if (_commandsList != null) _commandsList.index = idx;
                                });
                            });
                        }
                    }

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("Delete Command"), false, () =>
                    {
                        if (!EditorUtility.DisplayDialog("Delete Command", $"Delete Command {index}?", "Delete",
                                "Cancel"))
                            return;

                        DeleteArrayElementByPath("Delete Command", commandsPath, index, after: () =>
                        {
                            if (_commandsList != null)
                                _commandsList.index = Mathf.Max(0, index - 1);

                            _commandsList = null; // 리빌드로 맞춤
                        });
                    });
                });

                e.Use();
                return; // ✅ 여기서 끝내야 PropertyField가 기본 메뉴를 못 띄움
            }

            var element = commandsProp.GetArrayElementAtIndex(index);

            rect.y += 2f;
            rect.height -= 2f;

            var label = new GUIContent(SummarizeCommand(element, index));

            AutoExpandOnce(element); // 네가 만든 1회 확장 유지

            EditorGUI.PropertyField(rect, element, label, includeChildren: true);
        };

        // ✅ 드래그로 순서 바꾼 뒤: 선택 유지 + dirty 처리
        _commandsList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            list.index = newIndex;

            _so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetSequence);

            Repaint();
        };

        if (_pendingCommandIndex >= 0)
        {
            _commandsList.index = Mathf.Clamp(_pendingCommandIndex, 0, commandsProp.arraySize - 1);
            _pendingCommandIndex = -1;
        }
    }

    // ------------------------------
    // Node operations
    // ------------------------------
    private void AddNode()
    {
        if (_nodesProp == null) return;

        Undo.RecordObject(targetSequence, "Add Node");

        int idx = _nodesProp.arraySize;
        _nodesProp.arraySize++;

        // ✅ 새로 생긴 노드를 강제로 초기화 (복사 방지)
        var newNode = _nodesProp.GetArrayElementAtIndex(idx);

        var nameProp = newNode.FindPropertyRelative("name");
        if (nameProp != null) nameProp.stringValue = "";

        var stepsProp = newNode.FindPropertyRelative("steps");
        if (stepsProp != null && stepsProp.isArray)
            stepsProp.arraySize = 0;

        _selectedNode = idx;
        _selectedStep = -1;

        EditorUtility.SetDirty(targetSequence);
        _stepsList = null;
        _commandsList = null;

        _selectedNode = idx;
        _selectedStep = -1;

        if (_nodesList != null) _nodesList.index = _selectedNode;

        EditorUtility.SetDirty(targetSequence);
        _stepsList = null;
        _commandsList = null;
        Repaint();
    }

    private void DeleteSelectedNode()
    {
        if (_nodesProp == null) return;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return;

        if (!EditorUtility.DisplayDialog("Delete Node", $"Delete Node {_selectedNode}?", "Delete", "Cancel"))
            return;

        Undo.RecordObject(targetSequence, "Delete Node");
        _nodesProp.DeleteArrayElementAtIndex(_selectedNode);

        _selectedNode = Mathf.Clamp(_selectedNode, 0, _nodesProp.arraySize - 1);
        _selectedStep = -1;

        EditorUtility.SetDirty(targetSequence);
        _stepsList = null;
        _commandsList = null;
    }


    // ------------------------------
    // Step operations
    // ------------------------------
    private void AddStep(SerializedProperty stepsProp)
    {
        int nodeIndex = _selectedNode;
        int srcStepIndex = _selectedStep;

        DelayModify("Add Step", so =>
        {
            var seq = (SequenceSpecSO)so.targetObject;
            if (seq == null) return;
            if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

            var node = seq.nodes[nodeIndex];
            node.steps ??= new List<StepSpec>();

            // ✅ 항상 맨 아래에 추가
            int insertAt = node.steps.Count;

            // ✅ 선택된 Step을 기준으로 Deep Clone (없으면 blank)
            StepSpec newStep;
            if (srcStepIndex >= 0 && srcStepIndex < node.steps.Count)
                newStep = CloneStepDeep(node.steps[srcStepIndex]);
            else
                newStep = CreateBlankStep();

            node.steps.Insert(insertAt, newStep);

            // ✅ 새로 만든 Step을 선택 상태로
            _selectedStep = insertAt;

            _stepsList = null;
            _commandsList = null;
        });
    }


    private void DeleteSelectedStep(SerializedProperty stepsProp)
    {
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return;

        if (!EditorUtility.DisplayDialog("Delete Step", $"Delete Step {_selectedStep}?", "Delete", "Cancel"))
            return;

        Undo.RecordObject(targetSequence, "Delete Step");
        stepsProp.DeleteArrayElementAtIndex(_selectedStep);
        _selectedStep = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);

        EditorUtility.SetDirty(targetSequence);
        _stepsList = null;
        _commandsList = null;
    }

    // ------------------------------
    // Command operations (SerializeReference ONLY)
    // ------------------------------
    private void AddCommand(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        if (!IsSerializeReferenceCommandList(commandsProp))
        {
            Debug.LogError("[DialogueSituationToolWindow] commands list is not SerializeReference (ManagedReference).");
            return;
        }

        CacheCommandTypes();

        var menu = new GenericMenu();
        if (_cachedCommandTypes == null || _cachedCommandTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No command types found (NodeCommandSpecBase derived)"));
        }
        else
        {
            foreach (var t in _cachedCommandTypes)
            {
                string path = t.Name;
                menu.AddItem(new GUIContent(path), false, () =>
                {
                    string propPath = commandsProp.propertyPath;
                    DelayModify("Add Command", so =>
                    {
                        var fresh = so.FindProperty(propPath);
                        AddManagedRefCommand(fresh, t);
                    });
                });
            }
        }

        menu.ShowAsContext();
    }

    private void DeleteSelectedCommand(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;
        if (_commandsList == null) return;

        int idx = _commandsList.index;
        if (idx < 0 || idx >= commandsProp.arraySize) return;

        if (!EditorUtility.DisplayDialog("Delete Command", $"Delete Command {idx}?", "Delete", "Cancel"))
            return;

        string commandsPath = commandsProp.propertyPath;

        DeleteArrayElementByPath("Delete Command", commandsPath, idx, after: () =>
        {
            if (_commandsList != null)
                _commandsList.index = Mathf.Max(0, idx - 1);

            _commandsList = null;
        });
    }

    private static void AddManagedRefCommand(SerializedProperty commandsProp, Type concreteType)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        int idx = commandsProp.arraySize;
        commandsProp.arraySize++;

        var element = commandsProp.GetArrayElementAtIndex(idx);
        element.managedReferenceValue = Activator.CreateInstance(concreteType);
    }

    private static bool IsSerializeReferenceCommandList(SerializedProperty commandsProp)
    {
        // 빈 리스트일 땐 판단이 애매하지만, “이 에디터는 SerializeReference를 기대한다”로 처리.
        if (commandsProp == null || !commandsProp.isArray) return false;
        if (commandsProp.arraySize == 0) return true;

        var el = commandsProp.GetArrayElementAtIndex(0);
        return el != null && el.propertyType == SerializedPropertyType.ManagedReference;
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
        if (gateProp == null) return "(null)";

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

        // ✅ SerializeReference only
        if (cmdProp.propertyType != SerializedPropertyType.ManagedReference)
            return $"#{index} (Non-ManagedReference!)";

        var typeName = GetManagedRefTypeName(cmdProp);
        if (string.IsNullOrEmpty(typeName))
            typeName = "(null-ref)";

        if (string.Equals(typeName, DefaultShowLineTypeName, StringComparison.OrdinalIgnoreCase))
        {
            var line = cmdProp.FindPropertyRelative("line");
            if (line != null)
            {
                string speaker = line.FindPropertyRelative("speakerId")?.stringValue ?? "";
                string text = line.FindPropertyRelative("text")?.stringValue ?? "";
                text = Short(text, 42);
                return $"#{index} {typeName}  [{speaker}] {text}";
            }
        }

        string screenId = cmdProp.FindPropertyRelative("screenId")?.stringValue ?? "";
        string widgetId = cmdProp.FindPropertyRelative("widgetId")?.stringValue ?? "";
        if (!string.IsNullOrWhiteSpace(screenId) || !string.IsNullOrWhiteSpace(widgetId))
            return $"#{index} {typeName}  ({screenId}/{widgetId})";

        return $"#{index} {typeName}";
    }

    private static string GetManagedRefTypeName(SerializedProperty managedRefProp)
    {
        string full = managedRefProp.managedReferenceFullTypename; // "AssemblyName Namespace.TypeName"
        if (string.IsNullOrEmpty(full)) return null;

        int space = full.IndexOf(' ');
        if (space < 0 || space + 1 >= full.Length) return null;

        string className = full.Substring(space + 1);
        if (string.IsNullOrEmpty(className)) return null;

        int lastDot = className.LastIndexOf('.');
        return lastDot >= 0 ? className.Substring(lastDot + 1) : className;
    }

    private static string Short(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private void SyncNodeSelectionToList()
    {
        if (_nodesList == null) return;

        int count = _nodesProp?.arraySize ?? 0;
        if (count <= 0)
        {
            _nodesList.index = -1;
            return;
        }

        _selectedNode = Mathf.Clamp(_selectedNode, 0, count - 1);
        _nodesList.index = _selectedNode;
    }

    private void SyncStepSelectionToList(SerializedProperty stepsProp)
    {
        if (_stepsList == null || stepsProp == null || !stepsProp.isArray) return;

        int count = stepsProp.arraySize;
        _selectedStep = (count <= 0) ? -1 : Mathf.Clamp(_selectedStep, 0, count - 1);
        _stepsList.index = _selectedStep;
    }

    private void ShowContextMenu(Action<GenericMenu> build)
    {
        var menu = new GenericMenu();
        build?.Invoke(menu);
        menu.ShowAsContext();
    }

    /// <summary>
    /// 배열 프로퍼티(propertyPath)에 대해 index 요소를 삭제한다.
    /// 삭제 후 선택 인덱스 보정은 호출자가 해주면 된다.
    /// </summary>
    private void DeleteArrayElementByPath(string undoLabel, string arrayPropPath, int index, Action after = null)
    {
        DelayModify(undoLabel, so =>
        {
            var arr = so.FindProperty(arrayPropPath);
            if (arr == null || !arr.isArray) return;
            if (index < 0 || index >= arr.arraySize) return;

            // 1) 첫 삭제
            arr.DeleteArrayElementAtIndex(index);

            // 2) Unity 특성상 "null만 만들고 slot은 남는" 경우가 있음 → 한 번 더
            if (index < arr.arraySize)
            {
                var el = arr.GetArrayElementAtIndex(index);

                bool needsSecondDelete =
                    (el.propertyType == SerializedPropertyType.ObjectReference && el.objectReferenceValue == null) ||
                    (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null);

                if (needsSecondDelete)
                    arr.DeleteArrayElementAtIndex(index);
            }

            after?.Invoke();
        });
    }

    private void AutoExpandOnce(SerializedProperty prop)
    {
        if (prop == null) return;

        // propertyPath는 충분히 유니크함 (선택/리빌드 시 Clear 하니 더 안전)
        string key = prop.propertyPath;

        if (_autoExpandedOnce.Add(key))
            prop.isExpanded = true; // ✅ 처음 본 순간에만 펼침
    }
#if UNITY_EDITOR
    private static StepSpec CreateBlankStep()
    {
        return new StepSpec
        {
            gate = default,
            commands = new List<CommandSpecBase>()
        };
    }
    private static NodeSpec CreateBlankNode()
    {
        return new NodeSpec
        {
            editorName = "",
            steps = new List<StepSpec>()
        };
    }

    private static NodeSpec CloneNodeDeep(NodeSpec src)
    {
        if (src == null) return CreateBlankNode();

        var dst = new NodeSpec
        {
            editorName = src.editorName,
            steps = new List<StepSpec>()
        };

        if (src.steps != null)
        {
            foreach (var s in src.steps)
                dst.steps.Add(CloneStepDeep(s)); // ✅ Step까지 deep
        }

        return dst;
    }

    private static StepSpec CloneStepDeep(StepSpec src)
    {
        if (src == null) return CreateBlankStep();

        var dst = new StepSpec
        {
            gate = src.gate, // struct 이면 값 복사 OK
            commands = new List<CommandSpecBase>()
        };

        if (src.commands != null)
        {
            foreach (var c in src.commands)
                dst.commands.Add(CloneCommandDeep(c));
        }

        return dst;
    }

    private static CommandSpecBase CloneCommandDeep(CommandSpecBase src)
    {
        if (src == null) return null;

        var t = src.GetType();
        var clone = (CommandSpecBase)Activator.CreateInstance(t);

        // ✅ EditorJsonUtility는 SerializeReference(폴리모픽) 복사에 유리
        string json = EditorJsonUtility.ToJson(src);
        EditorJsonUtility.FromJsonOverwrite(json, clone);

        return clone;
    }
#endif

    // ------------------------------
    // Misc helpers
    // ------------------------------
    private static bool IsStructDefault(SerializedProperty prop)
    {
#if UNITY_2021_2_OR_NEWER
        try
        {
            object v = prop.boxedValue;
            if (v == null) return true;
            var t = v.GetType();
            object def = Activator.CreateInstance(t);
            return v.Equals(def);
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }
}
#endif