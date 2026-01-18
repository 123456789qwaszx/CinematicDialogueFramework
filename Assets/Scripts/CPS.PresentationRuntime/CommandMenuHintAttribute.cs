using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandMenuHintAttribute : Attribute
{
    public string Category { get; }
    public string DisplayName { get; }

    // This command belongs to these sets (menu folders).
    // e.g. new[] { "Custom/Portrait/Enter", "VN/MainEnterFirstLine" }
    public string[] Sets { get; set; }

    // Sorting within a set (smaller first).
    public int SetOrder { get; set; } = 0;

    // Sorting within a category (smaller first).
    public int Order { get; set; } = 0;

    public CommandMenuHintAttribute(string category, string displayName = null)
    {
        Category = category;
        DisplayName = displayName;
    }
}