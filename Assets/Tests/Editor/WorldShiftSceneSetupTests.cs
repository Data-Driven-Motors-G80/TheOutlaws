using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class WorldShiftSceneSetupTests
{
    [Test]
    public void SetupPreservesExistingScenesAndRegistersOnlyMenuAndSharedGameplay()
    {
        var setup = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("WorldShiftCampaignSetup")).First(t => t != null);
        string[] paths = (string[])setup.GetField("GameScenePaths").GetValue(null);
        var existing = paths.ToDictionary(p => p, File.ReadAllBytes);
        var originalBuild = EditorBuildSettings.scenes;
        try
        {
            setup.GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Assert.That(paths, Is.EqualTo(new[] { "Assets/Scenes/WorldShift_Menu.unity", "Assets/Scenes/City_WorldShift.unity" }));
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path), Is.EqualTo(paths));
            Assert.That(EditorBuildSettings.scenes.All(s => s.enabled), Is.True);
            foreach (var scene in existing) Assert.That(File.ReadAllBytes(scene.Key), Is.EqualTo(scene.Value), "Never overwrite an existing scene: " + scene.Key);
            Assert.That(Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories).OrderBy(p => p), Is.EqualTo(paths.OrderBy(p => p)));
        }
        finally { EditorBuildSettings.scenes = originalBuild; }
    }
}
