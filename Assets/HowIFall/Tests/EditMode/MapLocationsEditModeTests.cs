using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class MapLocationsEditModeTests
{
    [Test]
    public void AvailabilityAndValidationRespectTypedConditionsAndMapContract()
    {
        var stateObject=new GameObject("MapEditState"); var destination=ScriptableObject.CreateInstance<DialogueSceneData>();
        try
        {
            var state=stateObject.AddComponent<GameState>();
            var available=Location("dorm",destination,new List<ChoiceCondition>());
            var locked=Location("cafe",destination,new List<ChoiceCondition>{new ChoiceCondition{stateValue=ChoiceStateValue.Suspicion,comparison=ChoiceComparisonOperator.GreaterOrEqual,threshold=2}});
            Assert.That(available.IsAvailable(state),Is.True); Assert.That(locked.IsAvailable(state),Is.False); state.suspicion=2; Assert.That(locked.IsAvailable(state),Is.True);
            var ids=new HashSet<string>(); Assert.That(MapLocationData.TryValidate(available,ids,out _),Is.True); Assert.That(MapLocationData.TryValidate(Location("dorm",destination,new List<ChoiceCondition>()),ids,out _),Is.False);
            Assert.That(MapLocationData.TryValidate(Location("bad",destination,new List<ChoiceCondition>(),new Rect(.9f,.9f,.2f,.2f)),new HashSet<string>(),out _),Is.False);
            Assert.That(MapLocationData.TryValidate(Location("missing",null,new List<ChoiceCondition>()),new HashSet<string>(),out _),Is.False);
            Assert.That(SaveData.CurrentVersion,Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(destination); Object.DestroyImmediate(stateObject); }
    }
    private static MapLocationData Location(string id,DialogueSceneData destination,List<ChoiceCondition> conditions,Rect? rect=null)=>new MapLocationData{locationId=id,displayName=id,normalizedRect=rect??new Rect(.1f,.1f,.2f,.2f),destinationScene=destination,availabilityConditions=conditions};
}
