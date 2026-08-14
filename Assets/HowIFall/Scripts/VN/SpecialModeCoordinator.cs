using System;
using UnityEngine;

/// <summary>
/// Immutable cross-system permissions for one exclusive authored VN interaction.
/// A default value is invalid and is rejected by <see cref="SpecialModeCoordinator"/>.
/// </summary>
public readonly struct SpecialModePolicy
{
    private readonly bool isInitialized;

    public SpecialModePolicy(
        bool blocksDialogueAdvance,
        bool blocksAuto,
        bool blocksSkip,
        bool allowsSave,
        bool allowsLoad,
        bool allowsQuickMenu,
        bool allowsBacklog,
        bool allowsSettings,
        bool allowsEscapeCancel,
        bool allowsReturnToMainMenu,
        bool allowsGameMenu)
    {
        isInitialized = true;
        BlocksDialogueAdvance = blocksDialogueAdvance;
        BlocksAuto = blocksAuto;
        BlocksSkip = blocksSkip;
        AllowsSave = allowsSave;
        AllowsLoad = allowsLoad;
        AllowsQuickMenu = allowsQuickMenu;
        AllowsBacklog = allowsBacklog;
        AllowsSettings = allowsSettings;
        AllowsEscapeCancel = allowsEscapeCancel;
        AllowsReturnToMainMenu = allowsReturnToMainMenu;
        AllowsGameMenu = allowsGameMenu;
    }

    public bool IsValid => isInitialized;
    public bool BlocksDialogueAdvance { get; }
    public bool BlocksAuto { get; }
    public bool BlocksSkip { get; }
    public bool AllowsSave { get; }
    public bool AllowsLoad { get; }
    public bool AllowsQuickMenu { get; }
    public bool AllowsBacklog { get; }
    public bool AllowsSettings { get; }
    public bool AllowsEscapeCancel { get; }
    public bool AllowsReturnToMainMenu { get; }
    public bool AllowsGameMenu { get; }

    public static SpecialModePolicy BlockingExclusive => new SpecialModePolicy(
        blocksDialogueAdvance: true,
        blocksAuto: true,
        blocksSkip: true,
        allowsSave: false,
        allowsLoad: false,
        allowsQuickMenu: false,
        allowsBacklog: false,
        allowsSettings: false,
        allowsEscapeCancel: false,
        allowsReturnToMainMenu: false,
        allowsGameMenu: false);

    /// <summary>Exclusive hotspot presentation: blocks narrative input while allowing the existing Game Menu round-trip.</summary>
    public static SpecialModePolicy InteractiveScene => new SpecialModePolicy(
        blocksDialogueAdvance: true,
        blocksAuto: true,
        blocksSkip: true,
        allowsSave: false,
        allowsLoad: false,
        allowsQuickMenu: false,
        allowsBacklog: false,
        allowsSettings: false,
        allowsEscapeCancel: false,
        allowsReturnToMainMenu: false,
        allowsGameMenu: true);
}

/// <summary>Optional typed contract for a special mode that explicitly permits Escape cancellation.</summary>
public interface ISpecialModeEscapeHandler
{
    bool TryHandleSpecialModeEscapeCancel();
}

/// <summary>
/// Opaque proof that a special-mode owner currently holds the coordinator lease.
/// Only <see cref="SpecialModeCoordinator"/> can create a valid instance.
/// </summary>
public sealed class SpecialModeLease
{
    internal SpecialModeLease(SpecialModeCoordinator coordinator, UnityEngine.Object owner, ulong generation)
    {
        Coordinator = coordinator;
        Owner = owner;
        Generation = generation;
    }

    internal SpecialModeCoordinator Coordinator { get; }
    internal UnityEngine.Object Owner { get; }
    internal ulong Generation { get; }
}

/// <summary>
/// Scene-local, controller-owned arbiter for one authored special mode. It owns no GameObject or UI.
/// </summary>
public sealed class SpecialModeCoordinator
{
    private const string DiagnosticPrefix = "[SPECIAL MODE]";

    private readonly Func<string> normalBlockerReasonProvider;
    private readonly Action<string> diagnostic;
    private UnityEngine.Object activeOwner;
    private SpecialModePolicy activePolicy;
    private SpecialModeLease activeLease;
    private ulong nextGeneration;

    public SpecialModeCoordinator(Func<string> normalBlockerReasonProvider = null, Action<string> diagnostic = null)
    {
        this.normalBlockerReasonProvider = normalBlockerReasonProvider;
        this.diagnostic = diagnostic ?? Debug.LogWarning;
    }

    public bool HasActiveOwner
    {
        get
        {
            ClearDestroyedOwnerIfNeeded();
            return activeLease != null;
        }
    }

    public bool IsDialogueAdvanceBlocked => HasActiveOwner && activePolicy.BlocksDialogueAdvance;
    public bool IsAutoBlocked => HasActiveOwner && activePolicy.BlocksAuto;
    public bool IsSkipBlocked => HasActiveOwner && activePolicy.BlocksSkip;
    public bool CanSave => !HasActiveOwner || activePolicy.AllowsSave;
    public bool CanLoad => !HasActiveOwner || activePolicy.AllowsLoad;
    public bool CanOpenQuickMenu => !HasActiveOwner || activePolicy.AllowsQuickMenu;
    public bool CanOpenBacklog => !HasActiveOwner || activePolicy.AllowsBacklog;
    public bool CanOpenSettings => !HasActiveOwner || activePolicy.AllowsSettings;
    public bool CanReturnToMainMenu => !HasActiveOwner || activePolicy.AllowsReturnToMainMenu;
    public bool CanOpenGameMenu => !HasActiveOwner || activePolicy.AllowsGameMenu;

    public bool TryEnter(UnityEngine.Object owner, SpecialModePolicy policy, out SpecialModeLease lease)
    {
        lease = null;
        ClearDestroyedOwnerIfNeeded();

        if (owner == null)
        {
            Log("Entry rejected: owner is null or destroyed.");
            return false;
        }

        if (!policy.IsValid)
        {
            Log($"Entry rejected for {DescribeOwner(owner)}: policy is invalid and fails closed.");
            return false;
        }

        if (activeLease != null)
        {
            Log($"Entry rejected for {DescribeOwner(owner)}: active owner is {DescribeOwner(activeOwner)} (generation {activeLease.Generation}).");
            return false;
        }

        string blockerReason = normalBlockerReasonProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(blockerReason))
        {
            Log($"Entry rejected for {DescribeOwner(owner)}: normal blocker '{blockerReason}' is open.");
            return false;
        }

        nextGeneration++;
        if (nextGeneration == 0)
        {
            nextGeneration++;
        }

        activeOwner = owner;
        activePolicy = policy;
        activeLease = new SpecialModeLease(this, owner, nextGeneration);
        lease = activeLease;
        Log($"Entered {DescribeOwner(owner)} (generation {lease.Generation}).");
        return true;
    }

    public bool Exit(SpecialModeLease lease)
    {
        ClearDestroyedOwnerIfNeeded();

        if (lease == null)
        {
            Log("Exit rejected: lease is null.");
            return false;
        }

        if (activeLease == null
            || !ReferenceEquals(lease.Coordinator, this)
            || !ReferenceEquals(lease, activeLease)
            || lease.Generation != activeLease.Generation
            || !ReferenceEquals(lease.Owner, activeOwner))
        {
            Log($"Exit rejected for generation {lease.Generation}: lease is wrong or stale.");
            return false;
        }

        Log($"Exited {DescribeOwner(activeOwner)} (generation {lease.Generation}).");
        ClearActiveState();
        return true;
    }

    public bool TryRequestEscapeCancel()
    {
        ClearDestroyedOwnerIfNeeded();
        if (activeLease == null || !activePolicy.AllowsEscapeCancel)
        {
            return false;
        }

        if (!(activeOwner is ISpecialModeEscapeHandler handler))
        {
            Log($"Escape cancel rejected for {DescribeOwner(activeOwner)}: owner does not implement {nameof(ISpecialModeEscapeHandler)}.");
            return false;
        }

        bool accepted = handler.TryHandleSpecialModeEscapeCancel();
        if (!accepted)
        {
            Log($"Escape cancel was declined by {DescribeOwner(activeOwner)} (generation {activeLease.Generation}).");
        }

        return accepted;
    }

    public void ForceClearForHostLifecycle(string reason)
    {
        if (activeLease == null)
        {
            return;
        }

        Log($"Force-cleared {DescribeOwner(activeOwner)} (generation {activeLease.Generation}): {reason ?? "unspecified host lifecycle reason"}.");
        ClearActiveState();
    }

    private void ClearDestroyedOwnerIfNeeded()
    {
        if (activeLease != null && activeOwner == null)
        {
            Log($"Destroyed owner detected for generation {activeLease.Generation}; clearing active lease.");
            ClearActiveState();
        }
    }

    private void ClearActiveState()
    {
        activeOwner = null;
        activePolicy = default;
        activeLease = null;
    }

    private void Log(string message)
    {
        diagnostic?.Invoke($"{DiagnosticPrefix} {message}");
    }

    private static string DescribeOwner(UnityEngine.Object owner)
    {
        return owner == null ? "<destroyed>" : owner.GetType().Name;
    }
}
