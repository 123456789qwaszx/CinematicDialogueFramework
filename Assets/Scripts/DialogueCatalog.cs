using System.Collections.Generic;
using UnityEngine;

public interface IDialogueCatalog
{
    DialogueSituationSpec Get(string situationKey);
}

public class DialogueCatalog : MonoBehaviour
{
    [SerializeField] private List<DialogueSituationSpec> situations = new();

    private Dictionary<string, DialogueSituationSpec> _situationSpecsByKey;

    private void Awake()
    {
        _situationSpecsByKey = new Dictionary<string, DialogueSituationSpec>();
        foreach (DialogueSituationSpec situation in situations)
        {
            if (situation == null || string.IsNullOrWhiteSpace(situation.situationKey)) continue;
            _situationSpecsByKey[situation.situationKey] = situation;
        }
    }
    public DialogueSituationSpec Get(string situationKey)
    {
        if (_situationSpecsByKey != null && _situationSpecsByKey.TryGetValue(situationKey, out DialogueSituationSpec situationSpec))
            return situationSpec;

        return null;
    }
}