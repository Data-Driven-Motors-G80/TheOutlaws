using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanPresentation
    {
        /// <summary>Coastal hazard skins fitted to the existing road-aligned collision box.</summary>
        public Transform BuildObstacle(Transform parent, Vector3 centre, Vector3 size,
            WorldShiftObstacleVisual.Palette palette, ShiftHazard hazard, int variation)
        {
            if (!hazard.LateralMotion && size.x < 6f && variation % 2 == 0)
            {
                var van=WorldShiftRoadVehicleArt.Build(parent,centre,size,palette,ShiftLevel.Ocean,3+5*variation);
                van.name="Parked coastal service van";
                Contact(van.transform,new Vector3(0,-.9f+.025f,0),new Vector2(3.05f,5.5f),contact);
                return van.transform;
            }
            var root = new GameObject(hazard.LateralMotion ? "Harbour cargo trolley"
                : size.x > 6f ? (variation % 2 == 0 ? "Shipping container" : "Harbour wave barricade")
                : "Fishing cargo stack").transform;
            root.SetParent(parent, false); root.localPosition = centre;
            var body = new WorldShiftArtMesh(); var markings = new WorldShiftArtMesh(); var fittings = new WorldShiftArtMesh();
            Vector3 h = size * .5f;
            float floor = -h.y;
            Color ink = new Color(.065f, .12f, .17f);
            Color metal = new Color(.40f, .50f, .52f);
            // The solid base marks every edge of the collision footprint. All fittings stay inside it.
            if (!hazard.LateralMotion)
                fittings.ChamferBox(new Vector3(0, floor + .10f, 0), new Vector3(size.x, .20f, size.z), .04f, ink);
            if (hazard.LateralMotion)
            {
                float wheelY = floor + .26f;
                foreach (float side in new[] {-1f, 1f})
                    foreach (float end in new[] {-1f, 1f})
                    {
                        var wheel = new WorldShiftArtMesh();
                        wheel.Wheel(Vector3.zero,.25f,.20f,ink);
                        wheel.Wheel(Vector3.right*end*.11f,.15f,.025f,metal);
                        for(int spoke=0;spoke<5;spoke++)
                        {
                            float a=spoke*Mathf.PI*.4f;
                            wheel.Beam(Vector3.right*end*.125f,new Vector3(end*.125f,Mathf.Cos(a)*.14f,Mathf.Sin(a)*.14f),.035f,.035f,Cream);
                        }
                        var roller=Render("Trolley drive roller",wheel,scenery,root).transform;
                        roller.localPosition=new Vector3(side*(h.x-.31f),wheelY,end*(h.z-.12f));
                        roller.localRotation=Quaternion.Euler(0,90,0);
                        rollers.Add(new DriveRoller {Root=roller,Data=hazard});
                    }
                fittings.ChamferBox(new Vector3(0,floor+.40f,0),new Vector3(size.x,.20f,size.z),.045f,metal);
                body.ChamferBox(new Vector3(0, floor+.57f, 0), new Vector3(size.x,.14f,size.z),.04f,Color.white);
                for (int box=0; box<2; box++)
                {
                    float z = -h.z+.90f+box*1.65f;
                    body.ChamferBox(new Vector3(0,floor+1.15f,z), new Vector3(size.x*.85f,1.02f,1.40f),.065f,Color.white);
                    fittings.Box(new Vector3(0,floor+1.67f,z), new Vector3(.10f,.035f,1.40f),ink);
                    foreach(float side in new[] {-1f,1f})
                    {
                        fittings.Box(new Vector3(side*h.x*.86f,floor+1.15f,z),new Vector3(.025f,1.02f,.10f),ink);
                        fittings.ChamferBox(new Vector3(side*.37f,floor+1.25f,z-.713f),new Vector3(.26f,.22f,.025f),.02f,metal);
                    }
                }
                // Visible electric drive, cooling grille and amber warning lenses explain the shuttle motion.
                float motorZ=h.z-.72f;
                body.ChamferBox(new Vector3(0,floor+1.0f,motorZ),new Vector3(size.x*.85f,.72f,1.22f),.10f,Color.white);
                fittings.Box(new Vector3(0,floor+1.0f,motorZ+.62f),new Vector3(size.x*.65f,.43f,.025f),ink);
                for(int i=0;i<5;i++)
                    fittings.Box(new Vector3(0,floor+.84f+i*.08f,motorZ+.64f),new Vector3(size.x*.58f,.025f,.02f),metal);
                foreach(float side in new[] {-1f,1f})
                {
                    fittings.Cylinder(new Vector3(side*.43f,floor+1.36f,motorZ),.10f,.08f,.14f,new Color(1f,.53f,.13f),12);
                    fittings.Box(new Vector3(side*(h.x-.03f),floor+.7f,0),new Vector3(.035f,.055f,size.z*.87f),Cream);
                }
                // The shared direction-of-travel arrow stays readable when the trolley freezes in Night.
                WorldShiftMoverArrow.OnBothFaces(root, Mathf.Min(size.x * .38f, 1.8f), floor + 1.10f, size.z);
            }
            else if (size.x > 6f && variation % 2 == 0)
            {
                body.ChamferBox(Vector3.zero,new Vector3(size.x-.10f,size.y-.08f,size.z-.10f),.08f,Color.white);
                // Corrugation wraps the roof and exposed faces, with locking bars on the rear doors.
                for (float x=-h.x+.25f; x<h.x-.1f; x+=.46f)
                {
                    body.Box(new Vector3(x,h.y-.025f,0),new Vector3(.10f,.05f,size.z-.12f),Color.white);
                    fittings.Box(new Vector3(x+.12f,h.y+.008f,0),new Vector3(.035f,.016f,size.z-.12f),new Color(.17f,.24f,.25f));
                    foreach (float face in new[] {-1f,1f})
                    {
                        body.Box(new Vector3(x,0,face*(h.z-.025f)),new Vector3(.10f,size.y-.12f,.05f),Color.white);
                        fittings.Box(new Vector3(x+.12f,0,face*(h.z+.005f)),new Vector3(.045f,size.y-.14f,.025f),new Color(.17f,.24f,.25f));
                    }
                }
                foreach (float face in new[] {-1f,1f})
                {
                    float z=face*(h.z-.015f);
                    fittings.Box(new Vector3(0,0,z),new Vector3(.055f,size.y-.12f,.025f),new Color(.17f,.24f,.25f));
                    foreach(float side in new[] {-1f,1f})
                    {
                        fittings.Box(new Vector3(side*h.x*.57f,0,z),new Vector3(.055f,size.y-.22f,.025f),metal);
                        fittings.Box(new Vector3(side*h.x*.57f,-.12f,z),new Vector3(.28f,.07f,.03f),metal);
                        fittings.Box(new Vector3(side*(h.x-.18f),floor+.35f,z),new Vector3(.20f,.40f,.03f),new Color(.17f,.24f,.25f));
                    }
                }
            }
            else if (size.x > 6f)
            {
                // Solid marine concrete profile: wide foot, tapered shoulders, narrow top.
                Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
                foreach(float end in new[] {-1f,1f})
                {
                    float z=end*h.z;
                    // Each end is a filled polygon, rather than a see-through fence over a solid hitbox.
                    Vector3[] outline={P(-h.x,floor,z),P(-h.x,-.30f,z),P(-h.x+.38f,h.y,z),
                        P(h.x-.38f,h.y,z),P(h.x,-.30f,z),P(h.x,floor,z)};
                    for(int j=1;j<outline.Length-1;j++)
                        if (end < 0f) body.Triangle(outline[0],outline[j],outline[j+1],Color.white);
                        else body.Triangle(outline[0],outline[j+1],outline[j],Color.white);
                }
                body.Quad(P(-h.x+.38f,h.y,-h.z),P(-h.x+.38f,h.y,h.z),P(h.x-.38f,h.y,h.z),P(h.x-.38f,h.y,-h.z),Color.white);
                body.Quad(P(-h.x,floor,h.z),P(-h.x,-.30f,h.z),P(-h.x,-.30f,-h.z),P(-h.x,floor,-h.z),Color.white);
                body.Quad(P(h.x,floor,-h.z),P(h.x,-.30f,-h.z),P(h.x,-.30f,h.z),P(h.x,floor,h.z),Color.white);
                body.Quad(P(-h.x,-.30f,h.z),P(-h.x+.38f,h.y,h.z),P(-h.x+.38f,h.y,-h.z),P(-h.x,-.30f,-h.z),Color.white);
                body.Quad(P(h.x,-.30f,-h.z),P(h.x-.38f,h.y,-h.z),P(h.x-.38f,h.y,h.z),P(h.x,-.30f,h.z),Color.white);
                foreach(float end in new[] {-1f,1f})
                {
                    float z=end*(h.z+.001f);
                    for(float x=-h.x+.45f; x<h.x-.8f; x+=1.12f)
                        if (end < 0f)
                            markings.Quad(P(x,-.20f,z),P(x+.46f,.68f,z),P(x+.91f,.68f,z),P(x+.45f,-.20f,z),Color.white);
                        else
                            markings.Quad(P(x+.45f,-.20f,z),P(x+.91f,.68f,z),P(x+.46f,.68f,z),P(x,-.20f,z),Color.white);
                }
                // Recessed mooring-ring shapes give the road closures a harbour identity.
                foreach(float x in new[] {-size.x*.32f,0f,size.x*.32f})
                {
                    fittings.Cylinder(new Vector3(x,h.y-.12f,0),.20f,.20f,.12f,metal,10);
                    fittings.RopeRing(new Vector3(x,h.y-.015f,0),.29f,.045f,metal);
                }
            }
            else
            {
                // Stacked fish crates with slatted faces, strapped down on a broad timber pallet.
                fittings.Box(new Vector3(0,floor+.23f,0),new Vector3(size.x,.16f,size.z),new Color(.47f,.32f,.19f));
                for(int row=0;row<3;row++)
                {
                    float z=Mathf.Lerp(-h.z+.9f,h.z-.9f,row*.5f);
                    body.ChamferBox(new Vector3(0,floor+.85f,z),new Vector3(size.x-.08f,1.15f,1.7f),.06f,Color.white);
                    for(int slat=0;slat<3;slat++)
                        foreach(float end in new[] {-1f,1f})
                            fittings.Box(new Vector3(0,floor+.58f+slat*.26f,z+end*.855f),new Vector3(size.x*.82f,.055f,.025f),ink);
                    foreach(float side in new[] {-1f,1f})
                        fittings.Box(new Vector3(side*(h.x-.12f),floor+.87f,z),new Vector3(.07f,1.16f,1.72f),ink);
                    fittings.RopeRing(new Vector3(0,floor+1.45f,z),Mathf.Min(.48f,h.x*.62f),.08f,new Color(.78f,.68f,.43f));
                }
                // A fishing float tops the load without exceeding its collision height.
                body.Cylinder(new Vector3(0,h.y-.30f,0),.22f,.08f,.30f,Color.white,10);
            }
            var painted = new GameObject("World-coloured coastal hazard").transform;
            painted.SetParent(root,false);
            AddClosureHardware(fittings,markings,size,hazard.LateralMotion);
            Material bodyFinish=hazard.LateralMotion ? paintedMetal : size.x > 6f
                ? variation % 2 == 0 ? paintedMetal : concrete : timber;
            Render("Weathered coastal hazard body",body,bodyFinish,root);
            Render("Obstacle block",markings,luminous,painted);
            palette.Add(painted);
            Render("Marine fittings",fittings,metalFinish,root);
            Contact(root,new Vector3(0,-size.y*.5f+.025f,0),new Vector2(size.x*.99f,size.z*.99f),contact);

            return root;
        }
    }
}
