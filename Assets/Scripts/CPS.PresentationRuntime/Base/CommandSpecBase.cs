using System;

[Serializable]
public abstract class CommandSpecBase
{
    // 이 커맨드가 속한 화면 (UI Screen) 식별자
    public string screenId;

    // 이 화면 안에서 "어느 역할/세트"와 계약하는지.
    // 예: "MainSpeaker", "SubSpeaker", "SystemLine", "ChoicePanel"
    public string widgetRoleKey;
}