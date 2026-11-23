using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftPresentation
    {
        private void BuildHazard(ShiftHazard data)
        {
            RoadPathPoint point = route.Sample((data.Start + data.End) * 0.5f);
            Transform root = new GameObject(data.World + " " + data.Kind + " " + data.Start).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
            var chunk = new Chunk { Root = root.gameObject, At = data.Start, End = data.End,
                Tail = data.Kind == ShiftHazardKind.Oncoming ? 8f : 90f };
            chunks.Add(chunk);
            var solid = new GameObject("Solid - collides in this world"); solid.transform.SetParent(root, false);
            var ghost = new GameObject("Outline - belongs to other world"); ghost.transform.SetParent(root, false);
            if (data.Gate != null)
            {
                // No text on obstacles: the sideways arrow and the frozen-ice cue explain the block.
                var gateView = new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk };
                bool desertGate = game.LevelMode && game.Environment == ShiftLevel.Desert;
                // Desert: real animals that move sideways. A camel caravan on the ground, a vulture flock in the air.
                if (desertGate) gateView.Movers.Add(data.Gate.Airborne
                    ? WorldShiftDesertMover.Flock(solid.transform, Vector3.zero, data.Gate.Size)
                    : WorldShiftDesertMover.Caravan(solid.transform, Vector3.zero, data.Gate.Size));
                else if (game.LevelMode) WorldShiftSpikePlatform.Build(solid.transform, Vector3.zero, data.Gate.Size);
                else WorldShiftObstacleVisual.Build(solid.transform, Vector3.zero, data.Gate.Size, sideMoverPalette);
                // No floating ice ring under flying things.
                if (game.LevelMode && !(desertGate && data.Gate.Airborne)) BuildFrozenCue(root, Vector3.zero, data.Gate.Size, true);
                hazards.Add(gateView);
                return;
            }
            if (data.Rock != null)
            {
                var rockView = new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk };
                bool desertRock = game.LevelMode && game.Environment == ShiftLevel.Desert;
                if (desertRock && data.Rock.Airborne) rockView.Movers.Add(WorldShiftDesertMover.Vulture(solid.transform, root, data.Rock.Radius));
                else if (desertRock) WorldShiftDesertRockArt.Tumbleweed(solid.transform, root, data.Rock.Radius);
                // Mountain: a goat trotting across the road (white mountain goats and alpine ibex alternate).
                // Mountain: a big eagle gliding across a jump.
                else if (game.LevelMode && game.Environment == ShiftLevel.Mountain && data.Rock.Airborne)
                    rockView.Eagles.Add(WorldShiftMountainEagle.Build(solid.transform, root, data.Rock.Radius));
                else if (game.LevelMode && game.Environment == ShiftLevel.Mountain && !data.Rock.Airborne)
                    rockView.Goats.Add(WorldShiftMountainGoat.Build(solid.transform, root, data.Rock.Radius,
                        data.Rock.Motion.MotionSpeed * data.Rock.TravelScale, mountainGoatIndex++));
                else WorldShiftBoulderArt.Build(solid.transform, root, data.Rock.Radius, false,
                    game.LevelMode && game.Environment == ShiftLevel.Ocean);
                // No floating ice ring under flying vultures.
                bool flyingAnimal = data.Rock.Airborne && (desertRock || game.Environment == ShiftLevel.Mountain);
                if (game.LevelMode && !flyingAnimal) BuildFrozenCue(root, Vector3.zero, Vector3.one * (data.Rock.Radius * 2f));
                hazards.Add(rockView);
                return;
            }
            if (game.LevelMode && game.Environment == ShiftLevel.Mountain && data.Kind == ShiftHazardKind.Oncoming
                && data.SharedAcrossWorlds && !data.LateralMotion)
            {
                // Mountain rockslide: a boulder per lane rolls down the road in Day and freezes in Night.
                const float radius = 1.55f;
                var slideView = new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk, RollRadius = radius };
                for (int lane = 0; lane < 3; lane++)
                {
                    if ((data.LaneMask & (1 << lane)) == 0) continue;
                    Vector3 centre = new Vector3((lane - 1) * WorldShiftLevel.RoadWidth / 3f, radius, 0f);
                    var cue = new GameObject("Rockslide boulder cue").transform;
                    cue.SetParent(solid.transform, false);
                    cue.localPosition = centre;
                    var roller = new GameObject("Rolling boulder").transform;
                    roller.SetParent(cue, false);
                    WorldShiftBoulderArt.Build(roller, cue, radius, sidewaysCue: false);
                    slideView.Rollers.Add(roller);
                    BuildFrozenCue(solid.transform, centre, Vector3.one * (radius * 2f));
                }
                hazards.Add(slideView);
                return;
            }
            if (game.LevelMode && data.Kind == ShiftHazardKind.Oncoming && !data.LateralMotion)
            {
                var trafficView = new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk };
                for (int lane = 0; lane < 3; lane++)
                {
                    if ((data.LaneMask & (1 << lane)) == 0) continue;
                    Vector3 centre = new Vector3((lane - 1) * WorldShiftLevel.RoadWidth / 3f, .9f, 0f);
                    Vector3 size = new Vector3(3.05f, 1.8f, data.End - data.Start);
                    var vehicle=WorldShiftRoadVehicleArt.Build(solid.transform, centre, size, moverPalette, game.Environment,
                        Mathf.RoundToInt(data.Start * 7f) + lane * 3, data.SharedAcrossWorlds, data.World);
                    vehicle.ConfigureWarning(game,data);
                    trafficView.Traffic.Add(vehicle);
                    if (data.SharedAcrossWorlds) BuildFrozenCue(solid.transform, centre, size, true);
                    else WorldShiftLevelProps.DashedOutline(ghost.transform, centre, size,
                        WorldShiftLevelProps.Glow(data.World == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour,
                            data.World == ShiftWorld.Day ? .8f : 0f));
                }
                hazards.Add(trafficView);
                return;
            }
            float depth = data.End - data.Start;
            var palette = data.Kind == ShiftHazardKind.SwitchWall ? switchPalette
                : data.LateralMotion ? sideMoverPalette
                : data.Kind == ShiftHazardKind.Oncoming || data.MotionSpeed > 0f
                    ? moverPalette : data.SharedAcrossWorlds ? permanentPalette : obstaclePalette;
            // The other world's still obstacles show as passable dashes in their own colour.
            Material dashes = game.LevelMode && !data.SharedAcrossWorlds
                ? WorldShiftLevelProps.Glow(data.World == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour,
                    data.World == ShiftWorld.Day ? .8f : 0f) : null;
            var retractable = new List<WorldShiftRetractableBarrier>();
            var movers = new List<WorldShiftDesertMover>();
            int oceanVariant = oceanHazardIndex++;
            if (ocean != null && data.LateralMotion) ocean.BuildTrolleyTrack(data);
            void Block(Transform frame, Transform ghostFrame, Vector3 centre, Vector3 size)
            {
                if (game.LevelMode && data.LateralMotion && data.SharedAcrossWorlds && game.Environment == ShiftLevel.Desert)
                    movers.Add(WorldShiftDesertMover.Camel(frame, centre, size));
                else if (game.LevelMode && data.LateralMotion && data.SharedAcrossWorlds)
                    WorldShiftSpikePlatform.Build(frame, centre, size);
                else if (game.LevelMode && game.Environment == ShiftLevel.Snow && data.SharedAcrossWorlds
                    && data.Kind == ShiftHazardKind.Barrier && !data.LateralMotion && data.MotionSpeed == 0f)
                    WorldShiftSnowObstacleArt.Snowbank(frame, centre, size);
                else if (game.LevelMode && data.Kind == ShiftHazardKind.Barrier
                    && data.SharedAcrossWorlds && !data.LateralMotion && data.MotionSpeed == 0f)
                    WorldShiftPoliceBarricade.Build(frame, centre, size);
                else if (game.LevelMode && WorldShiftRetractableBarrier.Supports(data))
                    retractable.Add(WorldShiftRetractableBarrier.Build(frame, centre, size, data.World, game.Environment,
                        Mathf.RoundToInt(data.Start)));
                else if (ocean != null)
                    ocean.BuildObstacle(frame, centre, size, palette, data, oceanVariant);
                else
                    WorldShiftObstacleVisual.Build(frame, centre, size, palette,
                        data.Kind == ShiftHazardKind.SwitchWall, data.Kind == ShiftHazardKind.Oncoming && !data.LateralMotion);
                if (game.LevelMode && data.SharedAcrossWorlds
                    && (data.LateralMotion || data.MotionSpeed > 0f))
                    BuildFrozenCue(frame, centre, size, data.LateralMotion);
                if (dashes != null && !(game.LevelMode && WorldShiftRetractableBarrier.Supports(data)))
                    WorldShiftLevelProps.DashedOutline(ghostFrame, centre, size, dashes);
            }
            if (data.Kind == ShiftHazardKind.Gap)
            {
                WorldShiftRoadSigns.BuildGapWarnings(solid.transform, route, data.Start, data.World == ShiftWorld.Echo);
                BuildPitLighting(solid.transform, data);
                WorldShiftPitVisuals.Build(solid.transform, route, data);
            }
            else
            {
                // Fit repeated blocks to the existing lane masks and curved collision footprint.
                for (float offset = 0f; offset < depth; offset += 8f)
                {
                    float length = Mathf.Min(8f, depth - offset);
                    float at = data.Start + offset + length * .5f;
                    var frame = new GameObject("Obstacle footprint section").transform;
                    frame.SetParent(solid.transform, false);
                    var sample = route.Sample(at);
                    frame.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Forward, sample.Up));
                    var ghostFrame = new GameObject("Other-world footprint section").transform;
                    ghostFrame.SetParent(ghost.transform, false);
                    ghostFrame.SetPositionAndRotation(frame.position, frame.rotation);
                    if (data.Kind == ShiftHazardKind.SwitchWall)
                        Block(frame, ghostFrame, new Vector3(0f, .9f, 0f), new Vector3(WorldShiftLevel.RoadWidth, 1.8f, length));
                    else if (data.Kind == ShiftHazardKind.HalfBarrier)
                        Block(frame, ghostFrame, new Vector3(data.HalfSide * 2.5f, .9f, 0f), new Vector3(5f, 1.8f, length));
                    else if (data.Kind == ShiftHazardKind.Divider)
                        Block(frame, ghostFrame, new Vector3(0f, .9f, 0f), new Vector3(3.05f, 1.8f, length));
                    else if (game.LevelMode && data.LaneMask == 7 && !data.LateralMotion)
                        Block(frame, ghostFrame, new Vector3(0f, .9f, 0f), new Vector3(WorldShiftLevel.RoadWidth, 1.8f, length));
                    else for (int lane = 0; lane < 3; lane++)
                    {
                        if ((data.LaneMask & (1 << lane)) == 0) continue;
                        float x = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                        Block(frame, ghostFrame, new Vector3(x, .9f, 0f), new Vector3(data.LateralMotion ? (game.LevelMode ? WorldShiftSpikePlatform.Width : 1.6f) : 3.05f, 1.8f, length));
                    }
                }
            }
            var view = new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk };
            view.Barriers.AddRange(retractable);
            view.Movers.AddRange(movers);
            hazards.Add(view);
        }

        private void BuildFrozenCue(Transform parent, Vector3 centre, Vector3 size, bool spikePlatform = false)
        {
            var ice = WorldShiftFrozenIce.Build(parent, centre, size, spikePlatform ? size.y * .34f : float.PositiveInfinity).GetComponent<WorldShiftFrozenIce>();
            ice.SetFrozen(world == ShiftWorld.Echo, true);
            frozenCues.Add(ice);
        }
    }
}
