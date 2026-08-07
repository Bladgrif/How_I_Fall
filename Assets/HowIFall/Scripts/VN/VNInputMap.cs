using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

public enum VNInputAction
{
    ToggleSkip,
    OpenSave,
    QuickSave,
    QuickLoad,
    OpenLoad,
    ShowBacklog,
    CloseOrCancel,
    ToggleDebugStatsView,
    ToggleDebugStatsPanel
}

public readonly struct VNInputBinding
{
    public VNInputBinding(VNInputAction action, string label, string bindingDescription, bool showInHelp)
    {
        Action = action;
        Label = label;
        BindingDescription = bindingDescription;
        ShowInHelp = showInHelp;
    }

    public VNInputAction Action { get; }
    public string Label { get; }
    public string BindingDescription { get; }
    public bool ShowInHelp { get; }
}

/// <summary>Canonical keyboard bindings for the current How I Fall runtime. This is not a rebinding system.</summary>
public static class VNInputMap
{
    private static readonly VNInputBinding[] Bindings =
    {
        new(VNInputAction.ToggleSkip, "\u041f\u0440\u043e\u043f\u0443\u0441\u043a", "Ctrl", true),
        new(VNInputAction.OpenSave, "\u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c", "F5", true),
        new(VNInputAction.QuickSave, "\u0411\u044b\u0441\u0442\u0440\u043e\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435", "F6", true),
        new(VNInputAction.QuickLoad, "\u0411\u044b\u0441\u0442\u0440\u0430\u044f \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u0430", "F8", true),
        new(VNInputAction.OpenLoad, "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c", "F9", true),
        new(VNInputAction.ShowBacklog, "\u0418\u0441\u0442\u043e\u0440\u0438\u044f", "B", true),
        new(VNInputAction.CloseOrCancel, "\u041d\u0430\u0437\u0430\u0434 / \u0437\u0430\u043a\u0440\u044b\u0442\u044c \u043e\u043a\u043d\u043e", "Esc", true),
        new(VNInputAction.ToggleDebugStatsView, "Toggle debug stats view", "F2", false),
        new(VNInputAction.ToggleDebugStatsPanel, "Toggle debug stats panel", "F3", false)
    };

    public static IReadOnlyList<VNInputBinding> AllBindings => Bindings;

    public static bool WasPressedThisFrame(VNInputAction action, Keyboard keyboard = null)
    {
        keyboard ??= Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return action switch
        {
            VNInputAction.ToggleSkip => keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame,
            VNInputAction.OpenSave => keyboard.f5Key.wasPressedThisFrame,
            VNInputAction.QuickSave => keyboard.f6Key.wasPressedThisFrame,
            VNInputAction.QuickLoad => keyboard.f8Key.wasPressedThisFrame,
            VNInputAction.OpenLoad => keyboard.f9Key.wasPressedThisFrame,
            VNInputAction.ShowBacklog => keyboard.bKey.wasPressedThisFrame,
            VNInputAction.CloseOrCancel => keyboard.escapeKey.wasPressedThisFrame,
            VNInputAction.ToggleDebugStatsView => keyboard.f2Key.wasPressedThisFrame,
            VNInputAction.ToggleDebugStatsPanel => keyboard.f3Key.wasPressedThisFrame,
            _ => false
        };
    }

    public static string BuildHelpText()
    {
        var builder = new StringBuilder("\u0423\u043f\u0440\u0430\u0432\u043b\u0435\u043d\u0438\u0435\n");
        foreach (VNInputBinding binding in Bindings)
        {
            if (binding.ShowInHelp)
            {
                builder.Append(binding.BindingDescription)
                    .Append(" \u2014 ")
                    .Append(binding.Label)
                    .Append('\n');
            }
        }

        builder.Append("\u041d\u0435\u043a\u043e\u0442\u043e\u0440\u044b\u0435 \u043a\u043e\u043c\u0430\u043d\u0434\u044b \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u044b \u0432\u043e \u0432\u0440\u0435\u043c\u044f \u043e\u0442\u043a\u0440\u044b\u0442\u044b\u0445 \u043e\u043a\u043e\u043d.");
        return builder.ToString();
    }

    public static void Validate()
    {
        var actions = new HashSet<VNInputAction>();
        var bindings = new HashSet<string>(StringComparer.Ordinal);

        foreach (VNInputBinding binding in Bindings)
        {
            if (!actions.Add(binding.Action))
            {
                throw new InvalidOperationException($"Duplicate input action: {binding.Action}.");
            }

            if (string.IsNullOrWhiteSpace(binding.BindingDescription))
            {
                throw new InvalidOperationException($"Input action {binding.Action} has no binding description.");
            }

            if (!bindings.Add(binding.BindingDescription))
            {
                throw new InvalidOperationException($"Duplicate input binding: {binding.BindingDescription}.");
            }
        }
    }
}
