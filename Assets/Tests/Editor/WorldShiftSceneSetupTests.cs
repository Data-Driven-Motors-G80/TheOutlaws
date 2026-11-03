using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class WorldShiftSceneSetupTests
{
    [Test]
    public void SetupPreservesExistingScenesAndRegistersAllFiveEnvironments()
    {
        var setup = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("WorldShiftCampaignSetup")).First(t => t != null);
        string[] paths = (string[])setup.GetField("GameScenePaths").GetValue(null);
        var existing = paths.ToDictionary(p => p, File.ReadAllBytes);
        var originalBuild = EditorBuildSettings.scenes;
        try
        {
            setup.GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Assert.That(paths.Length, Is.EqualTo(7));
            Assert.That(EditorBuildSettings.scenes.Take(7).Select(s => s.path), Is.EqualTo(paths));
            Assert.That(EditorBuildSettings.scenes.Take(7).All(s => s.enabled), Is.True);
            foreach (var scene in existing) Assert.That(File.ReadAllBytes(scene.Key), Is.EqualTo(scene.Value), "Never overwrite an existing scene: " + scene.Key);
            Assert.That(EditorBuildSettings.scenes.Where(s => s.path.Contains("/Env_") || s.path.Contains("Validation") || s.path.Contains("WorldShiftTrial")).All(s => !s.enabled), Is.True);
        }
        finally { EditorBuildSettings.scenes = originalBuild; }
    }
}
