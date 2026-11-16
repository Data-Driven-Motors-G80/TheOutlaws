using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanPresentation
    {
        private void BuildCoast()
        {
            int index = 0;
            for (float at = 35f; at < game.RunLength + 120f; at += 78f)
            {
                var point = route.Sample(at);
                float side = index % 2 == 0 ? -1f : 1f;
                if (LandmarkReserve(at,side)) {index++;continue;}
                float offset = 44f + (index % 3) * 13f;
                Vector3 centre = point.Position + point.Right * side * offset;
                if (!ClearOfRoad(centre, 28f)) { index++; continue; }
                var g = new WorldShiftArtMesh();
                var foam = new WorldShiftArtMesh();
                float height = Mathf.Max(5f, point.Position.y - SeaHeight - 2f);
                Vector3 basePoint = new Vector3(centre.x, SeaHeight - 1f, centre.z);
                g.Rock(basePoint, new Vector3(19f, height, 27f), Stone, index);
                for (int edge = 0; edge < 9; edge++)
                {
                    float a = edge * Mathf.PI * 2f / 9f, b = (edge + 1) * Mathf.PI * 2f / 9f;
                    float r = .86f + .14f * Mathf.Sin(edge * 7.3f + index * 2.1f);
                    float r2 = .86f + .14f * Mathf.Sin(((edge + 1) % 9) * 7.3f + index * 2.1f);
                    Vector3 x = new Vector3(Mathf.Cos(a) * 19f * r, 0, Mathf.Sin(a) * 27f * r);
                    Vector3 y = new Vector3(Mathf.Cos(b) * 19f * r2, 0, Mathf.Sin(b) * 27f * r2);
                    Vector3 waterline = new Vector3(centre.x, SeaHeight + .035f, centre.z);
                    foam.Quad(waterline + x * .98f, waterline + y * .98f,
                        waterline + y * 1.06f, waterline + x * 1.06f, new Color(.49f, .83f, .79f));
                    // Sandy shelf sloping into a bright turquoise lagoon gives each island a shoreline.
                    Vector3 shelf = waterline + Vector3.up * .5f;
                    foam.Quad(shelf + x * .9f, shelf + y * .9f, waterline + y * 1.02f, waterline + x * 1.02f, Sand);
                    Vector3 lagoon = waterline - Vector3.up * .02f;
                    foam.Quad(lagoon + x * 1.06f, lagoon + y * 1.06f, lagoon + y * 1.4f, lagoon + x * 1.4f, new Color(.16f, .78f, .74f));
                    foam.Quad(lagoon + x * 1.4f, lagoon + y * 1.4f, lagoon + y * 1.65f, lagoon + x * 1.65f, new Color(.07f, .52f, .58f));
                }
                // Small boulders scattered on the beach line.
                for (int r = 0; r < 3; r++)
                {
                    float a = (index * 1.7f + r * 2.2f);
                    g.Rock(new Vector3(centre.x + Mathf.Cos(a) * 21f, SeaHeight - .4f, centre.z + Mathf.Sin(a) * 28f),
                        new Vector3(1.6f + r * .5f, 2.2f + r, 1.8f), Stone * (.9f + r * .05f), index + r * 5);
                }
                Vector3 top = basePoint + Vector3.up * height * .83f;
                g.Rock(top - Vector3.up * 1.8f, new Vector3(15f, 2.5f, 21f), Sand, index + 3);
                int section = 0;
                foreach (var relay in game.RelayLayout) if (at > relay.End) section++;
                if ((section == 3 && index % 3 == 0) || (section == 4 && index % 5 == 0)) Lighthouse(g, top, index);
                else if (section == 0)
                {
                    House(g, top + new Vector3(-4f, 0f, 0f), index);
                    House(g, top + new Vector3(3f, .5f, 4f), index + 1);
                    Palm(g, top + new Vector3(7f, 0, -4f), index);
                }
                else
                {
                    Palm(g, top + new Vector3(-5f, 0, -3f), index);
                    Palm(g, top + new Vector3(4f, 0, 3f), index + 2);
                    if (section == 1) g.Rock(basePoint + new Vector3(10f, 0, 9f), new Vector3(6f, height + 14f, 7f), Stone, index + 9);
                }
                var root = Render("Coastal island " + index, g, scenery);
                Render("Island surf", foam, scenery, root.transform);
                chunks.Add(new CoastChunk { At = at - 25f, End = at + 25f, Root = root });
                // White sail silhouettes and hulls give the open side of the coast a second scale.
                if (index % 3 == 0)
                {
                    var boat = new WorldShiftArtMesh();
                    Vector3 anchor = point.Position - point.Right * side * 80f;
                    anchor.y = SeaHeight + .35f;
                    boat.Rock(anchor, new Vector3(2f, 1f, 6f), Coral, index);
                    boat.Beam(anchor, anchor + Vector3.up * 12f, .12f, .12f, Cream);
                    boat.Triangle(anchor + new Vector3(.1f, 2f, 0), anchor + new Vector3(.1f, 11.5f, 0), anchor + new Vector3(5f, 2f, 0), Cream, true);
                    boat.Triangle(anchor + new Vector3(-.1f, 3f, 0), anchor + new Vector3(-.1f, 10f, 0), anchor + new Vector3(-3f, 3f, 0), Teal, true);
                    chunks.Add(new CoastChunk { At = at, End = at + 30, Root = Render("Sailing boat " + index, boat, scenery) });
                }
                else
                {
                    // Weathered sea stacks fill the open-water side between boats.
                    Vector3 stack = point.Position - point.Right * side * (58f + (index % 4) * 9f);
                    if (ClearOfRoad(stack, 8f))
                    {
                        var rocks = new WorldShiftArtMesh();
                        stack.y = SeaHeight - 1f;
                        rocks.Rock(stack, new Vector3(4f, 9f + index % 5 * 2f, 5f), Stone * .92f, index + 11);
                        rocks.Rock(stack + new Vector3(5f, 0, 3f), new Vector3(2.4f, 4.5f, 2.6f), Stone, index + 13);
                        rocks.Rock(stack + Vector3.up * (8f + index % 5 * 2f) * .82f, new Vector3(2.4f, .9f, 3f), Leaf, index);
                        chunks.Add(new CoastChunk { At = at, End = at + 30, Root = Render("Sea stack " + index, rocks, scenery) });
                    }
                }
                // Red/green channel buoys bob below the viaduct and frame the route.
                {
                    var buoy = new WorldShiftArtMesh(); var lamp = new WorldShiftArtMesh();
                    Vector3 b = point.Position + point.Right * -side * 15f; b.y = SeaHeight;
                    Color paint = side > 0 ? new Color(.82f, .17f, .14f) : new Color(.12f, .55f, .30f);
                    buoy.Cylinder(b - Vector3.up * .3f, 1f, .8f, 1.1f, paint, 8);
                    buoy.Cylinder(b + Vector3.up * .8f, .3f, .2f, 1.8f, Cream, 6);
                    lamp.Cylinder(b + Vector3.up * 2.6f, .32f, .32f, .4f, side > 0 ? new Color(1f, .35f, .25f) : new Color(.35f, 1f, .55f), 6);
                    var buoyRoot = Render("Channel buoy " + index, buoy, scenery);
                    Render("Buoy lamp", lamp, luminous, buoyRoot.transform);
                    if (ClearOfRoad(b, -10f)) chunks.Add(new CoastChunk { At = at - 10f, End = at + 10f, Root = buoyRoot });
                    else buoyRoot.SetActive(false);
                }
                index++;
            }
            // Taller beacons frame each jump from well outside the driving lanes.
            for (int i = 0; i < game.RelayLayout.Length; i++)
            {
                var jump = game.RelayLayout[i]; var p = route.Sample(jump.Ramp - 26f);
                var g = new WorldShiftArtMesh(); var glow = new WorldShiftArtMesh();
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 foot = p.Position + p.Right * side * 8.4f;
                    g.Beam(foot - Vector3.up * 2f, foot + Vector3.up * 7f, .65f, .65f, Cream);
                    glow.Beam(foot + Vector3.up * 3f, foot + Vector3.up * 6.5f, .72f, .72f, i % 2 == 0 ? new Color(1f, .75f, .27f) : new Color(.64f, .68f, 1f));
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 arrow = foot + Vector3.up * (1f + j * .7f);
                        g.Beam(arrow - p.Right * .45f - Vector3.up * .3f, arrow, .14f, .14f, Teal);
                        g.Beam(arrow, arrow + p.Right * .45f - Vector3.up * .3f, .14f, .14f, Teal);
                    }
                }
                var root = Render("Jump beacon pair " + (i + 1), g, scenery);
                Render("Jump beacon lenses", glow, luminous, root.transform);
                chunks.Add(new CoastChunk { At = jump.Ramp - 26f, End = jump.End, Root = root });
            }
        }

        private void BuildGulls()
        {
            // Simple flapping V silhouettes circling ahead of the player; purely decorative.
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("Gull " + (i + 1)).transform;
                root.SetParent(transform, false);
                foreach (float side in new[] { -1f, 1f })
                {
                    var wing = new WorldShiftArtMesh();
                    wing.Triangle(Vector3.zero, new Vector3(side * 1.1f, .25f, -.25f), new Vector3(side * 1.1f, .25f, .3f), Cream, true);
                    wing.Triangle(new Vector3(side * 1.1f, .25f, -.25f), new Vector3(side * 1.9f, -.15f, -.1f), new Vector3(side * 1.1f, .25f, .3f), Cream * .9f, true);
                    Render(side < 0 ? "Left wing" : "Right wing", wing, scenery, root);
                }
                gulls.Add(root);
            }
        }

        private void AnimateGulls()
        {
            Vector3 anchor = game.Player.position + game.Player.forward * 70f;
            for (int i = 0; i < gulls.Count; i++)
            {
                float t = animationTime * (.35f + i * .04f) + i * 1.9f;
                float radius = 18f + i * 6f, side = i % 2 == 0 ? 1f : -1f;
                Vector3 p = anchor + new Vector3(Mathf.Cos(t) * radius * side, 16f + i * 2.5f + Mathf.Sin(t * 2f) * 1.5f, Mathf.Sin(t) * radius);
                Vector3 velocity = new Vector3(-Mathf.Sin(t) * side, 0, Mathf.Cos(t));
                gulls[i].SetPositionAndRotation(p, Quaternion.LookRotation(velocity) * Quaternion.Euler(0, 0, side * 18f));
                float flap = Mathf.Sin(animationTime * 7f + i) * 22f;
                gulls[i].GetChild(0).localRotation = Quaternion.Euler(0, 0, -flap);
                gulls[i].GetChild(1).localRotation = Quaternion.Euler(0, 0, flap);
            }
        }

        private static void House(WorldShiftArtMesh g, Vector3 p, int variant)
        {
            g.Box(p + Vector3.up * 2f, new Vector3(5f, 4f, 6f), Cream);
            g.Box(p + Vector3.up * 4.2f, new Vector3(5.8f, .5f, 6.8f), variant % 2 == 0 ? Coral : Teal);
            for (int i = -1; i <= 1; i += 2)
            {
                g.Box(p + new Vector3(i * 1.3f, 2.3f, -3.03f), new Vector3(.9f, 1.3f, .08f), Teal);
                g.Box(p + new Vector3(2.53f, 2.3f, i * 1.6f), new Vector3(.08f, 1.3f, 1f), Teal);
            }
            g.Box(p + new Vector3(0, 1.1f, -3.05f), new Vector3(1f, 2.2f, .08f), Stone);
        }

        private static void Lighthouse(WorldShiftArtMesh g, Vector3 p, int seed)
        {
            for (int j = 0; j < 5; j++)
                g.Cylinder(p + Vector3.up * j * 3.2f, 2.4f - j * .22f, 2.18f - j * .22f, 3.2f, j % 2 == 0 ? Cream : Coral, 10);
            g.Cylinder(p + Vector3.up * 16f, 2.8f, 2.8f, .45f, Stone, 10);
            g.Cylinder(p + Vector3.up * 16.45f, 1.5f, 1.5f, 2f, new Color(.65f, .94f, .88f), 10);
            g.Cylinder(p + Vector3.up * 18.45f, 2.4f, 0f, 2f, Coral, 10);
        }

        private static void Palm(WorldShiftArtMesh g, Vector3 p, int seed)
        {
            float height = 7f + seed % 4;
            Vector3 crown = p + new Vector3(1.3f, height, .6f);
            g.Beam(p, crown, .45f, .45f, new Color(.42f, .30f, .19f));
            for (int i = 0; i < 7; i++)
            {
                float angle = (i * 360f / 7f + seed * 27f) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 across = Vector3.Cross(Vector3.up, d) * .8f;
                Vector3 middle = crown + d * 2.5f + Vector3.up * .65f;
                Vector3 tip = crown + d * 5f - Vector3.up * 1.5f;
                Color leaf = Color.Lerp(Leaf, new Color(.40f, .66f, .27f), (i % 3) * .22f);
                g.Triangle(crown, middle + across, middle, leaf, true);
                g.Triangle(crown, middle, middle - across, leaf * .85f, true);
                g.Triangle(middle + across, tip, middle, leaf, true);
                g.Triangle(middle, tip, middle - across, leaf * .85f, true);
            }
        }
    }
}
