using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Playtesters had to guess which way sideways movers were going. Every sideways mover in every level now carries the
// shared single-headed arrow, aimed each frame along its real direction of travel (Docs/OBSTACLE_VISUAL_RULES.md rule 2).
public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator EverySidewaysMoverArrowPointsTheWayItIsGoing()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int level = 0; level < 6; level++)
        {
            StartLevel(level); yield return WaitForNavigation();
            var game = Started(); var presentation = Field(game, "presentation");
            var views = ((IEnumerable)Field(presentation, "hazards")).Cast<object>()
                .Where(v => (bool)Field(Field(v, "Data"), "LateralMotion")).ToArray();
            object day = World("Day");
            int checkedTurns = 0;
            var flipped = new HashSet<object>();
            foreach (var view in views)
            {
                var data = Field(view, "Data"); var root = (Transform)Field(view, "Root");
                float start = (float)Field(data, "Start");
                var arrows = ((IEnumerable)Field(view, "Arrows")).Cast<MonoBehaviour>().ToArray();
                Assert.That(arrows, Is.Not.Empty, $"Level {level}: the sideways mover at {start} carries a direction arrow.");
                Assert.That(root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Sideways mover arrow"),
                    Is.EqualTo(arrows.Length), $"Level {level}: every arrow on the mover at {start} is the shared, aimable one.");
                float Moved()
                {
                    Call(data, "Advance", .05f, start, day);
                    return (float)Get(data, "LateralOffset") - (float)Get(data, "PreviousLateralOffset");
                }
                float before = Moved();
                for (int step = 0; step < 240; step++)
                {
                    Call(presentation, "AimMoverArrows");
                    float now = Moved(), after = Moved();
                    // Only judge steady motion; the arrow flips at the turnaround itself.
                    if (Mathf.Abs(now) < 1e-4f || Mathf.Sign(before) != Mathf.Sign(now) || Mathf.Sign(now) != Mathf.Sign(after))
                    { before = after; continue; }
                    float heading = Mathf.Sign(now);
                    foreach (var arrow in arrows)
                    {
                        Assert.That((float)Get(arrow, "Heading"), Is.EqualTo(heading),
                            $"Level {level}: the arrow on the mover at {start} points the way it is moving.");
                        Assert.That(Vector3.Dot(arrow.transform.TransformVector(Vector3.right), root.right) * heading, Is.GreaterThan(0f),
                            $"Level {level}: the arrow tip at {start} faces the direction of travel on screen.");
                    }
                    if (heading < 0f) flipped.Add(view);
                    checkedTurns++;
                    before = after;
                }
                Call(data, "ResetMotion");
            }
            if (views.Length > 0)
            {
                Assert.That(checkedTurns, Is.GreaterThan(0), $"Level {level}: movers were checked while moving.");
                Assert.That(flipped, Is.Not.Empty, $"Level {level}: arrows turn around with their movers.");
            }
            Call(presentation, "AimMoverArrows");
        }
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator MountainRockfallArrowShowsWhichWayTheBoulderRolls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started(); var presentation = Field(game, "presentation");
        var crossing = ((IEnumerable)Field(presentation, "hazards")).Cast<object>()
            .First(v => Field(Field(v, "Data"), "Rock") != null);
        var data = Field(crossing, "Data");
        float at = (float)Field(data, "Start");
        Pose(game, at - 16f, 3.4f);
        var arrow = ((IEnumerable)Field(crossing, "Arrows")).Cast<MonoBehaviour>().Single();
        // Roll the boulder in Day, capturing one picture heading each way.
        var seen = new HashSet<float>();
        for (int i = 0; i < 400 && seen.Count < 2; i++)
        {
            Call(data, "Advance", .05f, at, World("Day"));
            Call(presentation, "UpdateVisibility", at - 16f);
            float heading = (float)Get(arrow, "Heading");
            if (seen.Add(heading))
            {
                yield return null;
                Capture("mountain-rockfall-arrow-" + (heading > 0f ? "right" : "left"));
            }
        }
        Assert.That(seen, Is.EquivalentTo(new[] { -1f, 1f }), "The rockfall arrow flips when the boulder turns around.");
    }
}
