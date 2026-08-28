using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class SavePaginationEditModeTests
{
    [Test]
    public void ManualCapacityAndPageMappingUseSixtyStableAddresses()
    {
        Assert.That(SaveManager.SlotsPerPage, Is.EqualTo(6));
        Assert.That(SaveManager.ManualPageCount, Is.EqualTo(10));
        Assert.That(SaveManager.GetSlotCapacity(SaveSlotType.Manual), Is.EqualTo(60));
        Assert.That(ManualSaveLoadPanel.GetGlobalManualSlot(1, 1), Is.EqualTo(1));
        Assert.That(ManualSaveLoadPanel.GetGlobalManualSlot(1, 6), Is.EqualTo(6));
        Assert.That(ManualSaveLoadPanel.GetGlobalManualSlot(2, 1), Is.EqualTo(7));
        Assert.That(ManualSaveLoadPanel.GetGlobalManualSlot(10, 6), Is.EqualTo(60));
        Assert.That(ManualSaveLoadPanel.GetGlobalManualSlot(11, 1), Is.EqualTo(0));
    }

    [Test]
    public void ExistingManualFileStemsAndSpecialCapacitiesRemainCompatible()
    {
        string directory = Path.Combine(Path.GetTempPath(), "HowIFall_SavePagination_" + Guid.NewGuid().ToString("N"));
        var go = new GameObject("SavePaginationEditModeTests");
        try
        {
            SaveManager manager = go.AddComponent<SaveManager>();
            manager.ConfigureSaveDirectoryForTests(directory);
            for (int slot = 1; slot <= 6; slot++)
            {
                Assert.That(Path.GetFileName(manager.GetSlotJsonPath(SaveSlotType.Manual, slot)), Is.EqualTo($"slot_{slot:D2}.json"));
            }
            Assert.That(Path.GetFileName(manager.GetSlotJsonPath(SaveSlotType.Manual, 7)), Is.EqualTo("slot_07.json"));
            Assert.That(Path.GetFileName(manager.GetSlotJsonPath(SaveSlotType.Manual, 60)), Is.EqualTo("slot_60.json"));
            Assert.That(SaveManager.GetSlotCapacity(SaveSlotType.Auto), Is.EqualTo(6));
            Assert.That(SaveManager.GetSlotCapacity(SaveSlotType.Quick), Is.EqualTo(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetSlotJsonPath(SaveSlotType.Manual, 61));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetSlotJsonPath(SaveSlotType.Auto, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetSlotJsonPath(SaveSlotType.Quick, 7));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
