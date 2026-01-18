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

    // ------------------------------
    // Target
    // ------------------------------
    [SerializeField] private SequenceSpecSO targetSequence;

    private SerializedObject _so;
    private SerializedProperty _sequenceKeyProp;
    private SerializedProperty _nodesProp;

    // ------------------------------
    // Lists
    // ------------------------------
    private ReorderableList _nodesList;
    private ReorderableList _stepsList;
    private ReorderableList _commandsList;

    private int _selectedNode = -1;
    private int _selectedStep = -1;

    private string _stepsPropPath;
    private string _commandsPropPath;

    // ------------------------------
    // Track UI
    // ------------------------------
    private CommandTrackType _activeTrack = CommandTrackType.Dialogue;

    private static readonly GUIContent[] TrackTabs =
    {
        new GUIContent("Interaction"),
        new GUIContent("Setup"),
        new GUIContent("Motion"),
        new GUIContent("Dialogue"),
        new GUIContent("FX"),
    };

    // ------------------------------
    // Toolbar Search
    // ------------------------------
    private SearchField _searchField;
    private string _search = "";

    // ------------------------------
    // UX
    // ------------------------------
    private Vector2 _rightScroll;
    private bool _isDraggingSteps;
    private int _pendingCommandIndex = -1;
    private bool _scrollToNewCommand;

    private bool _scrollToCommandIndex;
    private int _scrollTargetCommandIndex = -1;
    private Vector2 _compiledScroll;
    private bool _compiledFoldout = true;

    private float _nodesW;
    private float _stepsW;

    private bool _hasSelectedCommand;

    // ------------------------------
    // Defaults on Add
    // ------------------------------
    [SerializeField] private bool _autoFillIdsOnAdd = true;
    [SerializeField] private string _defaultScreenId = "";
    [SerializeField] private string _defaultWidgetId = "";

    private const string PrefKey_DefaultScreenId = "CPS.SequenceEditor.DefaultScreenId";
    private const string PrefKey_DefaultWidgetId = "CPS.SequenceEditor.DefaultRoleKey";
    private const string PrefKey_AutoFillOnAdd = "CPS.SequenceEditor.AutoFillIdsOnAdd";

    // ------------------------------
    // Foldouts (SerializeReference stable id)
    // ------------------------------
    private readonly Dictionary<string, Dictionary<long, bool>> _commandFoldoutsByPath = new();

    private const string FoldoutKeyPrefix = "CPS.SequenceEditor.Foldouts.";

    [Serializable]
    private sealed class FoldoutStateBox
    {
        public List<PathEntry> entries = new();
    }

    [Serializable]
    private sealed class PathEntry
    {
        public string path;
        public List<long> ids = new();
        public List<bool> values = new();
    }

    // ------------------------------
    // Polymorphic Command Types
    // ------------------------------
    private static List<Type> _cachedCommandTypes;

    private const string CommandClipboardPrefix = "CPS_CMD_SPEC::";
    private const string StepClipboardPrefix = "CPS_STEP_SPEC::";

    // ------------------------------
    // Unity callbacks
    // ------------------------------
    private void OnEnable()
    {
        minSize = new Vector2(760f, 380f);
        wantsMouseMove = true;

        _searchField = new SearchField();
        CacheCommandTypes();

        _autoFillIdsOnAdd = EditorPrefs.GetBool(PrefKey_AutoFillOnAdd, _autoFillIdsOnAdd);
        _defaultScreenId = EditorPrefs.GetString(PrefKey_DefaultScreenId, _defaultScreenId);
        _defaultWidgetId = EditorPrefs.GetString(PrefKey_DefaultWidgetId, _defaultWidgetId);

        RebuildIfNeeded(force: true);
        LoadFoldouts();
    }

    private void OnDisable()
    {
        SaveFoldouts();
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SaveFoldouts();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is SequenceSpecSO so)
        {
            targetSequence = so;
            RebuildIfNeeded(force: true);
            LoadFoldouts();
            Repaint();
        }
    }

    private void OnGUI()
    {
        _nodesW = Mathf.Clamp(position.width * 0.24f, 185f, 240f);
        _stepsW = Mathf.Clamp(position.width * 0.28f, 215f, 300f);

        DrawToolbar();

        if (Event.current.type == EventType.MouseUp)
            _isDraggingSteps = false;

        if (targetSequence == null)
        {
            EditorGUILayout.HelpBox("Assign a SequenceSpecSO or select one in Project.", MessageType.Info);
            return;
        }

        RebuildIfNeeded(force: false);

        if (_so == null)
        {
            EditorGUILayout.HelpBox("Failed to create SerializedObject.", MessageType.Error);
            return;
        }

        _so.Update();

        HandleGlobalCommandDeleteShortcut();

        DrawHeader();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawNodesPanel();
            DrawRightPanel();
        }

        bool changed = _so.ApplyModifiedProperties();
        if (changed)
        {
            EditorUtility.SetDirty(targetSequence);
            ForceCompileAll();
        }

        _so.ApplyModifiedProperties();
    }

    // ------------------------------
    // Toolbar + Header
    // ------------------------------
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            targetSequence = (SequenceSpecSO)EditorGUILayout.ObjectField(targetSequence, typeof(SequenceSpecSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildIfNeeded(force: true);
                LoadFoldouts();
            }

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

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4f);

                float old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 90f;

                EditorGUILayout.PropertyField(_sequenceKeyProp, new GUIContent("sequenceKey"),
                    GUILayout.MaxWidth(360f));
                EditorGUIUtility.labelWidth = old;

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _autoFillIdsOnAdd = EditorGUILayout.ToggleLeft("Auto-fill", _autoFillIdsOnAdd, GUILayout.Width(80f));
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetBool(PrefKey_AutoFillOnAdd, _autoFillIdsOnAdd);

                GUILayout.Space(8f);

                EditorGUILayout.LabelField("ScreenId", GUILayout.Width(60f));
                EditorGUI.BeginChangeCheck();
                string newScreenId = EditorGUILayout.TextField(_defaultScreenId, GUILayout.Width(170f));
                if (EditorGUI.EndChangeCheck())
                {
                    _defaultScreenId = newScreenId;
                    EditorPrefs.SetString(PrefKey_DefaultScreenId, _defaultScreenId);
                }

                GUILayout.Space(16f);

                EditorGUILayout.LabelField("RoleKey", GUILayout.Width(90f));
                EditorGUI.BeginChangeCheck();
                string newWidget = EditorGUILayout.TextField(_defaultWidgetId, GUILayout.Width(170f));
                if (EditorGUI.EndChangeCheck())
                {
                    _defaultWidgetId = newWidget;
                    EditorPrefs.SetString(PrefKey_DefaultWidgetId, _defaultWidgetId);
                }

                GUILayout.FlexibleSpace();

                // 각 버튼을 개별 조건으로 Disable
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Step 단위
                    using (new EditorGUI.DisabledScope(!CanApplyIdsToCurrentStep()))
                    {
                        if (GUILayout.Button(
                                new GUIContent("Apply IDs (Step)",
                                    "Apply default IDs to ALL tracks in the current step."),
                                GUILayout.Width(140f)))
                        {
                            ApplyDefaultIdsToCurrentStep();
                        }
                    }

                    GUILayout.Space(4f);

                    // Node 단위
                    using (new EditorGUI.DisabledScope(!CanApplyIdsToCurrentNode()))
                    {
                        if (GUILayout.Button(
                                new GUIContent("Apply IDs (Node)",
                                    "Apply default IDs to ALL steps in the current node."),
                                GUILayout.Width(140f)))
                        {
                            ApplyDefaultIdsToCurrentNode();
                        }
                    }
                }
            }

            int nodeCount = _nodesProp != null ? _nodesProp.arraySize : 0;
            EditorGUILayout.LabelField($"Nodes: {nodeCount}");

            if (string.IsNullOrWhiteSpace(_sequenceKeyProp?.stringValue))
                EditorGUILayout.HelpBox("sequenceKey is empty. Route resolution will fail.", MessageType.Warning);

            if (nodeCount == 0)
                EditorGUILayout.HelpBox("No nodes. Use 'Add Node'.", MessageType.Warning);
        }
    }

    // ------------------------------
    // Panels
    // ------------------------------
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
                EditorGUILayout.HelpBox("NodeSpec must have List<StepSpec> steps.", MessageType.Error);
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
            // ---- Steps list ----
            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(_stepsW)))
            {
                EnsureStepsList(nodeProp, stepsProp);
                _stepsList?.DoLayoutList();
                HandleStepShortcuts(stepsProp);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Step", GUILayout.Height(24)))
                        AddStep(stepsProp);

                    GUILayout.FlexibleSpace();
                }
            }

            GUILayout.Space(6);

            // ---- Step detail ----
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

                var stepProp = stepsProp.GetArrayElementAtIndex(_selectedStep);

                using (new EditorGUI.DisabledScope(_isDraggingSteps))
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_rightScroll, GUILayout.ExpandHeight(true)))
                    {
                        _rightScroll = scroll.scrollPosition;

                        // ✅ 스크롤 안에는 Step 본문(커맨드 리스트까지)만
                        DrawStepDetail(stepProp);

                        if (_scrollToNewCommand && Event.current.type == EventType.Repaint)
                        {
                            _rightScroll.y = float.MaxValue;
                            _scrollToNewCommand = false;
                        }

                        if (_scrollToCommandIndex && Event.current.type == EventType.Repaint)
                        {
                            const float baseOffset = 180f;
                            const float perRow = 54f;

                            _rightScroll.y = baseOffset + (_scrollTargetCommandIndex * perRow);

                            _scrollToCommandIndex = false;
                            _scrollTargetCommandIndex = -1;
                        }
                    }

                    EditorGUILayout.Space(4);

                    DrawCompiledPreview(stepProp);

                    EditorGUILayout.Space(4);

                    //DrawTimingHint(stepProp);

                    DrawBottomCommandBar(stepProp);
                }
            }
        }
    }

    private void DrawBottomCommandBar(SerializedProperty stepProp)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(54f)))
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4f);

                var trackListProp = FindActiveTrackList(stepProp);
                bool validCommands = (trackListProp != null && trackListProp.isArray);
                bool hasCommands = validCommands && trackListProp.arraySize > 0;

                using (new EditorGUI.DisabledScope(!validCommands))
                {
                    if (GUILayout.Button("+ Command", GUILayout.Width(100), GUILayout.Height(34)))
                    {
                        string commandsPath = trackListProp.propertyPath;
                        int insertAt = trackListProp.arraySize;

                        ShowCommandAddMenu(
                            commandsPath,
                            insertAt: insertAt,
                            onSingle: t => InsertSingleAt(commandsPath, insertAt, t, scroll: true),
                            onBatch: types => InsertBatchAt(commandsPath, insertAt, types, scroll: true)
                        );
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(200f)))
                {
                    GUILayout.Space(4f);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(!hasCommands))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Expand All", GUILayout.Width(96), GUILayout.Height(28)))
                            SetAllCommandFoldouts(trackListProp, true);

                        GUILayout.Space(2f);

                        if (GUILayout.Button("Collapse All", GUILayout.Width(96), GUILayout.Height(28)))
                            SetAllCommandFoldouts(trackListProp, false);
                    }

                    GUILayout.Space(2f);
                }

                GUILayout.Space(2f);
            }
        }
    }

    // ------------------------------
    // Step Detail
    // ------------------------------
    private void DrawStepDetail(SerializedProperty stepProp)
    {
        EditorGUILayout.LabelField($"Step {_selectedStep}", EditorStyles.boldLabel);

        // Step label
        var stepNameProp = stepProp.FindPropertyRelative("editorName");
        if (stepNameProp != null)
        {
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Step Label", stepNameProp.stringValue ?? "");
            if (EditorGUI.EndChangeCheck())
                stepNameProp.stringValue = newName;
        }

        EditorGUILayout.Space(6);

        // Gate
        var gateProp = stepProp.FindPropertyRelative("gate");
        if (gateProp != null)
        {
            EditorGUILayout.PropertyField(gateProp, new GUIContent("Gate(after this step)"), includeChildren: true);
        }
        else
        {
            EditorGUILayout.HelpBox("StepSpec must have GateToken gate.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space(8);

        // Track tabs
        DrawTrackTabs();

        // Active track list
        var trackListProp = FindActiveTrackList(stepProp);
        if (trackListProp == null || !trackListProp.isArray)
        {
            EditorGUILayout.HelpBox(
                "StepSpec.tracks.<track> list is missing or not an array. Check StepTracks field names.",
                MessageType.Error);
            return;
        }

        // Commands list (editable)
        EditorGUILayout.LabelField("Commands (Active Track)", EditorStyles.boldLabel);
        EnsureCommandsList(stepProp, trackListProp);
        _commandsList?.DoLayoutList();
        HandleCommandShortcuts(trackListProp);
    }

    private void DrawTrackTabs()
    {
        int current = TrackToIndex(_activeTrack);

        using (new EditorGUILayout.HorizontalScope())
        {
            int next = GUILayout.Toolbar(current, TrackTabs);
            if (next != current)
            {
                _activeTrack = IndexToTrack(next);

                // switching track => reset command selection and list cache
                _commandsList = null;
                _commandsPropPath = null;
                _hasSelectedCommand = false;

                Repaint();
            }
        }
    }

    private void DrawCompiledPreview(SerializedProperty stepProp)
    {
        var compiledProp = stepProp.FindPropertyRelative("compiled");
        if (compiledProp == null || !compiledProp.isArray)
        {
            EditorGUILayout.HelpBox(
                "StepSpec.compiled missing. (It should exist as [SerializeReference] List<CommandSpecBase> compiled)",
                MessageType.Warning);
            return;
        }

        int count = compiledProp.arraySize;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _compiledFoldout = EditorGUILayout.Foldout(
                _compiledFoldout,
                $"Compiled (Runtime Order)  ({count})",
                true); // toggleOnLabelClick = true

            if (!_compiledFoldout)
            {
                if (count == 0)
                    EditorGUILayout.LabelField("— empty —", EditorStyles.centeredGreyMiniLabel);

                return;
            }

            if (count == 0)
            {
                EditorGUILayout.LabelField("— empty —", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var origin = BuildOriginMapForStep(stepProp);

            const float compiledHeight = 300f;

            using (var scroll = new EditorGUILayout.ScrollViewScope(
                       _compiledScroll,
                       GUILayout.Height(compiledHeight)))
            {
                _compiledScroll = scroll.scrollPosition;

                for (int i = 0; i < count; i++)
                {
                    var el = compiledProp.GetArrayElementAtIndex(i);
                    if (el == null) continue;

                    string line = SummarizeCompiledLine(el, i, origin, out bool hasDrift, out bool missingOrigin);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var style = new GUIStyle(EditorStyles.label);
                        if (hasDrift || missingOrigin)
                        {
                            style.normal.textColor =
                                EditorGUIUtility.isProSkin
                                    ? new Color(1f, 0.78f, 0.25f)
                                    : new Color(0.65f, 0.35f, 0.0f);
                        }

                        if (GUILayout.Button(line, style))
                        {
                            if (TryGetOrigin(el, origin, out var o))
                            {
                                JumpToOrigin(stepProp, o.track, o.index);
                            }
                        }

                        if (missingOrigin)
                            GUILayout.Label("(! missing)", EditorStyles.miniLabel, GUILayout.Width(70));
                        else if (hasDrift)
                            GUILayout.Label("(! drift)", EditorStyles.miniLabel, GUILayout.Width(50));
                    }
                }
            }

            EditorGUILayout.Space(4);

            // using (new EditorGUILayout.HorizontalScope())
            // {
            //     GUILayout.FlexibleSpace();
            //
            //     if (GUILayout.Button(
            //             new GUIContent("Rebuild Compiled", "Recompile tracks -> compiled"),
            //             GUILayout.Width(140)))
            //     {
            //         DelayModify("Rebuild Compiled", so =>
            //         {
            //             ForceCompileAll();
            //         }, forceRebuild: false);
            //     }
            // }
        }
    }

    private void DrawTimingHint(SerializedProperty stepProp)
    {
        // This is intentionally minimal.
        // Later you can compute durations from spec.Meta.durationHint / blockingHint etc.
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Timing Preview (stub)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Later: visualize blocking commands & durations using spec.Meta hints.");
        }
    }

    private string GetFoldoutStorageKey()
    {
        if (targetSequence == null) return null;

        string assetPath = AssetDatabase.GetAssetPath(targetSequence);
        if (string.IsNullOrEmpty(assetPath)) return null;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) return null;

        return FoldoutKeyPrefix + guid;
    }

    private void SaveFoldouts()
    {
        string key = GetFoldoutStorageKey();
        if (string.IsNullOrEmpty(key)) return;

        var box = new FoldoutStateBox();

        foreach (var kv in _commandFoldoutsByPath)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;

            var entry = new PathEntry { path = kv.Key };
            foreach (var kv2 in kv.Value)
            {
                entry.ids.Add(kv2.Key);
                entry.values.Add(kv2.Value);
            }

            box.entries.Add(entry);
        }

        string json = JsonUtility.ToJson(box);
        SessionState.SetString(key, json);
        EditorPrefs.SetString(key, json);
    }

    private void LoadFoldouts()
    {
        string key = GetFoldoutStorageKey();
        if (string.IsNullOrEmpty(key)) return;

        string json = SessionState.GetString(key, "");
        if (string.IsNullOrEmpty(json))
            json = EditorPrefs.GetString(key, "");

        _commandFoldoutsByPath.Clear();

        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var box = JsonUtility.FromJson<FoldoutStateBox>(json);
            if (box?.entries == null) return;

            foreach (var entry in box.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.path)) continue;
                if (entry.ids == null || entry.values == null) continue;

                var map = new Dictionary<long, bool>();
                int n = Mathf.Min(entry.ids.Count, entry.values.Count);

                for (int i = 0; i < n; i++)
                {
                    long id = entry.ids[i];
                    if (id == 0) continue;
                    map[id] = entry.values[i];
                }

                _commandFoldoutsByPath[entry.path] = map;
            }
        }
        catch
        {
            // 깨진 데이터면 무시
        }
    }

    // ------------------------------
    // Rebuild / Lists
    // ------------------------------
    private void RebuildIfNeeded(bool force)
    {
        if (!force && _so != null && _so.targetObject == targetSequence && _nodesList != null)
            return;

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

        int prevNode = _selectedNode;
        int prevStep = _selectedStep;

        _so = new SerializedObject(targetSequence);
        _sequenceKeyProp = _so.FindProperty("sequenceKey");
        _nodesProp = _so.FindProperty("nodes");

        int nodeCount = _nodesProp?.arraySize ?? 0;
        _selectedNode = (nodeCount <= 0) ? -1 : Mathf.Clamp(prevNode, 0, nodeCount - 1);

        if (_selectedNode >= 0)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
            var stepsProp = nodeProp.FindPropertyRelative("steps");
            int stepCount = (stepsProp != null && stepsProp.isArray) ? stepsProp.arraySize : 0;
            _selectedStep = (stepCount <= 0) ? -1 : Mathf.Clamp(prevStep, 0, stepCount - 1);
        }
        else
        {
            _selectedStep = -1;
        }

        _commandFoldoutsByPath.Clear();

        BuildNodesList();

        _stepsList = null;
        _commandsList = null;

        _stepsPropPath = null;
        _commandsPropPath = null;
    }

    private void BuildNodesList()
    {
        if (_nodesProp == null) return;

        _nodesList = new ReorderableList(_so, _nodesProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false);

        _nodesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Nodes");

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
            if (index < 0 || index >= _nodesProp.arraySize) return;

            var nodeProp = _nodesProp.GetArrayElementAtIndex(index);
            var stepsProp = nodeProp.FindPropertyRelative("steps");
            int stepCount = (stepsProp != null && stepsProp.isArray) ? stepsProp.arraySize : 0;

            var nameProp = nodeProp.FindPropertyRelative("editorName");

            // optional search dim
            bool hit = true;
            if (!string.IsNullOrWhiteSpace(_search))
                hit = NodeMatchesSearch(nodeProp, _search);

            if (!hit && Event.current.type == EventType.Repaint)
            {
                var dim = EditorGUIUtility.isProSkin ? new Color(0, 0, 0, 0.28f) : new Color(1, 1, 1, 0.38f);
                EditorGUI.DrawRect(rect, dim);
            }

            const float labelW = 52f;
            const float countW = 44f;

            var labelRect = new Rect(rect.x, rect.y + 1f, labelW, rect.height - 2f);
            var fieldRect = new Rect(rect.x + labelW + 2f, rect.y + 1f, rect.width - labelW - countW - 4f,
                rect.height - 2f);
            var countRect = new Rect(rect.x + rect.width - countW, rect.y, countW, rect.height);

            EditorGUI.LabelField(labelRect, $"Node {index}", EditorStyles.miniLabel);

            if (nameProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUI.TextField(fieldRect, nameProp.stringValue ?? "");
                if (EditorGUI.EndChangeCheck())
                    nameProp.stringValue = newName;

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

            EditorGUI.LabelField(countRect, $"({stepCount})", EditorStyles.miniLabel);

            // right click
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
                            seq.nodes.Insert(insertAt, CreateBlankNode());

                            _selectedNode = insertAt;
                            _selectedStep = -1;
                            _nodesList = null;
                            _stepsList = null;
                            _commandsList = null;

                            ForceCompileAll();
                        });
                    });

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("Delete Node"), false, () =>
                    {
                        DeleteArrayElementByPath("Delete Node", nodesPath, index, after: () =>
                        {
                            int newNode = Mathf.Clamp(_selectedNode, 0, _nodesProp.arraySize - 2);
                            _selectedNode = newNode;
                            _selectedStep = -1;

                            _stepsList = null;
                            _commandsList = null;

                            ForceCompileAll();
                        });
                    });
                });

                e.Use();
            }
        };

        SyncNodeSelectionToList();
    }

    private void EnsureStepsList(SerializedProperty nodeProp, SerializedProperty stepsProp)
    {
        if (_stepsList != null && _stepsPropPath == stepsProp.propertyPath)
        {
            _stepsList.index = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);
            return;
        }

        _stepsPropPath = stepsProp.propertyPath;

        _selectedStep = (stepsProp.arraySize <= 0) ? -1 : Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);

        _stepsList = new ReorderableList(_so, stepsProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false);

        _stepsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Steps");

        _stepsList.onSelectCallback = list =>
        {
            _selectedStep = list.index;
            _stepsList.index = _selectedStep;
            _commandsList = null;
            Repaint();
        };

        _stepsList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 10f;

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

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                _isDraggingSteps = true;

            var stepProp = stepsProp.GetArrayElementAtIndex(index);

            var gateProp = stepProp.FindPropertyRelative("gate");
            string gateSummary = gateProp != null ? SummarizeGate(gateProp) : "(no gate)";

            // show compiled count (runtime-relevant)
            var compiledProp = stepProp.FindPropertyRelative("compiled");
            int cmdCount = (compiledProp != null && compiledProp.isArray) ? compiledProp.arraySize : 0;

            var nameProp = stepProp.FindPropertyRelative("editorName");
            string stepName = (nameProp != null) ? (nameProp.stringValue ?? "") : "";
            stepName = stepName.Trim();
            string title = string.IsNullOrEmpty(stepName) ? $"Step {index}" : stepName;

            const float leftPad = 2f;
            var contentRect = new Rect(rect.x + leftPad, rect.y, rect.width - leftPad, rect.height);
            EditorGUI.LabelField(contentRect, $"{title} | {gateSummary} | ({cmdCount})");

            // right-click menu
            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                _selectedStep = index;
                _stepsList.index = index;
                _commandsList = null;
                Repaint();

                string stepsPath = stepsProp.propertyPath;

                ShowContextMenu(menu =>
                {
                    menu.AddItem(new GUIContent("Add Step (below)"), false, () =>
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
                            node.steps.Insert(insertAt, CreateBlankStep());

                            _selectedStep = insertAt;
                            _stepsList = null;
                            _commandsList = null;

                            ForceCompileAll();
                        });
                    });

                    menu.AddItem(new GUIContent("Duplicate Step"), false, () =>
                    {
                        int nodeIndex = _selectedNode;
                        int srcIndex = index;
                        int insertAt = index + 1;

                        DelayModify("Duplicate Step", so =>
                        {
                            var seq = (SequenceSpecSO)so.targetObject;
                            if (seq == null) return;
                            if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

                            var node = seq.nodes[nodeIndex];
                            node.steps ??= new List<StepSpec>();

                            if (srcIndex < 0 || srcIndex >= node.steps.Count) return;

                            insertAt = Mathf.Clamp(insertAt, 0, node.steps.Count);
                            node.steps.Insert(insertAt, CloneStepDeep(node.steps[srcIndex]));

                            _selectedStep = insertAt;
                            _stepsList = null;
                            _commandsList = null;

                            ForceCompileAll();
                        });
                    });

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("Delete Step"), false, () =>
                    {
                        DeleteArrayElementByPath("Delete Step", stepsPath, index, after: () =>
                        {
                            _selectedStep = Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 2);
                            _stepsList = null;
                            _commandsList = null;

                            ForceCompileAll();
                        });
                    });
                });

                e.Use();
            }
        };

        _stepsList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            _selectedStep = newIndex;
            _commandsList = null;

            _so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetSequence);

            ForceCompileAll();
            Repaint();
        };

        _stepsList.index = (_selectedStep < 0) ? -1 : Mathf.Clamp(_selectedStep, 0, stepsProp.arraySize - 1);
    }

    private void EnsureCommandsList(SerializedProperty stepProp, SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray)
            return;

        if (!IsSerializeReferenceCommandList(commandsProp))
        {
            EditorGUILayout.HelpBox("This editor requires [SerializeReference] polymorphic command lists.",
                MessageType.Error);
            return;
        }

        string commandsPath = commandsProp.propertyPath;
        var foldoutMap = GetFoldoutMap(commandsPath);

        // same list => only pending selection
        if (_commandsList != null && _commandsPropPath == commandsPath)
        {
            if (_pendingCommandIndex >= 0)
            {
                _commandsList.index = Mathf.Clamp(_pendingCommandIndex, 0, commandsProp.arraySize - 1);
                _pendingCommandIndex = -1;

                _hasSelectedCommand = _commandsList.index >= 0 && _commandsList.index < commandsProp.arraySize;
            }

            return;
        }

        _commandsPropPath = commandsPath;

        _commandsList = new ReorderableList(_so, commandsProp,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false);

        _commandsList.index = -1;
        _hasSelectedCommand = false;

        _commandsList.onSelectCallback = list =>
        {
            _hasSelectedCommand = (list.index >= 0 && list.index < commandsProp.arraySize);
            Repaint();
        };

        _commandsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Commands", EditorStyles.boldLabel);

            var e = Event.current;
            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                ShowCommandAddMenu(
                    commandsPath,
                    insertAt: 0,
                    onSingle: t => InsertSingleAt(commandsPath, 0, t, scroll: true),
                    onBatch: types => InsertBatchAt(commandsPath, 0, types, scroll: true)
                );
                e.Use();
            }
        };

        _commandsList.drawNoneElementCallback = rect =>
        {
            GUI.Label(rect, "No commands yet. Right-click to add.", EditorStyles.centeredGreyMiniLabel);

            var e = Event.current;
            bool rightClick =
                (e.type == EventType.ContextClick) ||
                (e.type == EventType.MouseDown && e.button == 1);

            if (rightClick && rect.Contains(e.mousePosition))
            {
                ShowCommandAddMenu(
                    commandsPath,
                    insertAt: 0,
                    onSingle: t => InsertSingleAt(commandsPath, 0, t, scroll: true),
                    onBatch: types => InsertBatchAt(commandsPath, 0, types, scroll: true)
                );
                e.Use();
            }
        };

        _commandsList.elementHeightCallback = index =>
        {
            float header = EditorGUIUtility.singleLineHeight;

            if (index < 0 || index >= commandsProp.arraySize)
                return header + 6f;

            var el = commandsProp.GetArrayElementAtIndex(index);
            if (el == null || el.propertyType != SerializedPropertyType.ManagedReference)
                return header + 6f;

            long id = el.managedReferenceId;

            bool expanded = false;
            if (foldoutMap != null && id != 0 && foldoutMap.TryGetValue(id, out bool saved))
                expanded = saved;

            el.isExpanded = expanded;

            float body = GetManagedRefBodyHeight(el);
            return header + body + 10f;
        };

        _commandsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index < 0 || index >= commandsProp.arraySize) return;

            var e = Event.current;

            // background zebra + selected
            if (e.type == EventType.Repaint)
            {
                bool even = (index % 2) == 0;

                var bg = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, even ? 0.04f : 0.02f)
                    : new Color(0f, 0f, 0f, even ? 0.04f : 0.02f);
                EditorGUI.DrawRect(rect, bg);

                bool selected = (_commandsList != null && _commandsList.index == index);
                if (selected)
                {
                    var sel = EditorGUIUtility.isProSkin
                        ? new Color(0.20f, 0.45f, 0.80f, 0.16f)
                        : new Color(0.20f, 0.45f, 0.80f, 0.10f);
                    EditorGUI.DrawRect(rect, sel);
                }

                var line = new Rect(rect.x, rect.yMax - 1f, rect.width, 1f);
                var c = EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.15f);
                EditorGUI.DrawRect(line, c);
            }

            // context menu on row
            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                if (_commandsList != null) _commandsList.index = index;
                _hasSelectedCommand = true;
                Repaint();

                int clickedIndex = index;
                int insertAt = clickedIndex + 1;

                // Reuse the same menu as header/none-area:
                // Sets / Recent / Category
                ShowCommandAddMenu(
                    commandsPath: commandsPath,
                    insertAt: insertAt,
                    onSingle: t => InsertSingleAt(commandsPath, insertAt, t, scroll: false),
                    onBatch: types => InsertBatchAt(commandsPath, insertAt, types, scroll: false),
                    extendMenu: menu =>
                    {
                        menu.AddSeparator("");

                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            DeleteCommandAt(commandsPath, clickedIndex, after: () =>
                            {
                                if (_commandsList != null)
                                    _commandsList.index = Mathf.Clamp(clickedIndex - 1, 0,
                                        Mathf.Max(0, _commandsList.count - 2));
                                _commandsList = null;

                                ForceCompileAll();
                            });
                        });
                    }
                );

                // If you still want Delete on row context (at the very bottom),
                // we’ll add it via extendMenu inside ShowCommandAddMenu (next section).

                e.Use();
                return;
            }

            var element = commandsProp.GetArrayElementAtIndex(index);
            if (element == null) return;

            rect.y += 2f;
            rect.height -= 2f;

            float lineH = EditorGUIUtility.singleLineHeight;
            var headerRect = new Rect(rect.x, rect.y, rect.width, lineH);

            long id = (element.propertyType == SerializedPropertyType.ManagedReference)
                ? element.managedReferenceId
                : 0;

            bool expanded = false;
            if (foldoutMap != null && id != 0 && foldoutMap.TryGetValue(id, out bool saved))
                expanded = saved;
            element.isExpanded = expanded;

            // foldout arrow
            var arrowRect = new Rect(headerRect.x, headerRect.y, 14f, headerRect.height);
            bool newExpanded =
                EditorGUI.Foldout(arrowRect, element.isExpanded, GUIContent.none, toggleOnLabelClick: false);

            if (newExpanded != element.isExpanded)
            {
                element.isExpanded = newExpanded;
                if (foldoutMap != null && id != 0)
                    foldoutMap[id] = newExpanded;

                SaveFoldouts();
            }
            else
            {
                if (foldoutMap != null && id != 0 && !foldoutMap.ContainsKey(id))
                    foldoutMap[id] = element.isExpanded;
            }

            // label
            var labelRect = new Rect(headerRect.x + 14f, headerRect.y, headerRect.width - 14f, headerRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent(SummarizeCommand(element, index)));

            // body
            if (element.isExpanded)
            {
                var bodyRect = new Rect(rect.x, rect.y + lineH + 2f, rect.width, rect.height - lineH - 2f);
                DrawManagedRefBody(bodyRect, element);
            }
        };

        _commandsList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            list.index = newIndex;

            _so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetSequence);

            ForceCompileAll();
            Repaint();
        };

        // pending selection
        if (_pendingCommandIndex >= 0)
        {
            _commandsList.index = Mathf.Clamp(_pendingCommandIndex, 0, commandsProp.arraySize - 1);
            _pendingCommandIndex = -1;
            _hasSelectedCommand = (_commandsList.index >= 0 && _commandsList.index < commandsProp.arraySize);
        }
    }

    // ------------------------------
    // Modify helper
    // ------------------------------
    private void DelayModify(string undoLabel, Action<SerializedObject> action, bool forceRebuild = false)
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

            // Always keep compiled fresh (simple & safe)
            ForceCompileAll();

            if (forceRebuild)
                RebuildIfNeeded(force: true);

            Repaint();
        };
    }

    private void ForceCompileAll()
    {
        if (targetSequence == null) return;

        // Prefer SequenceSpecSO.CompileAllSteps() if you have it.
        // If you don't, replace this with your compiler entry.
        try
        {
            targetSequence.CompileAllSteps();
        }
        catch
        {
            // As a fallback, do nothing.
            // (But ideally you keep CompileAllSteps() on the asset)
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

        var newNode = _nodesProp.GetArrayElementAtIndex(idx);

        var nameProp = newNode.FindPropertyRelative("editorName");
        if (nameProp != null) nameProp.stringValue = "";

        var stepsProp = newNode.FindPropertyRelative("steps");
        if (stepsProp != null && stepsProp.isArray)
            stepsProp.arraySize = 0;

        _selectedNode = idx;
        _selectedStep = -1;

        _so.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetSequence);

        ForceCompileAll();

        _stepsList = null;
        _commandsList = null;

        if (_nodesList != null) _nodesList.index = _selectedNode;
        Repaint();
    }

    // ------------------------------
    // Step operations
    // ------------------------------
    private void AddStep(SerializedProperty stepsProp)
    {
        int nodeIndex = _selectedNode;

        DelayModify("Add Step", so =>
        {
            var seq = (SequenceSpecSO)so.targetObject;
            if (seq == null) return;
            if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

            var node = seq.nodes[nodeIndex];
            node.steps ??= new List<StepSpec>();

            int insertAt = node.steps.Count;
            node.steps.Insert(insertAt, CreateBlankStep());

            _selectedStep = insertAt;

            _stepsList = null;
            _commandsList = null;
        });
    }

    // ------------------------------
    // Command operations (Active Track only)
    // ------------------------------
    private void AddCommand(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        if (!IsSerializeReferenceCommandList(commandsProp))
        {
            Debug.LogError("[SequenceSpecEditorWindow] Track list is not SerializeReference.");
            return;
        }

        CacheCommandTypes();

        var menu = new GenericMenu();

        if (_cachedCommandTypes == null || _cachedCommandTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No command types found"));
        }
        else
        {
            foreach (var t in _cachedCommandTypes)
            {
                var tt = t;
                menu.AddItem(new GUIContent(tt.Name), false, () =>
                {
                    string propPath = commandsProp.propertyPath;
                    DelayModify("Add Command", so =>
                    {
                        var fresh = so.FindProperty(propPath);
                        if (fresh == null || !fresh.isArray) return;

                        int insertAt = fresh.arraySize;

                        fresh.InsertArrayElementAtIndex(insertAt);
                        var el = fresh.GetArrayElementAtIndex(insertAt);
                        el.managedReferenceValue = CreateCommandInstance(tt);

                        _pendingCommandIndex = insertAt;
                        _commandsList = null;
                        _scrollToNewCommand = true;
                    });
                });
            }
        }

        menu.ShowAsContext();
    }

    private void InsertSingleAt(string commandsPath, int insertAt, Type t, bool scroll)
    {
        DelayModify("Add Command", so =>
        {
            var fresh = so.FindProperty(commandsPath);
            if (fresh == null || !fresh.isArray) return;

            var map = GetFoldoutMap(commandsPath);
            var foldouts = SnapshotCommandFoldouts(fresh);

            int idx = Mathf.Clamp(insertAt, 0, fresh.arraySize);
            fresh.InsertArrayElementAtIndex(idx);

            var el = fresh.GetArrayElementAtIndex(idx);
            el.managedReferenceValue = CreateCommandInstance(t);
            NormalizeInsertedCommandMeta(el, targetTrack: _activeTrack);
            SyncMetaAfterInsert(el, targetTrack: _activeTrack);

            long newId = el.managedReferenceId;

            RestoreCommandFoldouts(fresh, foldouts, newIdToCollapse: -1);

            el.isExpanded = false;
            if (map != null && newId != 0) map[newId] = false;

            _pendingCommandIndex = idx;
            _commandsList = null;
            _scrollToNewCommand = scroll;
        });
    }

    private void InsertBatchAt(string commandsPath, int insertAt, IReadOnlyList<Type> types, bool scroll)
    {
        if (types == null || types.Count == 0) return;

        DelayModify("Add Command Set", so =>
        {
            var fresh = so.FindProperty(commandsPath);
            if (fresh == null || !fresh.isArray) return;

            var map = GetFoldoutMap(commandsPath);
            var foldouts = SnapshotCommandFoldouts(fresh);

            int baseIdx = Mathf.Clamp(insertAt, 0, fresh.arraySize);
            var newIds = new List<long>(types.Count);

            for (int i = 0; i < types.Count; i++)
            {
                int idx = baseIdx + i;
                fresh.InsertArrayElementAtIndex(idx);

                var el = fresh.GetArrayElementAtIndex(idx);
                el.managedReferenceValue = CreateCommandInstance(types[i]);

                NormalizeInsertedCommandMeta(el, targetTrack: _activeTrack);
                SyncMetaAfterInsert(el, targetTrack: _activeTrack);

                el.isExpanded = false;

                long id = el.managedReferenceId;
                if (id != 0) newIds.Add(id);
            }

            RestoreCommandFoldouts(fresh, foldouts, newIdToCollapse: -1);

            if (map != null)
            {
                for (int i = 0; i < newIds.Count; i++)
                    map[newIds[i]] = false;
            }

            _pendingCommandIndex = baseIdx;
            _commandsList = null;
            _scrollToNewCommand = scroll;
        });
    }

    private void DeleteCommandAt(string commandsPath, int index, Action after = null)
    {
        DelayModify("Delete Command", so =>
        {
            var arr = so.FindProperty(commandsPath);
            if (arr == null || !arr.isArray) return;
            if (index < 0 || index >= arr.arraySize) return;

            long deletedId = 0;
            var delEl = arr.GetArrayElementAtIndex(index);
            if (delEl != null && delEl.propertyType == SerializedPropertyType.ManagedReference)
                deletedId = delEl.managedReferenceId;

            var foldouts = SnapshotCommandFoldouts(arr);

            arr.DeleteArrayElementAtIndex(index);
            if (index < arr.arraySize)
            {
                var el = arr.GetArrayElementAtIndex(index);
                bool needsSecondDelete =
                    (el.propertyType == SerializedPropertyType.ObjectReference && el.objectReferenceValue == null) ||
                    (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null);

                if (needsSecondDelete)
                    arr.DeleteArrayElementAtIndex(index);
            }

            RestoreCommandFoldouts(arr, foldouts, newIdToCollapse: -1);

            var map = GetFoldoutMap(commandsPath);
            if (map != null && deletedId != 0)
                map.Remove(deletedId);

            after?.Invoke();
        });
    }

    // ------------------------------
    // Track property resolution
    // ------------------------------
    private SerializedProperty FindActiveTrackList(SerializedProperty stepProp)
    {
        if (stepProp == null) return null;

        var tracksProp = stepProp.FindPropertyRelative("tracks");
        if (tracksProp == null) return null;

        return _activeTrack switch
        {
            CommandTrackType.Interaction => tracksProp.FindPropertyRelative("interaction"),
            CommandTrackType.Setup => tracksProp.FindPropertyRelative("setup"),
            CommandTrackType.Motion => tracksProp.FindPropertyRelative("motion"),
            CommandTrackType.Dialogue => tracksProp.FindPropertyRelative("dialogue"),
            CommandTrackType.FX => tracksProp.FindPropertyRelative("fx"),
            _ => tracksProp.FindPropertyRelative("dialogue"),
        };
    }

    // ------------------------------
    // Apply default IDs
    // ------------------------------
    private bool CanApplyIdsToCurrentStep()
    {
        if (!HasDefaultIds()) return false;

        if (_nodesProp == null) return false;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return false;

        var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
        var stepsProp = nodeProp.FindPropertyRelative("steps");
        if (stepsProp == null || !stepsProp.isArray) return false;
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return false;

        var stepProp = stepsProp.GetArrayElementAtIndex(_selectedStep);
        var tracksProp = stepProp.FindPropertyRelative("tracks");
        if (tracksProp == null) return false;

        // 이 Step 안에 커맨드가 하나라도 있는지 확인
        foreach (var name in new[] { "interaction", "setup", "motion", "dialogue", "fx" })
        {
            var lp = tracksProp.FindPropertyRelative(name);
            if (lp != null && lp.isArray && lp.arraySize > 0)
                return true;
        }

        return false;
    }

    private bool CanApplyIdsToCurrentNode()
    {
        if (!HasDefaultIds()) return false;

        if (_nodesProp == null) return false;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return false;

        var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
        var stepsProp = nodeProp.FindPropertyRelative("steps");
        if (stepsProp == null || !stepsProp.isArray) return false;

        // 이 Node 안에 커맨드가 하나라도 있는지 확인
        for (int si = 0; si < stepsProp.arraySize; si++)
        {
            var stepProp = stepsProp.GetArrayElementAtIndex(si);
            var tracksProp = stepProp.FindPropertyRelative("tracks");
            if (tracksProp == null) continue;

            foreach (var name in new[] { "interaction", "setup", "motion", "dialogue", "fx" })
            {
                var lp = tracksProp.FindPropertyRelative(name);
                if (lp != null && lp.isArray && lp.arraySize > 0)
                    return true;
            }
        }

        return false;
    }

    private bool HasDefaultIds()
    {
        return !string.IsNullOrWhiteSpace(_defaultScreenId) || !string.IsNullOrWhiteSpace(_defaultWidgetId);
    }

    private void ApplyDefaultIdsToCurrentStep()
    {
        if (!HasDefaultIds()) return;

        int nodeIndex = _selectedNode;
        int stepIndex = _selectedStep;

        string screenId = _defaultScreenId ?? string.Empty;
        string widgetRoleKey = _defaultWidgetId ?? string.Empty;

        DelayModify("Apply IDs (Step)", so =>
        {
            var nodes = so.FindProperty("nodes");
            if (nodes == null || !nodes.isArray) return;
            if (nodeIndex < 0 || nodeIndex >= nodes.arraySize) return;

            var nodeProp = nodes.GetArrayElementAtIndex(nodeIndex);
            var stepsProp = nodeProp.FindPropertyRelative("steps");
            if (stepsProp == null || !stepsProp.isArray) return;
            if (stepIndex < 0 || stepIndex >= stepsProp.arraySize) return;

            var stepProp = stepsProp.GetArrayElementAtIndex(stepIndex);
            var tracksProp = stepProp.FindPropertyRelative("tracks");
            if (tracksProp == null) return;

            // 이 Step의 모든 트랙에 기본 ID 적용
            foreach (var name in new[] { "interaction", "setup", "motion", "dialogue", "fx" })
            {
                var lp = tracksProp.FindPropertyRelative(name);
                ApplyDefaultIdsToList(lp, screenId, widgetRoleKey);
            }
        });
    }

    private void ApplyDefaultIdsToList(SerializedProperty listProp, string screenId, string roleKey)
    {
        if (listProp == null || !listProp.isArray)
            return;

        bool hasScreen = !string.IsNullOrWhiteSpace(screenId);
        bool hasRole = !string.IsNullOrWhiteSpace(roleKey);

        if (!hasScreen && !hasRole)
            return;

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var cmdProp = listProp.GetArrayElementAtIndex(i);
            if (cmdProp == null) continue;
            if (cmdProp.propertyType != SerializedPropertyType.ManagedReference) continue;

            if (hasScreen)
            {
                var screenProp = cmdProp.FindPropertyRelative("screenId");
                if (screenProp != null && screenProp.propertyType == SerializedPropertyType.String)
                    screenProp.stringValue = screenId;
            }

            if (hasRole)
            {
                var roleProp = cmdProp.FindPropertyRelative("roleKey");
                if (roleProp != null && roleProp.propertyType == SerializedPropertyType.String)
                    roleProp.stringValue = roleKey;
            }
        }
    }


    private void ApplyDefaultIdsToCurrentNode()
    {
        if (!HasDefaultIds()) return;

        int nodeIndex = _selectedNode;

        string screenId = _defaultScreenId ?? string.Empty;
        string widgetRoleKey = _defaultWidgetId ?? string.Empty;

        DelayModify("Apply IDs (Node)", so =>
        {
            var nodes = so.FindProperty("nodes");
            if (nodes == null || !nodes.isArray) return;
            if (nodeIndex < 0 || nodeIndex >= nodes.arraySize) return;

            var nodeProp = nodes.GetArrayElementAtIndex(nodeIndex);
            var stepsProp = nodeProp.FindPropertyRelative("steps");
            if (stepsProp == null || !stepsProp.isArray) return;

            // 이 Node 안의 모든 Step + 모든 트랙에 기본 ID 적용
            for (int si = 0; si < stepsProp.arraySize; si++)
            {
                var stepProp = stepsProp.GetArrayElementAtIndex(si);
                var tracksProp = stepProp.FindPropertyRelative("tracks");
                if (tracksProp == null) continue;

                foreach (var name in new[] { "interaction", "setup", "motion", "dialogue", "fx" })
                {
                    var lp = tracksProp.FindPropertyRelative(name);
                    ApplyDefaultIdsToList(lp, screenId, widgetRoleKey);
                }
            }
        });
    }

    // ------------------------------
    // Shortcuts
    // ------------------------------
    private void HandleGlobalCommandDeleteShortcut()
    {
        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (EditorGUIUtility.editingTextField) return;

        bool mod = e.control || e.command;
        if (mod) return;
        if (e.keyCode != KeyCode.Delete) return;

        // target current step active track list
        if (_nodesProp == null) return;
        if (_selectedNode < 0 || _selectedNode >= _nodesProp.arraySize) return;

        var nodeProp = _nodesProp.GetArrayElementAtIndex(_selectedNode);
        var stepsProp = nodeProp.FindPropertyRelative("steps");
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_selectedStep < 0 || _selectedStep >= stepsProp.arraySize) return;

        var stepProp = stepsProp.GetArrayElementAtIndex(_selectedStep);
        var trackList = FindActiveTrackList(stepProp);
        if (trackList == null || !trackList.isArray) return;

        if (_commandsList == null) return;

        int idx = _commandsList.index;
        if (idx < 0 || idx >= trackList.arraySize) return;

        string commandsPath = trackList.propertyPath;

        DeleteCommandAt(commandsPath, idx, after: () =>
        {
            if (_commandsList != null)
                _commandsList.index = Mathf.Clamp(idx - 1, 0, trackList.arraySize - 2);
            _commandsList = null;
        });

        e.Use();
    }

    private void HandleCommandShortcuts(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return;
        if (_commandsList == null) return;

        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (EditorGUIUtility.editingTextField) return;

        bool mod = e.control || e.command;

        // Delete
        if (!mod && e.keyCode == KeyCode.Delete)
        {
            int idx = _commandsList.index;
            if (idx >= 0 && idx < commandsProp.arraySize)
            {
                DeleteCommandAt(commandsProp.propertyPath, idx, after: () => { _commandsList = null; });
                e.Use();
            }

            return;
        }

        // Cut
        if (mod && e.keyCode == KeyCode.X)
        {
            int idx = _commandsList.index;
            if (idx >= 0 && idx < commandsProp.arraySize)
            {
                var el = commandsProp.GetArrayElementAtIndex(idx);
                if (el != null && el.propertyType == SerializedPropertyType.ManagedReference)
                {
                    CopyCommandToClipboard(el.managedReferenceValue as CommandSpecBase);
                    DeleteCommandAt(commandsProp.propertyPath, idx, after: () => { _commandsList = null; });
                    e.Use();
                }
            }

            return;
        }

        // Copy
        if (mod && e.keyCode == KeyCode.C)
        {
            int idx = _commandsList.index;
            if (idx >= 0 && idx < commandsProp.arraySize)
            {
                var el = commandsProp.GetArrayElementAtIndex(idx);
                if (el != null && el.propertyType == SerializedPropertyType.ManagedReference)
                {
                    CopyCommandToClipboard(el.managedReferenceValue as CommandSpecBase);
                    e.Use();
                }
            }

            return;
        }

        // Paste
        if (mod && e.keyCode == KeyCode.V)
        {
            if (!TryGetClipboardJson(out string json)) return;

            int insertAt = commandsProp.arraySize;
            int sel = _commandsList.index;
            if (sel >= 0 && sel < commandsProp.arraySize)
                insertAt = sel + 1;

            InsertCommandFactoryAt(
                commandsProp.propertyPath,
                insertAt,
                factory: () => CreateCommandFromJson(json),
                scroll: false,
                expandNew: false
            );

            e.Use();
            return;
        }

        // Duplicate
        if (mod && e.keyCode == KeyCode.D)
        {
            int idx = _commandsList.index;
            if (idx >= 0 && idx < commandsProp.arraySize)
            {
                var el = commandsProp.GetArrayElementAtIndex(idx);
                if (el != null && el.propertyType == SerializedPropertyType.ManagedReference)
                {
                    CopyCommandToClipboard(el.managedReferenceValue as CommandSpecBase);

                    if (TryGetClipboardJson(out string json))
                    {
                        int insertAt = idx + 1;
                        string propPath = commandsProp.propertyPath;

                        DelayModify("Duplicate Command", so =>
                        {
                            var fresh = so.FindProperty(propPath);
                            if (fresh == null || !fresh.isArray) return;

                            insertAt = Mathf.Clamp(insertAt, 0, fresh.arraySize);
                            fresh.InsertArrayElementAtIndex(insertAt);

                            var pastedEl = fresh.GetArrayElementAtIndex(insertAt);
                            pastedEl.managedReferenceValue = CreateCommandFromJson(json);

                            SyncMetaAfterInsert(pastedEl, targetTrack: _activeTrack);

                            _pendingCommandIndex = insertAt;
                            _commandsList = null;
                        });

                        e.Use();
                    }
                }
            }
        }
    }

    private void HandleStepShortcuts(SerializedProperty stepsProp)
    {
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_stepsList == null) return;

        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (EditorGUIUtility.editingTextField) return;

        bool mod = e.control || e.command;

        // Backspace delete step (when no command selected)
        if (!mod && e.keyCode == KeyCode.Backspace)
        {
            if (_commandsList != null && _commandsList.index >= 0)
                return;

            int idx = _stepsList.index;
            if (idx >= 0 && idx < stepsProp.arraySize)
            {
                DeleteSelectedStep(stepsProp);
                e.Use();
            }

            return;
        }

        // Copy step
        if (mod && e.keyCode == KeyCode.C)
        {
            if (_commandsList != null && _commandsList.index >= 0)
                return;

            int idx = _stepsList.index;
            if (idx >= 0 && idx < stepsProp.arraySize)
            {
                var step = targetSequence.nodes[_selectedNode].steps[idx];
                CopyStepToClipboard(step);
                e.Use();
            }

            return;
        }

        // Duplicate step
        if (mod && e.keyCode == KeyCode.D)
        {
            if (_commandsList != null && _commandsList.index >= 0)
                return;

            int idx = _stepsList.index;
            if (idx >= 0 && idx < stepsProp.arraySize)
            {
                int nodeIndex = _selectedNode;
                int srcIndex = idx;
                int insertAt = idx + 1;

                DelayModify("Duplicate Step", so =>
                {
                    var seq = (SequenceSpecSO)so.targetObject;
                    if (seq == null) return;
                    if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

                    var node = seq.nodes[nodeIndex];
                    node.steps ??= new List<StepSpec>();
                    if (srcIndex < 0 || srcIndex >= node.steps.Count) return;

                    insertAt = Mathf.Clamp(insertAt, 0, node.steps.Count);
                    node.steps.Insert(insertAt, CloneStepDeep(node.steps[srcIndex]));

                    _selectedStep = insertAt;
                    _stepsList = null;
                    _commandsList = null;
                });

                e.Use();
            }

            return;
        }

        // Paste step
        if (mod && e.keyCode == KeyCode.V)
        {
            if (_commandsList != null && _commandsList.index >= 0)
                return;

            if (!TryGetStepClipboardJson(out string json))
                return;

            int insertAt = stepsProp.arraySize;
            int sel = _stepsList.index;
            if (sel >= 0 && sel < stepsProp.arraySize)
                insertAt = sel + 1;

            int nodeIndex = _selectedNode;

            DelayModify("Paste Step", so =>
            {
                var seq = (SequenceSpecSO)so.targetObject;
                if (seq == null) return;
                if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

                var pasted = CreateStepFromJson(json);
                if (pasted == null) return;

                // Rebuild derived compiled later via ForceCompileAll()
                seq.nodes[nodeIndex].steps.Insert(Mathf.Clamp(insertAt, 0, seq.nodes[nodeIndex].steps.Count), pasted);

                _selectedStep = insertAt;
                _stepsList = null;
                _commandsList = null;
            });

            e.Use();
            return;
        }
    }

    private void DeleteSelectedStep(SerializedProperty stepsProp)
    {
        if (stepsProp == null || !stepsProp.isArray) return;
        if (_stepsList == null) return;

        int idx = _stepsList.index;
        if (idx < 0 || idx >= stepsProp.arraySize) return;

        string stepsPath = stepsProp.propertyPath;

        DeleteArrayElementByPath("Delete Step", stepsPath, idx, after: () =>
        {
            _selectedStep = Mathf.Clamp(idx - 1, 0, stepsProp.arraySize - 2);
            _stepsList = null;
            _commandsList = null;

            ForceCompileAll();
        });
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

        return gateProp.type;
    }

    private string SummarizeCommand(SerializedProperty cmdProp, int index)
    {
        if (cmdProp == null) return $"#{index} (null)";

        if (cmdProp.propertyType != SerializedPropertyType.ManagedReference)
            return $"#{index} (Non-ManagedReference!)";

        var typeName = GetManagedRefTypeName(cmdProp);
        if (string.IsNullOrEmpty(typeName)) typeName = "(null-ref)";

        string screenId = cmdProp.FindPropertyRelative("screenId")?.stringValue ?? "";
        string roleKey = cmdProp.FindPropertyRelative("roleKey")?.stringValue ?? "";

        if (!string.IsNullOrWhiteSpace(screenId) || !string.IsNullOrWhiteSpace(roleKey))
            return $"#{index} {typeName}  ({screenId}/{roleKey})";

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

    private readonly struct Origin
    {
        public readonly CommandTrackType track;
        public readonly int index;

        public Origin(CommandTrackType t, int i)
        {
            track = t;
            index = i;
        }
    }

    private Dictionary<long, Origin> BuildOriginMapForStep(SerializedProperty stepProp)
    {
        var map = new Dictionary<long, Origin>();

        var tracksProp = stepProp.FindPropertyRelative("tracks");
        if (tracksProp == null) return map;

        void ScanList(string name, CommandTrackType track)
        {
            var lp = tracksProp.FindPropertyRelative(name);
            if (lp == null || !lp.isArray) return;

            for (int i = 0; i < lp.arraySize; i++)
            {
                var el = lp.GetArrayElementAtIndex(i);
                if (el == null) continue;
                if (el.propertyType != SerializedPropertyType.ManagedReference) continue;

                long id = el.managedReferenceId;
                if (id == 0) continue;

                // first wins (should be unique anyway)
                if (!map.ContainsKey(id))
                    map[id] = new Origin(track, i);
            }
        }

        ScanList("interaction", CommandTrackType.Interaction);
        ScanList("setup", CommandTrackType.Setup);
        ScanList("motion", CommandTrackType.Motion);
        ScanList("dialogue", CommandTrackType.Dialogue);
        ScanList("fx", CommandTrackType.FX);

        return map;
    }

    private bool TryGetOrigin(SerializedProperty compiledEl, Dictionary<long, Origin> originMap, out Origin origin)
    {
        origin = default;

        if (compiledEl == null || compiledEl.propertyType != SerializedPropertyType.ManagedReference)
            return false;

        long id = compiledEl.managedReferenceId;
        if (id == 0) return false;

        return originMap != null && originMap.TryGetValue(id, out origin);
    }

    private static string PhaseShort(CommandPhase p) => p switch
    {
        CommandPhase.Setup => "S",
        CommandPhase.Motion => "M",
        CommandPhase.Dialogue => "D",
        CommandPhase.FX => "F",
        CommandPhase.Teardown => "T",
        _ => "?"
    };

    private static string TrackShort(CommandTrackType t) => t switch
    {
        CommandTrackType.Interaction => "I",
        CommandTrackType.Setup => "S",
        CommandTrackType.Motion => "M",
        CommandTrackType.Dialogue => "D",
        CommandTrackType.FX => "FX",
        _ => "?"
    };

    private static bool TryReadMeta(SerializedProperty cmdProp,
        out CommandTrackType metaTrack,
        out CommandPhase metaPhase,
        out bool blocking,
        out bool infinite,
        out float duration)
    {
        metaTrack = default;
        metaPhase = default;
        blocking = false;
        infinite = false;
        duration = 0f;

        if (cmdProp == null || cmdProp.propertyType != SerializedPropertyType.ManagedReference)
            return false;

        // meta field name can be "meta" or "Meta"
        var meta =
            cmdProp.FindPropertyRelative("_meta") ??
            cmdProp.FindPropertyRelative("meta") ??
            cmdProp.FindPropertyRelative("Meta");
        if (meta == null) return false;

        var tr = meta.FindPropertyRelative("track");
        var ph = meta.FindPropertyRelative("phase");
        var bh = meta.FindPropertyRelative("blockingHint");
        var ih = meta.FindPropertyRelative("infiniteHint");
        var dh = meta.FindPropertyRelative("durationHint");

        if (tr != null && tr.propertyType == SerializedPropertyType.Enum)
            metaTrack = (CommandTrackType)tr.intValue;

        if (ph != null && ph.propertyType == SerializedPropertyType.Enum)
            metaPhase = (CommandPhase)ph.intValue;

        if (bh != null && bh.propertyType == SerializedPropertyType.Boolean)
            blocking = bh.boolValue;
        if (ih != null && ih.propertyType == SerializedPropertyType.Boolean)
            infinite = ih.boolValue;
        if (dh != null && dh.propertyType == SerializedPropertyType.Float)
            duration = dh.floatValue;

        return true;
    }

    private string SummarizeCompiledLine(
        SerializedProperty cmdProp,
        int compiledIndex,
        Dictionary<long, Origin> originMap,
        out bool hasDrift,
        out bool missingOrigin)
    {
        hasDrift = false;
        missingOrigin = false;

        // Base info (type + ids)
        string baseLine = SummarizeCommand(cmdProp, compiledIndex);

        // Meta info
        bool hasMeta = TryReadMeta(cmdProp, out var metaTrack, out var metaPhase, out bool block, out bool inf,
            out float dur);

        // Origin info (where it lives in tracks)
        bool hasOrigin = TryGetOrigin(cmdProp, originMap, out var origin);
        if (!hasOrigin)
            missingOrigin = true;

        // Drift detection: Meta.track vs origin track
        if (hasMeta && hasOrigin)
        {
            if (metaTrack != origin.track)
                hasDrift = true;
        }

        // Build badges
        string phaseBadge = hasMeta ? $"P:{PhaseShort(metaPhase)}" : "P:?";
        string trackBadge = hasMeta ? $"T:{TrackShort(metaTrack)}" : "T:?";

        // Timing hints
        string time = "";
        if (hasMeta)
        {
            if (block) time += " [B]";
            if (inf) time += " [INF]";
            if (dur > 0f) time += $" [{dur:0.###}s]";
        }

        // Origin tag (for jump / debug)
        string originTag = hasOrigin ? $"  -> {TrackShort(origin.track)}#{origin.index}" : "  -> (missing)";

        // Drift marker
        string driftTag = hasDrift ? "  !!drift" : "";

        return $"#{compiledIndex} [{phaseBadge}][{trackBadge}]{time} {baseLine}{originTag}{driftTag}";
    }

    // ------------------------------
    // TypeCache
    // ------------------------------
    private static void CacheCommandTypes()
    {
        if (_cachedCommandTypes != null) return;

        var types = TypeCache.GetTypesDerivedFrom<CommandSpecBase>();
        _cachedCommandTypes = types
            .Where(t => t != null && !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private CommandSpecBase CreateCommandInstance(Type t)
    {
        var inst = (CommandSpecBase)Activator.CreateInstance(t);

        // bake meta if you have Editor_SetMeta + defaults
        try
        {
            inst?.Editor_SetMeta(CommandMetaDefaults.GetDefault(t));
        }
        catch
        {
            // ignore if not present yet
        }

        if (_autoFillIdsOnAdd && inst != null)
        {
            if (!string.IsNullOrWhiteSpace(_defaultScreenId))
                inst.screenId = _defaultScreenId;

            if (!string.IsNullOrWhiteSpace(_defaultWidgetId))
                inst.roleKey = _defaultWidgetId;
        }

        return inst;
    }

    // ------------------------------
    // Command add menu helper
    // ------------------------------
    private void ShowCommandAddMenu(
        string commandsPath,
        int insertAt,
        Action<Type> onSingle,
        Action<IReadOnlyList<Type>> onBatch,
        Action<GenericMenu> extendMenu = null)
    {
        CacheCommandTypes();

        // 1) Try external hook first (Sets / Recent / Category)
        bool handled = SequenceEditorMenuHooks.TryShowCommandMenu(
            commandTypes: _cachedCommandTypes,
            onAddSingleRequested: onSingle,
            onAddBatchRequested: onBatch,
            extendMenu: menu => { extendMenu?.Invoke(menu); });

        if (handled)
            return;

        // 2) Fallback: plain list (old behavior)
        var fallback = new GenericMenu();

        if (_cachedCommandTypes == null || _cachedCommandTypes.Count == 0)
        {
            fallback.AddDisabledItem(new GUIContent("No command types found"));
        }
        else
        {
            foreach (var t in _cachedCommandTypes)
            {
                var tt = t;
                fallback.AddItem(new GUIContent(tt.Name), false, () => onSingle(tt));
            }
        }

        extendMenu?.Invoke(fallback);
        fallback.ShowAsContext();
    }

    // ------------------------------
    // Foldout map helpers
    // ------------------------------
    private Dictionary<long, bool> GetFoldoutMap(string commandsPath)
    {
        if (string.IsNullOrEmpty(commandsPath))
            return null;

        if (!_commandFoldoutsByPath.TryGetValue(commandsPath, out var map) || map == null)
        {
            map = new Dictionary<long, bool>();
            _commandFoldoutsByPath[commandsPath] = map;
        }

        return map;
    }

    private Dictionary<long, bool> SnapshotCommandFoldouts(SerializedProperty commandsProp)
    {
        var map = new Dictionary<long, bool>();
        if (commandsProp == null || !commandsProp.isArray) return map;

        for (int i = 0; i < commandsProp.arraySize; i++)
        {
            var el = commandsProp.GetArrayElementAtIndex(i);
            if (el == null) continue;
            if (el.propertyType != SerializedPropertyType.ManagedReference) continue;

            long id = el.managedReferenceId;
            map[id] = el.isExpanded;
        }

        return map;
    }

    private void RestoreCommandFoldouts(SerializedProperty commandsProp, Dictionary<long, bool> map,
        long newIdToCollapse)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        for (int i = 0; i < commandsProp.arraySize; i++)
        {
            var el = commandsProp.GetArrayElementAtIndex(i);
            if (el == null) continue;
            if (el.propertyType != SerializedPropertyType.ManagedReference) continue;

            long id = el.managedReferenceId;

            if (id == newIdToCollapse)
            {
                el.isExpanded = false;
                continue;
            }

            if (map != null && map.TryGetValue(id, out bool expanded))
                el.isExpanded = expanded;
        }
    }

    private void SetAllCommandFoldouts(SerializedProperty commandsProp, bool expanded)
    {
        if (commandsProp == null || !commandsProp.isArray) return;

        string commandsPath = commandsProp.propertyPath;
        var map = GetFoldoutMap(commandsPath);
        if (map == null) return;

        var alive = new HashSet<long>();

        for (int i = 0; i < commandsProp.arraySize; i++)
        {
            var el = commandsProp.GetArrayElementAtIndex(i);
            if (el == null) continue;
            if (el.propertyType != SerializedPropertyType.ManagedReference) continue;

            long id = el.managedReferenceId;
            if (id == 0) continue;

            alive.Add(id);
            map[id] = expanded;
            el.isExpanded = expanded;
        }

        if (map.Count > alive.Count)
        {
            var toRemove = new List<long>();
            foreach (var kv in map)
            {
                if (!alive.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                map.Remove(toRemove[i]);
        }

        Repaint();
    }

    // ------------------------------
    // SerializeReference body draw
    // ------------------------------
    private static float GetManagedRefBodyHeight(SerializedProperty managedRef, float vSpace = 2f)
    {
        if (managedRef == null) return 0f;
        if (managedRef.propertyType != SerializedPropertyType.ManagedReference) return 0f;
        if (!managedRef.isExpanded) return 0f;

        float h = 0f;

        var it = managedRef.Copy();
        var end = it.GetEndProperty();

        bool hasChild = it.NextVisible(true);
        if (!hasChild) return 0f;

        while (!SerializedProperty.EqualContents(it, end))
        {
            h += EditorGUI.GetPropertyHeight(it, includeChildren: true) + vSpace;

            if (!it.NextVisible(false))
                break;
        }

        return h;
    }

    private static void DrawManagedRefBody(Rect rect, SerializedProperty managedRef, float vSpace = 2f)
    {
        if (managedRef == null) return;
        if (managedRef.propertyType != SerializedPropertyType.ManagedReference) return;
        if (!managedRef.isExpanded) return;

        var it = managedRef.Copy();
        var end = it.GetEndProperty();

        bool hasChild = it.NextVisible(true);
        if (!hasChild) return;

        float y = rect.y;

        using (new EditorGUI.IndentLevelScope(1))
        {
            while (!SerializedProperty.EqualContents(it, end))
            {
                float ph = EditorGUI.GetPropertyHeight(it, includeChildren: true);
                var r = new Rect(rect.x, y, rect.width, ph);

                EditorGUI.PropertyField(r, it, includeChildren: true);

                y += ph + vSpace;

                if (!it.NextVisible(false))
                    break;
            }
        }
    }

    private static bool IsSerializeReferenceCommandList(SerializedProperty commandsProp)
    {
        if (commandsProp == null || !commandsProp.isArray) return false;
        if (commandsProp.arraySize == 0) return true;

        var el = commandsProp.GetArrayElementAtIndex(0);
        return el != null && el.propertyType == SerializedPropertyType.ManagedReference;
    }

    // // ------------------------------
    // // Clipboard: Command
    // // ------------------------------
    // [Serializable]
    // private sealed class CommandClipboardBox : ScriptableObject
    // {
    //     public CommandSpecBase spec;
    // }

    private static void CopyCommandToClipboard(CommandSpecBase spec)
    {
        if (spec == null) return;

        var box = ScriptableObject.CreateInstance<CommandClipboardBox>();
        try
        {
            box.spec = spec;

            string json = EditorJsonUtility.ToJson(box);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[SequenceSpecEditor] Copy failed: json is empty");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = CommandClipboardPrefix + json;
            // Debug.Log($"Copy OK: {spec.GetType().Name}");
        }
        finally
        {
            DestroyImmediate(box);
        }
    }

    private static bool TryGetClipboardJson(out string json)
    {
        json = null;

        string buf = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(buf)) return false;
        if (!buf.StartsWith(CommandClipboardPrefix, StringComparison.Ordinal)) return false;

        json = buf.Substring(CommandClipboardPrefix.Length);
        return !string.IsNullOrEmpty(json);
    }

    private static CommandSpecBase CreateCommandFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var box = ScriptableObject.CreateInstance<CommandClipboardBox>();
        try
        {
            EditorJsonUtility.FromJsonOverwrite(json, box);
            return box.spec;
        }
        finally
        {
            DestroyImmediate(box);
        }
    }

    private void InsertCommandFactoryAt(
        string commandsPath,
        int insertAt,
        Func<CommandSpecBase> factory,
        bool scroll,
        bool expandNew)
    {
        DelayModify("Insert Command", so =>
        {
            var fresh = so.FindProperty(commandsPath);
            if (fresh == null || !fresh.isArray) return;

            var foldouts = SnapshotCommandFoldouts(fresh);

            int idx = Mathf.Clamp(insertAt, 0, fresh.arraySize);
            fresh.InsertArrayElementAtIndex(idx);

            var el = fresh.GetArrayElementAtIndex(idx);
            el.managedReferenceValue = factory?.Invoke();
            NormalizeInsertedCommandMeta(el, targetTrack: _activeTrack);
            SyncMetaAfterInsert(el, targetTrack: _activeTrack);

            long newId = el.managedReferenceId;

            RestoreCommandFoldouts(fresh, foldouts, newIdToCollapse: -1);

            el.isExpanded = expandNew;

            _pendingCommandIndex = idx;
            _commandsList = null;
            _scrollToNewCommand = scroll;
        });
    }

    // ------------------------------
    // Clipboard: Step
    // ------------------------------
    // [Serializable]
    // private sealed class StepClipboardBox : ScriptableObject
    // {
    //     public StepSpec step;
    // }

    private static void CopyStepToClipboard(StepSpec step)
    {
        if (step == null) return;

        var box = ScriptableObject.CreateInstance<StepClipboardBox>();
        try
        {
            box.step = step;
            string json = EditorJsonUtility.ToJson(box);
            EditorGUIUtility.systemCopyBuffer = StepClipboardPrefix + json;
        }
        finally
        {
            DestroyImmediate(box);
        }
    }

    private static bool TryGetStepClipboardJson(out string json)
    {
        json = null;

        string buf = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(buf)) return false;
        if (!buf.StartsWith(StepClipboardPrefix, StringComparison.Ordinal)) return false;

        json = buf.Substring(StepClipboardPrefix.Length);
        return !string.IsNullOrEmpty(json);
    }

    private static StepSpec CreateStepFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var box = ScriptableObject.CreateInstance<StepClipboardBox>();
        try
        {
            EditorJsonUtility.FromJsonOverwrite(json, box);
            return box.step;
        }
        finally
        {
            DestroyImmediate(box);
        }
    }

    // ------------------------------
    // Search helper (very light)
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

            // search in compiled summary (runtime)
            var compiled = step.FindPropertyRelative("compiled");
            if (compiled == null || !compiled.isArray) continue;

            for (int ci = 0; ci < compiled.arraySize; ci++)
            {
                var cmd = compiled.GetArrayElementAtIndex(ci);
                string summary = SummarizeCommand(cmd, ci);
                if (!string.IsNullOrEmpty(summary) && summary.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        return false;
    }

    // ------------------------------
    // Misc / utilities
    // ------------------------------
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

    private void ShowContextMenu(Action<GenericMenu> build)
    {
        var menu = new GenericMenu();
        build?.Invoke(menu);
        menu.ShowAsContext();
    }

    private void DeleteArrayElementByPath(string undoLabel, string arrayPropPath, int index, Action after = null)
    {
        DelayModify(undoLabel, so =>
        {
            var arr = so.FindProperty(arrayPropPath);
            if (arr == null || !arr.isArray) return;
            if (index < 0 || index >= arr.arraySize) return;

            arr.DeleteArrayElementAtIndex(index);

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

    private static int TrackToIndex(CommandTrackType t) => t switch
    {
        CommandTrackType.Interaction => 0,
        CommandTrackType.Setup => 1,
        CommandTrackType.Motion => 2,
        CommandTrackType.Dialogue => 3,
        CommandTrackType.FX => 4,
        _ => 3
    };

    private static CommandTrackType IndexToTrack(int i) => i switch
    {
        0 => CommandTrackType.Interaction,
        1 => CommandTrackType.Setup,
        2 => CommandTrackType.Motion,
        3 => CommandTrackType.Dialogue,
        4 => CommandTrackType.FX,
        _ => CommandTrackType.Dialogue
    };

    // ------------------------------
    // Data constructors / deep clone
    // ------------------------------
    private static StepSpec CreateBlankStep()
    {
        return new StepSpec
        {
            editorName = "",
            gate = default,
            tracks = new StepTracks(),
            compiled = new List<CommandSpecBase>(),
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

    private static StepSpec CloneStepDeep(StepSpec src)
    {
        if (src == null) return CreateBlankStep();

        var dst = new StepSpec
        {
            editorName = src.editorName,
            gate = src.gate,
            tracks = new StepTracks(),
            compiled = new List<CommandSpecBase>() // derived; will be rebuilt
        };

        // clone tracks
        if (src.tracks != null)
        {
            CloneListInto(src.tracks.interaction, dst.tracks.interaction);
            CloneListInto(src.tracks.setup, dst.tracks.setup);
            CloneListInto(src.tracks.motion, dst.tracks.motion);
            CloneListInto(src.tracks.dialogue, dst.tracks.dialogue);
            CloneListInto(src.tracks.fx, dst.tracks.fx);
        }

        return dst;
    }

    private static void CloneListInto(List<CommandSpecBase> src, List<CommandSpecBase> dst)
    {
        if (dst == null) return;
        dst.Clear();

        if (src == null) return;
        foreach (var c in src)
            dst.Add(CloneCommandDeep(c));
    }

    private static CommandSpecBase CloneCommandDeep(CommandSpecBase src)
    {
        if (src == null) return null;

        var t = src.GetType();
        var clone = (CommandSpecBase)Activator.CreateInstance(t);

        string json = EditorJsonUtility.ToJson(src);
        EditorJsonUtility.FromJsonOverwrite(json, clone);

        return clone;
    }

    private void JumpToOrigin(SerializedProperty stepProp, CommandTrackType track, int index)
    {
        // 1) switch track tab
        _activeTrack = track;

        // 2) reset command list cache so it rebuilds for the new track
        _commandsList = null;
        _commandsPropPath = null;

        // 3) select the command in that track list
        _pendingCommandIndex = Mathf.Max(0, index);

        // 4) approximate scroll: move the right panel down roughly to the row
        // (exact rect scroll is hard with variable element heights, but this works well enough)
        _scrollToCommandIndex = true;
        _scrollTargetCommandIndex = _pendingCommandIndex;

        // also ensure we're on this step (defensive)
        _hasSelectedCommand = true;

        Repaint();
    }

    private void NormalizeInsertedCommandMeta(SerializedProperty cmdProp, CommandTrackType targetTrack)
    {
        var meta = cmdProp.FindPropertyRelative("_meta") ??
                   cmdProp.FindPropertyRelative("meta") ??
                   cmdProp.FindPropertyRelative("Meta");
        if (meta == null) return;

        var tr = meta.FindPropertyRelative("track");
        if (tr != null && tr.propertyType == SerializedPropertyType.Enum)
            tr.intValue = (int)targetTrack;

        // (선택) phase도 자동화하려면 여기서 phase도 바꿔주기
        // var ph = meta.FindPropertyRelative("phase");
        // ph.enumValueIndex = ...
    }

    private void SyncMetaAfterInsert(SerializedProperty cmdProp, CommandTrackType targetTrack)
    {
        if (cmdProp == null || cmdProp.propertyType != SerializedPropertyType.ManagedReference)
            return;

        // managedReferenceValue로 실제 인스턴스를 잡을 수 있음(에디터).
        var spec = cmdProp.managedReferenceValue as CommandSpecBase;
        if (spec == null) return;

        // 1) 어트리뷰트 기본값 생성
        var meta = CommandMetaDefaults.GetDefault(spec.GetType());

        // 2) 목적지 트랙 강제 보정(드리프트 방지)
        meta.track = targetTrack;

        // (선택) phase는 정책에 따라:
        // - 그대로 meta.phase(어트리뷰트값) 유지하는게 보통 정답
        // - 트랙에 따라 phase를 강제하고 싶으면 여기서 덮어써도 됨

        spec.Editor_SetMeta(meta);
    }

    private void FixDriftForCurrentStep()
    {
        int nodeIndex = _selectedNode;
        int stepIndex = _selectedStep;

        DelayModify("Fix Drift (Step)", so =>
        {
            var seq = (SequenceSpecSO)so.targetObject;
            if (seq == null) return;
            if (seq.nodes == null) return;
            if (nodeIndex < 0 || nodeIndex >= seq.nodes.Count) return;

            var node = seq.nodes[nodeIndex];
            if (node.steps == null) return;
            if (stepIndex < 0 || stepIndex >= node.steps.Count) return;

            var step = node.steps[stepIndex];
            if (step.tracks == null) return;

            // 각 트랙 리스트별로 meta 다시 굽기
            RebakeMetaList(step.tracks.interaction, CommandTrackType.Interaction);
            RebakeMetaList(step.tracks.setup, CommandTrackType.Setup);
            RebakeMetaList(step.tracks.motion, CommandTrackType.Motion);
            RebakeMetaList(step.tracks.dialogue, CommandTrackType.Dialogue);
            RebakeMetaList(step.tracks.fx, CommandTrackType.FX);
        }, forceRebuild: false);
    }

    private static void RebakeMetaList(List<CommandSpecBase> list, CommandTrackType track)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var spec = list[i];
            if (spec == null) continue;

            var meta = CommandMetaDefaults.GetDefault(spec.GetType());
            meta.track = track; //

            spec.Editor_SetMeta(meta);
        }
    }
}
#endif