using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 시퀀스 / 스피커 / 초상화 리소스를 로딩/캐싱하는 전역 리포지토리.
/// - 더 이상 재생(Play) / 게이트 / 입력 처리는 하지 않는다.
/// - 순수하게 "데이터 조회"만 담당.
/// </summary>
public sealed class DialogueRepository : Singleton<DialogueRepository>
{
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = true;

    [Header("Resources Path (Optional)")]
    [SerializeField] private string sequenceResourcesPath = "Dialogue/Sequence";
    [SerializeField] private string speakerResourcesPath  = "Dialogue/Speaker";

    // ===== Data lookup =====
    private readonly Dictionary<string, DialogueSequenceData> _sequencesById 
        = new Dictionary<string, DialogueSequenceData>(StringComparer.Ordinal);

    private readonly Dictionary<string, DialogueSpeakerData> _speakersById
        = new Dictionary<string, DialogueSpeakerData>(StringComparer.Ordinal);

    private readonly Dictionary<string, Dictionary<Expression, Sprite>> _portraitCache
        = new Dictionary<string, Dictionary<Expression, Sprite>>(StringComparer.Ordinal);

    #region Unity lifecycle

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // 중복 인스턴스면 바로 리턴

        InitData();
    }

    #endregion

    #region Init

    /// <summary>
    /// Resources에서 모든 시퀀스/스피커 데이터를 다시 로딩한다.
    /// </summary>
    public void InitData()
    {
        _sequencesById.Clear();
        _speakersById.Clear();
        _portraitCache.Clear();

        LoadAllSequences();
        LoadAllSpeakers();
    }

    private void LoadAllSequences()
    {
        DialogueSequenceData[] allSequences =
            Resources.LoadAll<DialogueSequenceData>(sequenceResourcesPath);

        foreach (DialogueSequenceData seq in allSequences)
        {
            if (seq == null || string.IsNullOrEmpty(seq.sequenceId))
            {
                Debug.LogWarning($"[DialogueRepository] Sequence has empty sequenceId: {seq?.name}");
                continue;
            }

            if (!_sequencesById.TryAdd(seq.sequenceId, seq))
            {
                Debug.LogWarning($"[DialogueRepository] Duplicate Sequence ID detected: {seq.sequenceId}");
            }
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueRepository] Loaded {_sequencesById.Count} DialogueSequenceData assets");
    }

    private void LoadAllSpeakers()
    {
        DialogueSpeakerData[] allSpeakers =
            Resources.LoadAll<DialogueSpeakerData>(speakerResourcesPath);

        foreach (DialogueSpeakerData speaker in allSpeakers)
        {
            if (speaker == null || string.IsNullOrEmpty(speaker.speakerId))
            {
                Debug.LogWarning($"[DialogueRepository] Speaker has empty speakerId: {speaker?.name}");
                continue;
            }

            if (!_speakersById.TryAdd(speaker.speakerId, speaker))
            {
                Debug.LogWarning($"[DialogueRepository] Duplicate Speaker ID detected: {speaker.speakerId}");
            }

            _portraitCache[speaker.speakerId] = BuildPortraitCache(speaker);
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueRepository] Loaded DialogueSpeakerData: {_speakersById.Count} speakers");
    }

    private Dictionary<Expression, Sprite> BuildPortraitCache(DialogueSpeakerData speaker)
    {
        var dict = new Dictionary<Expression, Sprite>();

        foreach (PortraitConfig config in speaker.portraitConfigs)
        {
            if (config == null || config.portrait == null)
            {
                Debug.Log($"[DialogueRepository] Missing portrait sprite: {speaker.speakerId} / {config?.expression}");
                continue;
            }

            dict[config.expression] = config.portrait;
        }

        return dict;
    }

    #endregion

    #region Public API - Sequence / Situation

    public bool TryGetSequence(string sequenceId, out DialogueSequenceData sequence)
        => _sequencesById.TryGetValue(sequenceId, out sequence);

    public bool TryGetSituation(string sequenceId, string situationId, out SituationEntry situation)
    {
        situation = null;
        if (!_sequencesById.TryGetValue(sequenceId, out DialogueSequenceData seq) || seq == null)
            return false;

        if (seq.situations == null)
            return false;

        situation = seq.situations
            .FirstOrDefault(s => s != null && s.situationId == situationId);
        return situation != null;
    }

    #endregion

    #region Public API - Speaker / Portrait

    public bool TryGetSpeaker(string speakerId, out DialogueSpeakerData speaker)
        => _speakersById.TryGetValue(speakerId, out speaker);

    /// <summary>
    /// 스피커 + 표정으로 초상화 스프라이트 조회 (없으면 Default 사용, 그마저도 없으면 null)
    /// </summary>
    public Sprite GetSpeakerPortrait(string speakerId, Expression expression)
    {
        if (!_portraitCache.TryGetValue(speakerId, out var dict) || dict == null)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[DialogueRepository] No portrait dictionary found for speakerId: {speakerId}");
            return null;
        }

        if (dict.TryGetValue(expression, out Sprite sprite))
            return sprite;

        if (dict.TryGetValue(Expression.Default, out Sprite defaultSprite))
            return defaultSprite;

        return null;
    }

    #endregion
}
