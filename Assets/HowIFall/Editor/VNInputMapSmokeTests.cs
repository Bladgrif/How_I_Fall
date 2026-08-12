using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VNInputMapSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Input Map Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall input map smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        VNInputMap.Validate();

        Require(VNInputMap.AllBindings.Count == Enum.GetValues(typeof(VNInputAction)).Length, "Every VN input action must have one descriptor.");
        RequireBinding(VNInputAction.ToggleSkip, "Ctrl", "\u041f\u0440\u043e\u043f\u0443\u0441\u043a");
        RequireBinding(VNInputAction.OpenSave, "F5", "\u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c");
        RequireBinding(VNInputAction.QuickSave, "F6", "\u0411\u044b\u0441\u0442\u0440\u043e\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435");
        RequireBinding(VNInputAction.QuickLoad, "F8", "\u0411\u044b\u0441\u0442\u0440\u0430\u044f \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u0430");
        RequireBinding(VNInputAction.OpenLoad, "F9", "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c");
        RequireBinding(VNInputAction.ShowBacklog, "B", "\u0418\u0441\u0442\u043e\u0440\u0438\u044f");
        RequireBinding(VNInputAction.ToggleInterfaceVisibility, "H", "\u0421\u043a\u0440\u044b\u0442\u044c / \u043f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0438\u043d\u0442\u0435\u0440\u0444\u0435\u0439\u0441");
        RequireBinding(VNInputAction.CloseOrCancel, "Esc", "\u0418\u0433\u0440\u043e\u0432\u043e\u0435 \u043c\u0435\u043d\u044e / \u043d\u0430\u0437\u0430\u0434");

        string helpText = VNInputMap.BuildHelpText();
        foreach (VNInputBinding binding in VNInputMap.AllBindings.Where(binding => binding.ShowInHelp))
        {
            Require(helpText.Contains(binding.BindingDescription) && helpText.Contains(binding.Label), $"Help must be built from {binding.Action}.");
        }

        Require(!helpText.Contains("Enter"), "Help must not advertise unsupported Enter advance.");
        Require(!helpText.Contains("Space"), "Help must not advertise unsupported Space advance.");
        Require(!helpText.Contains("Alt+A"), "Help must not advertise unsupported Auto toggle.");
        Require(!helpText.Contains("\u0441\u043a\u0440\u0438\u043d\u0448\u043e\u0442"), "Help must not advertise unsupported screenshots.");
    }

    private static void RequireBinding(VNInputAction action, string key, string label)
    {
        VNInputBinding binding = VNInputMap.AllBindings.Single(candidate => candidate.Action == action);
        Require(binding.ShowInHelp, $"{action} must be player-facing in Help.");
        Require(binding.BindingDescription == key, $"{action} must use {key}.");
        Require(binding.Label == label, $"{action} must use the expected Help label.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
