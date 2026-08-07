using System.Collections.Generic;

public static class RelationshipFeedback
{
    public static string Build(DialogueChoice choice)
    {
        if (choice == null)
        {
            return string.Empty;
        }

        List<string> changes = new List<string>(3);
        AddChange(changes, "\u041c\u0430\u0448\u0430", choice.trustMashaDelta);
        AddChange(changes, "\u0410\u0440\u0442\u0451\u043c", choice.trustArtemDelta);
        AddChange(changes, "\u041b\u0435\u0440\u0430", choice.leraInterestDelta);

        return string.Join("\n", changes);
    }

    private static void AddChange(List<string> changes, string characterName, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        string outcome = delta > 0
            ? "\u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u043b\u0443\u0447\u0448\u0438\u043b\u0438\u0441\u044c"
            : "\u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u0445\u0443\u0434\u0448\u0438\u043b\u0438\u0441\u044c";
        changes.Add($"{characterName} \u2014 {outcome}");
    }
}
