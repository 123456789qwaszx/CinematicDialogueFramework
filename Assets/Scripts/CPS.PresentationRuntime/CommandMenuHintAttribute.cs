using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandMenuHintAttribute : Attribute
{
    public string Category { get; }
    public string DisplayName { get; }

    // 추가: 이 커맨드가 포함될 “세트들”
    // 예: new[] { "Custom/PortraitStart", "Custom/PortraitExit" }
    public string[] Sets { get; set; }

    // 추가: 세트 안에서의 정렬 우선순위 (작을수록 먼저)
    public int SetOrder { get; set; } = 0;

    // Order도 같이 쓰고 싶다면 여기에 추가 가능
    public int Order { get; set; } = 0;

    public CommandMenuHintAttribute(string category, string displayName = null)
    {
        Category    = category;
        DisplayName = displayName;
    }
}