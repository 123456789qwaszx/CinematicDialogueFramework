#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class CommandRecentRegistry
{
    // EditorPrefs에 저장할 키
    private const string PrefKey  = "CPS.SeqRecentCommands";
    private const int    MaxCount = 8;   // 최근 명령 최대 개수(원하면 조절)

    /// <summary>
    /// 커맨드 타입을 "최근 사용" 목록에 기록한다.
    /// </summary>
    public static void Record(Type type)
    {
        if (type == null) return;

        string id = type.FullName;   // FullName 기준으로 저장 (Namespace 포함)
        if (string.IsNullOrEmpty(id)) return;

        // 기존 목록 불러오기
        var list = LoadRawList();

        // 중복 제거 후 맨 앞에 삽입 (MRU)
        list.RemoveAll(s => s == id);
        list.Insert(0, id);

        // 최대 개수 제한
        if (list.Count > MaxCount)
            list.RemoveRange(MaxCount, list.Count - MaxCount);

        SaveRawList(list);
    }

    /// <summary>
    /// 현재 프로젝트에 존재하는 타입 중에서
    /// Recent 리스트와 매칭되는 타입들을 순서대로 돌려준다.
    /// </summary>
    public static List<Type> GetRecentTypes(IReadOnlyList<Type> allTypes)
    {
        var result = new List<Type>();
        if (allTypes == null || allTypes.Count == 0)
            return result;

        // allTypes 를 FullName -> Type 딕셔너리로
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var t in allTypes)
        {
            if (t == null) continue;
            string id = t.FullName;
            if (string.IsNullOrEmpty(id)) continue;

            if (!map.ContainsKey(id))
                map.Add(id, t);
        }

        // 저장된 문자열 목록
        var rawList = LoadRawList();
        foreach (var id in rawList)
        {
            if (map.TryGetValue(id, out var t))
                result.Add(t);   // 저장된 순서를 그대로 유지
        }

        return result;
    }

    // -------------------------------------------------
    // 내부: EditorPrefs <-> List<string> 직렬화 유틸
    // -------------------------------------------------
    private static List<string> LoadRawList()
    {
        string raw = EditorPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(raw))
            return new List<string>();

        // | 로 구분 (단순하고 문제 없음)
        return raw
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static void SaveRawList(List<string> list)
    {
        if (list == null || list.Count == 0)
        {
            EditorPrefs.DeleteKey(PrefKey);
            return;
        }

        string raw = string.Join("|", list);
        EditorPrefs.SetString(PrefKey, raw);
    }
}
#endif
