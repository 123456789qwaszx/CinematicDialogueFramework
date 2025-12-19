using System;
using System.Collections.Generic;

[Serializable]
public class DialogueNodeSpec
{
    public List<NodeCommandSpec> commands = new();
    public List<GateToken> gateTokens = new() { GateToken.Input() };
}