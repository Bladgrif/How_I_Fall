using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class InteractiveHotspotEditModeTests
{
    [Test]
    public void LocalCompletionUnlocksDoorAndResetsForANewSceneRun()
    {
        GameObject stateObject = new GameObject("InteractiveHotspotEditState");
        try
        {
            GameState state = stateObject.AddComponent<GameState>();
            InteractiveHotspotData laptop = CreateHotspot("laptop", true);
            InteractiveHotspotData door = CreateHotspot("door", false, "laptop");
            InteractiveHotspotData window = CreateHotspot("window", true);
            var completed = new HashSet<string>();

            Assert.That(laptop.IsAvailable(state, completed), Is.True);
            Assert.That(door.IsAvailable(state, completed), Is.False);
            Assert.That(window.IsAvailable(state, completed), Is.True);

            completed.Add("laptop");
            Assert.That(laptop.IsCompleted(completed), Is.True);
            Assert.That(door.IsAvailable(state, completed), Is.True);

            completed.Add("window");
            Assert.That(window.IsCompleted(completed), Is.True);
            Assert.That(window.IsAvailable(state, completed), Is.False, "A completed one-shot must not become available again in the same run.");

            var nextRunCompleted = new HashSet<string>();
            Assert.That(laptop.IsAvailable(state, nextRunCompleted), Is.True);
            Assert.That(door.IsAvailable(state, nextRunCompleted), Is.False);
            Assert.That(window.IsAvailable(state, nextRunCompleted), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(stateObject); }
    }

    [Test]
    public void OptionalCanonicalConditionAndOutcomeRemainSupported()
    {
        GameObject stateObject = new GameObject("InteractiveHotspotCanonicalState");
        try
        {
            GameState state = stateObject.AddComponent<GameState>();
            InteractiveHotspotData hotspot = CreateHotspot("canonical", false);
            hotspot.availabilityConditions = AtLeast(ChoiceStateValue.Suspicion, 2);
            hotspot.outcome.stateChanges = new List<InteractiveStateChange>
            {
                new InteractiveStateChange { stateValue = ChoiceStateValue.TrustMasha, operation = InteractiveStateChangeOperation.Add, value = 3 }
            };

            Assert.That(hotspot.IsAvailable(state, new HashSet<string>()), Is.False);
            state.suspicion = 2;
            Assert.That(hotspot.IsAvailable(state, new HashSet<string>()), Is.True);
            Assert.That(hotspot.outcome.TryApply(state), Is.True);
            Assert.That(state.trustMasha, Is.EqualTo(3));
        }
        finally { UnityEngine.Object.DestroyImmediate(stateObject); }
    }

    [Test]
    public void InvalidLocalPrerequisiteConfigurationFailsValidation()
    {
        var ids = new HashSet<string>();
        var hotspot = CreateHotspot("invalid", true, "invalid");
        Assert.That(InteractiveHotspotData.TryValidate(hotspot, ids, out _), Is.True);

        InteractiveSceneData scene = ScriptableObject.CreateInstance<InteractiveSceneData>();
        try
        {
            scene.sceneId = "invalid_scene";
            scene.displayName = "Invalid";
            scene.hotspots = new List<InteractiveHotspotData> { hotspot };
            Assert.That(scene.TryValidate(null, out _), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(scene); }
    }

    private static InteractiveHotspotData CreateHotspot(string id, bool oneShot, params string[] required)
    {
        return new InteractiveHotspotData
        {
            hotspotId = id,
            displayName = id,
            normalizedRect = new Rect(0f, 0f, .2f, .2f),
            availabilityConditions = new List<ChoiceCondition>(),
            requiredCompletedHotspotIds = new List<string>(required),
            oneShot = oneShot,
            outcome = new InteractiveHotspotOutcome { stateChanges = new List<InteractiveStateChange>() }
        };
    }

    private static List<ChoiceCondition> AtLeast(ChoiceStateValue value, int threshold) => new List<ChoiceCondition> { new ChoiceCondition { stateValue = value, comparison = ChoiceComparisonOperator.GreaterOrEqual, threshold = threshold } };
}
