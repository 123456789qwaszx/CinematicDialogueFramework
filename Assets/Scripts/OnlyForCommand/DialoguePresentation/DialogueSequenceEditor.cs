// using System;
// using System.Collections.Generic;
// using UnityEditor;
// using UnityEngine;
//
// namespace Modules.Dialogue.Editor
// {
//     /// <summary>
//     /// 에디터에서만 쓰는 워크스페이스 SO
//     /// 순수하게 입력용 버퍼 역할 (런타임에서 직접 사용하지 않음)
//     /// </summary>
//     internal sealed class DialogueSequenceWorkspace : ScriptableObject
//     {
//         [Serializable]
//         internal sealed class DialogueLineDraft
//         {
//             public string speakerId;
//
//             public Expression expression = Expression.Default;
//             public DialoguePosition position = DialoguePosition.Left;
//
//             [TextArea(2, 6)]
//             public string text;
//         }
//
//         [Serializable]
//         internal sealed class SituationRow
//         {
//             public string situationKey;
//             public List<string> sequenceKeys = new(); // legacy (예전 문자열 리스트 → text로 migrate)
//             public List<DialogueLineDraft> lines = new();
//         }
//
//         public List<SituationRow> rows = new();
//     }
//
//     /// <summary>
//     /// 새 DialogueSequenceData 구조에 맞춘 시퀀스 에디터
//     /// - SituationKey(=SituationId) 단위로 대사 라인을 입력
//     /// - Bake 시 DialogueSequenceData.asset로 변환
//     /// </summary>
//     public sealed class DialogueSequenceEditorWindow : EditorWindow
//     {
//         private const string DefaultWorkspacePath = "Assets/Dialogue/DialogueSequenceWorkspace.asset";
//         private const string DefaultBakePath      = "Assets/Dialogue/DialogueSequenceData.asset";
//
//         private const string RenameControlName = "InlineRenameSituationKey";
//
//         private DialogueSequenceWorkspace _workspace;
//         private int _selectedSituationIndex = -1;
//
//         private Vector2 _leftScroll;
//         private Vector2 _rightScroll;
//
//         // 입력 버퍼
//         private string _newSituationKey = "";
//         private string _newSpeakerId = "";
//         private string _newLineText = "";
//         private Expression _newExpression = Expression.Default;
//         private DialoguePosition _newPosition = DialoguePosition.Left;
//
//         // 인라인 리네임 상태
//         private int _renamingIndex = -1;
//         private string _renamingOriginal = "";
//         private string _renamingText = "";
//
//         [MenuItem("Tools/Dialogue/Sequence Editor")]
//         private static void OpenWindow()
//         {
//             var w = GetWindow<DialogueSequenceEditorWindow>("Dialogue Sequence Editor");
//             w.minSize = new Vector2(1000, 550);
//             w.LoadWorkspace();
//             w.Show();
//         }
//
//         private void OnEnable()
//         {
//             LoadWorkspace();
//         }
//
//         private void OnGUI()
//         {
//             if (_workspace == null)
//             {
//                 EditorGUILayout.HelpBox("Workspace not loaded.", MessageType.Warning);
//                 if (GUILayout.Button("Load Workspace"))
//                     LoadWorkspace();
//                 return;
//             }
//
//             HandleInlineRenameHotkeys(); // Enter/Esc 처리
//
//             using (new EditorGUILayout.HorizontalScope())
//             {
//                 DrawSituationPanel(); // 왼쪽
//                 DrawLinesPanel();     // 오른쪽
//             }
//         }
//
//         private void LoadWorkspace()
//         {
//             _workspace = AssetDatabase.LoadAssetAtPath<DialogueSequenceWorkspace>(DefaultWorkspacePath);
//             if (_workspace == null)
//             {
//                 EnsureFolder("Assets/Dialogue");
//
//                 _workspace = CreateInstance<DialogueSequenceWorkspace>();
//                 AssetDatabase.CreateAsset(_workspace, DefaultWorkspacePath);
//                 AssetDatabase.SaveAssets();
//                 AssetDatabase.Refresh();
//             }
//
//             MigrateLegacyIfNeeded();
//         }
//
//         private void SaveWorkspace()
//         {
//             if (_workspace == null) return;
//             EditorUtility.SetDirty(_workspace);
//             AssetDatabase.SaveAssets();
//         }
//
//         // ====== SituationKey 편집 ======
//         private void AddSituationKey(string key)
//         {
//             key = (key ?? string.Empty).Trim();
//
//             // 빈 입력이면 기본 키 자동 생성
//             if (string.IsNullOrEmpty(key))
//                 key = GenerateDefaultSituationKey();
//
//             if (FindSituationIndex(key) >= 0)
//             {
//                 ShowNotification(new GUIContent($"Duplicate SituationKey: {key}"));
//                 return;
//             }
//
//             _workspace.rows.Add(new DialogueSequenceWorkspace.SituationRow
//             {
//                 situationKey = key,
//                 sequenceKeys = new List<string>(),
//                 lines = new List<DialogueSequenceWorkspace.DialogueLineDraft>()
//             });
//
//             _selectedSituationIndex = _workspace.rows.Count - 1;
//             SaveWorkspace();
//         }
//
//         private void RemoveSituationKey(string key)
//         {
//             int idx = FindSituationIndex(key);
//             if (idx < 0) return;
//
//             // 리네임 중인 대상이 삭제되면 리네임 해제
//             if (_renamingIndex == idx)
//                 CancelInlineRename();
//
//             _workspace.rows.RemoveAt(idx);
//
//             if (_selectedSituationIndex >= _workspace.rows.Count)
//                 _selectedSituationIndex = _workspace.rows.Count - 1;
//
//             SaveWorkspace();
//         }
//
//         private void RenameSituationKey(string from, string to)
//         {
//             from = (from ?? string.Empty).Trim();
//             to   = (to ?? string.Empty).Trim();
//             if (string.IsNullOrEmpty(from)) return;
//
//             if (string.IsNullOrEmpty(to))
//                 to = GenerateDefaultSituationKey();
//
//             int fromIdx = FindSituationIndex(from);
//             if (fromIdx < 0) return;
//
//             if (!string.Equals(from, to, StringComparison.Ordinal) && FindSituationIndex(to) >= 0)
//             {
//                 ShowNotification(new GUIContent($"Duplicate SituationKey: {to}"));
//                 return;
//             }
//
//             _workspace.rows[fromIdx].situationKey = to;
//             SaveWorkspace();
//         }
//
//         // ====== (신규) 인라인 리네임 ======
//         private void BeginInlineRename(int index)
//         {
//             if (index < 0 || index >= _workspace.rows.Count) return;
//
//             _renamingIndex = index;
//             _renamingOriginal = _workspace.rows[index].situationKey ?? "";
//             _renamingText = _renamingOriginal;
//
//             // 다음 OnGUI에서 포커스
//             EditorApplication.delayCall += () =>
//             {
//                 GUI.FocusControl(RenameControlName);
//                 Repaint();
//             };
//         }
//
//         private void CommitInlineRename()
//         {
//             if (_renamingIndex < 0 || _renamingIndex >= _workspace.rows.Count) { CancelInlineRename(); return; }
//
//             string to = (_renamingText ?? "").Trim();
//             string from = _renamingOriginal;
//
//             if (string.IsNullOrEmpty(to))
//                 to = GenerateDefaultSituationKey();
//
//             // 중복 체크 (자기 자신 제외)
//             int dup = FindSituationIndex(to);
//             if (dup >= 0 && dup != _renamingIndex)
//             {
//                 ShowNotification(new GUIContent($"Duplicate SituationKey: {to}"));
//                 // 계속 편집 상태 유지
//                 EditorApplication.delayCall += () =>
//                 {
//                     GUI.FocusControl(RenameControlName);
//                     Repaint();
//                 };
//                 return;
//             }
//
//             // 실제 반영
//             _workspace.rows[_renamingIndex].situationKey = to;
//             SaveWorkspace();
//
//             CancelInlineRename();
//         }
//
//         private void CancelInlineRename()
//         {
//             _renamingIndex = -1;
//             _renamingOriginal = "";
//             _renamingText = "";
//             GUI.FocusControl(null);
//         }
//
//         private void HandleInlineRenameHotkeys()
//         {
//             if (_renamingIndex < 0) return;
//
//             var e = Event.current;
//             if (e.type != EventType.KeyDown) return;
//
//             if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
//             {
//                 e.Use();
//                 CommitInlineRename();
//             }
//             else if (e.keyCode == KeyCode.Escape)
//             {
//                 e.Use();
//                 CancelInlineRename();
//             }
//         }
//
//         // ====== 라인 편집 ======
//         private void AddLineToSituation(
//             string situationKey,
//             string speakerId,
//             Expression expression,
//             DialoguePosition position,
//             string text)
//         {
//             int idx = FindSituationIndex(situationKey);
//             if (idx < 0) return;
//
//             speakerId = (speakerId ?? string.Empty).Trim();
//             text      = (text ?? string.Empty);
//
//             if (string.IsNullOrWhiteSpace(text)) return;
//
//             var row = _workspace.rows[idx];
//             row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//
//             row.lines.Add(new DialogueSequenceWorkspace.DialogueLineDraft
//             {
//                 speakerId = speakerId,
//                 expression = expression,
//                 position = position,
//                 text = text
//             });
//
//             SaveWorkspace();
//         }
//
//         private void RemoveLineFromSituation(string situationKey, int index)
//         {
//             int idx = FindSituationIndex(situationKey);
//             if (idx < 0) return;
//
//             var row = _workspace.rows[idx];
//             row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//
//             if (index < 0 || index >= row.lines.Count) return;
//             row.lines.RemoveAt(index);
//             SaveWorkspace();
//         }
//
//         private void MoveLineOrder(string situationKey, int fromIndex, int toIndex)
//         {
//             int idx = FindSituationIndex(situationKey);
//             if (idx < 0) return;
//
//             var row = _workspace.rows[idx];
//             row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//
//             if (fromIndex < 0 || fromIndex >= row.lines.Count) return;
//             if (toIndex < 0 || toIndex >= row.lines.Count) return;
//             if (fromIndex == toIndex) return;
//
//             var item = row.lines[fromIndex];
//             row.lines.RemoveAt(fromIndex);
//             row.lines.Insert(toIndex, item);
//             SaveWorkspace();
//         }
//
//         private void DuplicateLineEntry(string situationKey, int index)
//         {
//             int idx = FindSituationIndex(situationKey);
//             if (idx < 0) return;
//
//             var row = _workspace.rows[idx];
//             row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//
//             if (index < 0 || index >= row.lines.Count) return;
//
//             var src = row.lines[index];
//             row.lines.Insert(index + 1, new DialogueSequenceWorkspace.DialogueLineDraft
//             {
//                 speakerId = src.speakerId,
//                 expression = src.expression,
//                 position = src.position,
//                 text = src.text
//             });
//
//             SaveWorkspace();
//         }
//
//         // ====== 검증/빌드 ======
//         private bool ValidateAll()
//         {
//             if (_workspace == null) return false;
//
//             var set = new HashSet<string>(StringComparer.Ordinal);
//             foreach (var row in _workspace.rows)
//             {
//                 if (row == null) return false;
//
//                 var key = (row.situationKey ?? string.Empty).Trim();
//                 if (string.IsNullOrEmpty(key)) return false;
//                 if (!set.Add(key)) return false;
//             }
//             return true;
//         }
//
//         private void ShowValidationReport()
//         {
//             if (_workspace == null) return;
//
//             var errors = new List<string>();
//             var set = new HashSet<string>(StringComparer.Ordinal);
//
//             for (int i = 0; i < _workspace.rows.Count; i++)
//             {
//                 var row = _workspace.rows[i];
//                 if (row == null) { errors.Add($"Row[{i}] is null"); continue; }
//
//                 var key = (row.situationKey ?? "").Trim();
//                 if (string.IsNullOrEmpty(key)) errors.Add($"Row[{i}] has empty SituationKey");
//                 else if (!set.Add(key)) errors.Add($"Duplicate SituationKey: {key}");
//
//                 row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//                 for (int li = 0; li < row.lines.Count; li++)
//                 {
//                     var line = row.lines[li];
//                     if (line == null) { errors.Add($"{key}: Line[{li}] is null"); continue; }
//                     if (string.IsNullOrWhiteSpace(line.text))
//                         errors.Add($"{key}: Line[{li}] text is empty");
//                 }
//             }
//
//             if (errors.Count == 0)
//                 EditorUtility.DisplayDialog("Validation", "OK (no errors)", "Close");
//             else
//                 EditorUtility.DisplayDialog("Validation Errors", string.Join("\n", errors), "Close");
//         }
//
//         /// <summary>
//         /// Workspace → DialogueSequenceData 로 Bake
//         /// </summary>
//         private void BakeRuntimeData()
//         {
//             if (!ValidateAll())
//             {
//                 ShowValidationReport();
//                 return;
//             }
//
//             bool ok = EditorUtility.DisplayDialog(
//                 "Bake Runtime Data",
//                 "DialogueSequenceData.asset 를 갱신합니다.\n(기존 데이터가 덮어써집니다)\n\n진행할까요?",
//                 "Bake",
//                 "Cancel");
//
//             if (!ok) return;
//
//             EnsureFolder("Assets/Dialogue");
//
//             var data = AssetDatabase.LoadAssetAtPath<DialogueSequenceData>(DefaultBakePath);
//             if (data == null)
//             {
//                 data = CreateInstance<DialogueSequenceData>();
//                 AssetDatabase.CreateAsset(data, DefaultBakePath);
//             }
//
//             var bakedSituations = new List<SituationEntry>(_workspace.rows.Count);
//
//             foreach (var row in _workspace.rows)
//             {
//                 var entry = new SituationEntry
//                 {
//                     situationId = (row.situationKey ?? "").Trim(),
//                     lines = new List<DialogueLine>()
//                 };
//
//                 row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//                 foreach (var l in row.lines)
//                 {
//                     if (l == null) continue;
//                     entry.lines.Add(new DialogueLine
//                     {
//                         speakerId = (l.speakerId ?? "").Trim(),
//                         expression = l.expression,
//                         position = l.position,
//                         text = l.text ?? ""
//                     });
//                 }
//
//                 bakedSituations.Add(entry);
//             }
//
//             data.situations = bakedSituations;
//
//             EditorUtility.SetDirty(data);
//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();
//
//             EditorGUIUtility.PingObject(data);
//             ShowNotification(new GUIContent($"Baked: {DefaultBakePath}"));
//         }
//
//         // =====================================================
//         // UI
//         // =====================================================
//
//         private void DrawSituationPanel()
//         {
//             using (new EditorGUILayout.VerticalScope(GUILayout.Width(360)))
//             {
//                 EditorGUILayout.LabelField("Situation Keys", EditorStyles.boldLabel);
//
//                 using (new EditorGUILayout.HorizontalScope())
//                 {
//                     _newSituationKey = EditorGUILayout.TextField(_newSituationKey);
//
//                     if (GUILayout.Button("Add", GUILayout.Width(60)))
//                     {
//                         AddSituationKey(_newSituationKey);
//                         _newSituationKey = "";
//                         GUI.FocusControl(null);
//                     }
//                 }
//
//                 _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
//
//                 for (int i = 0; i < _workspace.rows.Count; i++)
//                 {
//                     var row = _workspace.rows[i];
//                     if (row == null) continue;
//
//                     bool selected = (i == _selectedSituationIndex);
//
//                     using (new EditorGUILayout.HorizontalScope())
//                     {
//                         // ---- 인라인 리네임 상태면 TextField로 ----
//                         if (_renamingIndex == i)
//                         {
//                             GUI.SetNextControlName(RenameControlName);
//                             _renamingText = EditorGUILayout.TextField(_renamingText);
//
//                             // 삭제 버튼은 유지
//                             if (GUILayout.Button("X", GUILayout.Width(24)))
//                             {
//                                 RemoveSituationKey(row.situationKey);
//                                 GUIUtility.ExitGUI();
//                             }
//                         }
//                         else
//                         {
//                             // Toggle("Button")으로 선택 강조
//                             bool now = GUILayout.Toggle(selected, row.situationKey, "Button");
//                             Rect lastRect = GUILayoutUtility.GetLastRect();
//
//                             if (now && !selected)
//                                 _selectedSituationIndex = i;
//
//                             // 더블클릭 시 인라인 리네임 시작
//                             var e = Event.current;
//                             if (e.type == EventType.MouseDown && e.clickCount == 2 && lastRect.Contains(e.mousePosition))
//                             {
//                                 _selectedSituationIndex = i;
//                                 BeginInlineRename(i);
//                                 e.Use();
//                             }
//
//                             if (GUILayout.Button("X", GUILayout.Width(24)))
//                             {
//                                 RemoveSituationKey(row.situationKey);
//                                 GUIUtility.ExitGUI();
//                             }
//                         }
//                     }
//                 }
//
//                 EditorGUILayout.EndScrollView();
//
//                 GUILayout.Space(10);
//                 DrawWorkspaceActions();
//             }
//         }
//
//         private void DrawWorkspaceActions()
//         {
//             using (new EditorGUILayout.VerticalScope("box"))
//             {
//                 EditorGUILayout.LabelField("Workspace Actions (주의)", EditorStyles.miniBoldLabel);
//                 EditorGUILayout.HelpBox(
//                     "Save / Validate / Bake는 '데이터 편집'이 아니라\n워크스페이스/런타임 에셋을 건드리는 작업입니다.\n\n(리네임 중이면 Enter=확정, Esc=취소)",
//                     MessageType.Warning);
//
//                 using (new EditorGUILayout.HorizontalScope())
//                 {
//                     if (GUILayout.Button("Save Workspace"))
//                         SaveWorkspace();
//
//                     if (GUILayout.Button("Validate"))
//                         ShowValidationReport();
//                 }
//
//                 GUILayout.Space(4);
//
//                 using (new EditorGUILayout.HorizontalScope())
//                 {
//                     GUILayout.FlexibleSpace();
//                     if (GUILayout.Button("Bake Runtime Data", GUILayout.Width(180), GUILayout.Height(28)))
//                         BakeRuntimeData();
//                 }
//
//                 GUILayout.Space(2);
//                 if (GUILayout.Button("Ping Workspace"))
//                     EditorGUIUtility.PingObject(_workspace);
//             }
//         }
//
//         private void DrawLinesPanel()
//         {
//             using (new EditorGUILayout.VerticalScope())
//             {
//                 EditorGUILayout.LabelField("Dialogue Lines (typed in editor)", EditorStyles.boldLabel);
//
//                 if (_selectedSituationIndex < 0 || _selectedSituationIndex >= _workspace.rows.Count)
//                 {
//                     EditorGUILayout.HelpBox("Select a SituationKey on the left.", MessageType.Info);
//                     return;
//                 }
//
//                 var row = _workspace.rows[_selectedSituationIndex];
//                 if (row == null) return;
//
//                 row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//
//                 EditorGUILayout.LabelField($"Situation: {row.situationKey}", EditorStyles.miniBoldLabel);
//
//                 EditorGUILayout.Space(6);
//                 using (new EditorGUILayout.VerticalScope("box"))
//                 {
//                     EditorGUILayout.LabelField("Add Line", EditorStyles.miniBoldLabel);
//
//                     using (new EditorGUILayout.HorizontalScope())
//                     {
//                         EditorGUILayout.LabelField("SpeakerId", GUILayout.Width(70));
//                         _newSpeakerId = EditorGUILayout.TextField(_newSpeakerId);
//                     }
//
//                     using (new EditorGUILayout.HorizontalScope())
//                     {
//                         EditorGUILayout.LabelField("Expression", GUILayout.Width(70));
//                         _newExpression = (Expression)EditorGUILayout.EnumPopup(_newExpression);
//
//                         EditorGUILayout.LabelField("Position", GUILayout.Width(70));
//                         _newPosition = (DialoguePosition)EditorGUILayout.EnumPopup(_newPosition);
//                     }
//
//                     EditorGUILayout.LabelField("Text");
//                     _newLineText = EditorGUILayout.TextArea(_newLineText, GUILayout.MinHeight(60));
//
//                     using (new EditorGUILayout.HorizontalScope())
//                     {
//                         GUILayout.FlexibleSpace();
//                         if (GUILayout.Button("Add", GUILayout.Width(120)))
//                         {
//                             AddLineToSituation(row.situationKey, _newSpeakerId, _newExpression, _newPosition, _newLineText);
//                             _newLineText = "";
//                             GUI.FocusControl(null);
//                         }
//                     }
//                 }
//
//                 EditorGUILayout.Space(8);
//                 _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
//
//                 for (int i = 0; i < row.lines.Count; i++)
//                 {
//                     var line = row.lines[i];
//                     if (line == null) continue;
//
//                     using (new EditorGUILayout.VerticalScope("box"))
//                     {
//                         using (new EditorGUILayout.HorizontalScope())
//                         {
//                             EditorGUILayout.LabelField($"[{i:00}]", GUILayout.Width(40));
//                             EditorGUILayout.LabelField("SpeakerId", GUILayout.Width(70));
//                             line.speakerId = EditorGUILayout.TextField(line.speakerId);
//
//                             GUILayout.FlexibleSpace();
//
//                             if (GUILayout.Button("Dup", GUILayout.Width(44)))
//                             {
//                                 DuplicateLineEntry(row.situationKey, i);
//                                 GUIUtility.ExitGUI();
//                             }
//
//                             if (GUILayout.Button("↑", GUILayout.Width(28)) && i > 0)
//                             {
//                                 MoveLineOrder(row.situationKey, i, i - 1);
//                                 GUIUtility.ExitGUI();
//                             }
//
//                             if (GUILayout.Button("↓", GUILayout.Width(28)) && i < row.lines.Count - 1)
//                             {
//                                 MoveLineOrder(row.situationKey, i, i + 1);
//                                 GUIUtility.ExitGUI();
//                             }
//
//                             if (GUILayout.Button("X", GUILayout.Width(24)))
//                             {
//                                 RemoveLineFromSituation(row.situationKey, i);
//                                 GUIUtility.ExitGUI();
//                             }
//                         }
//
//                         using (new EditorGUILayout.HorizontalScope())
//                         {
//                             EditorGUILayout.LabelField("Expression", GUILayout.Width(70));
//                             line.expression = (Expression)EditorGUILayout.EnumPopup(line.expression);
//
//                             EditorGUILayout.LabelField("Position", GUILayout.Width(70));
//                             line.position = (DialoguePosition)EditorGUILayout.EnumPopup(line.position);
//                         }
//
//                         EditorGUILayout.LabelField("Text");
//                         line.text = EditorGUILayout.TextArea(line.text, GUILayout.MinHeight(60));
//                     }
//                 }
//
//                 EditorGUILayout.EndScrollView();
//
//                 if (GUI.changed)
//                     SaveWorkspace();
//             }
//         }
//
//         // =====================================================
//         // Helpers
//         // =====================================================
//
//         private int FindSituationIndex(string key)
//         {
//             key = (key ?? string.Empty).Trim();
//             for (int i = 0; i < _workspace.rows.Count; i++)
//             {
//                 var row = _workspace.rows[i];
//                 if (row == null) continue;
//                 if (string.Equals((row.situationKey ?? "").Trim(), key, StringComparison.Ordinal))
//                     return i;
//             }
//             return -1;
//         }
//
//         private string GenerateDefaultSituationKey()
//         {
//             int n = 1;
//             while (true)
//             {
//                 string candidate = $"SIT_{n:000}";
//                 if (FindSituationIndex(candidate) < 0)
//                     return candidate;
//                 n++;
//             }
//         }
//
//         private static void EnsureFolder(string assetFolderPath)
//         {
//             var parts = assetFolderPath.Split('/');
//             if (parts.Length < 2) return;
//
//             string cur = parts[0]; // "Assets"
//             for (int i = 1; i < parts.Length; i++)
//             {
//                 string next = $"{cur}/{parts[i]}";
//                 if (!AssetDatabase.IsValidFolder(next))
//                     AssetDatabase.CreateFolder(cur, parts[i]);
//                 cur = next;
//             }
//         }
//
//         /// <summary>
//         /// 예전 sequenceKeys(문자열 리스트)를 lines로 마이그레이션
//         /// </summary>
//         private void MigrateLegacyIfNeeded()
//         {
//             if (_workspace == null || _workspace.rows == null) return;
//
//             bool changed = false;
//
//             foreach (var row in _workspace.rows)
//             {
//                 if (row == null) continue;
//                 row.lines ??= new List<DialogueSequenceWorkspace.DialogueLineDraft>();
//                 row.sequenceKeys ??= new List<string>();
//
//                 if (row.lines.Count == 0 && row.sequenceKeys.Count > 0)
//                 {
//                     foreach (var s in row.sequenceKeys)
//                     {
//                         if (string.IsNullOrWhiteSpace(s)) continue;
//                         row.lines.Add(new DialogueSequenceWorkspace.DialogueLineDraft
//                         {
//                             speakerId = "",
//                             expression = Expression.Default,
//                             position = DialoguePosition.Left,
//                             text = s
//                         });
//                     }
//                     changed = true;
//                 }
//             }
//
//             if (changed)
//                 SaveWorkspace();
//         }
//     }
// }
