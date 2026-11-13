using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftOceanPresentation
    {
        private Material concrete, timber, metalFinish, paintedMetal, roadRubber, glassFinish, vehicleFinish, contact;
        private readonly List<(Material material, Color tint)> finishes = new List<(Material, Color)>();
        private sealed class CarContact { public Transform Root, Target; public bool Police; public Material Material; }
        private readonly List<CarContact> carContacts = new List<CarContact>();
        private Mesh contactMesh;

        private void BuildFinishes()
        {
            concrete=Finish("Salt-weathered marine concrete",new Color(.68f,.70f,.65f),3,.05f,.05f,.65f);
            timber=Finish("Sun-bleached dock timber",new Color(.61f,.45f,.28f),2,.14f,.04f,.55f);
            metalFinish=Finish("Galvanised fittings",Color.white,1,.65f,.28f,.35f);
            paintedMetal=Finish("Faded blue-grey working metal",new Color(.39f,.51f,.52f),0,.40f,.16f,.6f);
            roadRubber=Finish("Subtle tyre wear",new Color(.18f,.25f,.28f),4,.25f,.08f,0f);
            roadRubber.SetFloat("_Grain",1f);
            glassFinish=Finish("Coastal car glazing",Color.white,5,.85f,.35f,.05f);
            vehicleFinish=Finish("Coastal car paint and trim",Color.white,0,.60f,.23f,.18f);
            contact=Material("WorldShiftOceanContact","Soft grounded contacts");
            contact.SetFloat("_Opacity",.35f);
        }

        private Material Finish(string label,Color tint,float kind,float smooth,float specular,float wear)
        {
            var m=Material("WorldShiftOceanSurface",label);
            m.SetColor("_BaseColor",tint);m.SetFloat("_Finish",kind);m.SetFloat("_Smoothness",smooth);
            m.SetFloat("_Specular",specular);m.SetFloat("_Wear",wear);
            finishes.Add((m,tint));return m;
        }

        private void SetFinishWorld(bool day)
        {
            foreach(var finish in finishes)
                finish.material.SetColor("_BaseColor",finish.tint*(day ? Color.white : new Color(.48f,.63f,.79f)));
            // Match the road's lighting and grain; tyre residue is only slightly darker.
            roadRubber.SetColor("_BaseColor",(day ? new Color(.20f,.27f,.30f) : new Color(.085f,.145f,.205f))*.92f);
            contact.SetFloat("_Opacity",day ? .35f : .22f);
        }

        public void ApplyCoastalLight(Light light,ShiftWorld next)
        {
            bool day=next==ShiftWorld.Day;
            light.transform.rotation=Quaternion.LookRotation(-new Vector3(-.45f,.55f,.8f).normalized,Vector3.up);
            light.color=day ? new Color(1f,.94f,.82f) : new Color(.56f,.73f,1f);
            light.intensity=day ? 1.1f : .42f;
            RenderSettings.ambientLight=day ? new Color(.62f,.70f,.73f) : new Color(.19f,.29f,.39f);
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(RenderSettings.ambientLight.linear);
            RenderSettings.ambientProbe=probe;
        }

        private Transform Contact(Transform parent,Vector3 position,Vector2 size,Material material)
        {
            if(contactMesh==null)
            {
                var g=new WorldShiftArtMesh();
                g.Quad(new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f),Color.white);
                contactMesh=g.Mesh("Shared soft contact quad");meshes.Add(contactMesh);
            }
            var root=new GameObject("Soft contact shadow",typeof(MeshFilter),typeof(MeshRenderer));
            root.transform.SetParent(parent,false);root.transform.localPosition=position;
            root.transform.localScale=new Vector3(size.x,1,size.y);
            root.GetComponent<MeshFilter>().sharedMesh=contactMesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return root.transform;
        }

        private void BuildCarContact(Transform target,bool police)
        {
            var mat=Material("WorldShiftOceanContact",police ? "Police contact" : "Player contact");
            var shadow=Contact(transform,Vector3.zero,new Vector2(2.2f,4.2f),mat);
            carContacts.Add(new CarContact {Root=shadow,Target=target,Police=police,Material=mat});
        }

        private void RefreshCarContacts(float distance)
        {
            foreach(var item in carContacts)
            {
                float at=item.Police ? distance-game.Police.DistanceToPlayer-3.3f : distance;
                var p=route.Sample(at);
                Vector3 location=p.Position+p.Right*Vector3.Dot(item.Target.position-p.Position,p.Right)+p.Up*.028f;
                float height=Mathf.Max(0,Vector3.Dot(item.Target.position-p.Position,p.Up));
                item.Root.gameObject.SetActive(route.HasSurface(at,world)&&height<6f);
                item.Root.SetPositionAndRotation(location,Quaternion.LookRotation(p.Forward,p.Up));
                item.Material.SetFloat("_Opacity",(world==ShiftWorld.Day ? .43f : .28f)*Mathf.Exp(-height*.7f));
            }
        }

        private void BuildPierContact(Transform parent,float at)
        {
            var p=route.Sample(at);
            foreach(float side in new[] {-1f,1f})
            {
                Vector3 foot=p.Position+p.Right*side*4.6f;foot.y=SeaHeight+.04f;
                var shadow=Contact(parent,Vector3.zero,new Vector2(3.8f,5.8f),contact);
                shadow.SetPositionAndRotation(foot,Quaternion.LookRotation(Vector3.ProjectOnPlane(p.Forward,Vector3.up),Vector3.up));
            }
        }

        private void BuildRoadPatina(Transform parent,float start,float end)
        {
            var g=new WorldShiftArtMesh();
            for(float d=start;d<end-.01f;d+=4f)
            {
                var a=route.Sample(d);var b=route.Sample(Mathf.Min(d+4f,end));
                // Broken, low-contrast tyre scuffs stay separate from lane paint and hazards.
                if(((int)(d/16f))%3!=0) continue;
                foreach(float lane in new[] {-3.333f,0f,3.333f})
                    foreach(float wheel in new[] {-.65f,.65f})
                    {
                        float x=lane+wheel;
                        g.Quad(a.Position+a.Right*(x-.04f)+a.Up*.019f,b.Position+b.Right*(x-.04f)+b.Up*.019f,
                            b.Position+b.Right*(x+.04f)+b.Up*.019f,a.Position+a.Right*(x+.04f)+a.Up*.019f,Color.white);
                    }
            }
            // Expansion joints follow the road surface and never float across a missing deck.
            var p=route.Sample(start+.08f);
            g.Beam(p.Position-p.Right*4.92f+p.Up*.018f,p.Position+p.Right*4.92f+p.Up*.018f,.035f,.025f,Color.white);
            Render("Road scuffs and expansion joints",g,roadRubber,parent);
        }

        private void AddClosureHardware(WorldShiftArtMesh fittings,WorldShiftArtMesh markings,Vector3 size,bool moving)
        {
            Vector3 h=size*.5f;
            // Prominent world-coloured bands communicate state while the body keeps its natural finish.
            foreach(float end in new[] {-1f,1f})
            {
                float z=end*(h.z+.025f);
                markings.Box(new Vector3(0,-h.y+.36f,z),new Vector3(size.x*.88f,.25f,.025f),Color.white);
                if(size.x>6f)
                    foreach(float edge in new[] {-1f,1f})
                        markings.Box(new Vector3(edge*(h.x-.34f),.08f,z),new Vector3(.48f,size.y*.72f,.035f),Color.white);
                foreach(float side in new[] {-1f,1f})
                {
                    float x=side*(h.x-.18f);
                    fittings.ChamferBox(new Vector3(x,-h.y+.20f,end*(h.z-.17f)),new Vector3(.25f,.17f,.27f),.03f,new Color(.31f,.36f,.36f));
                    if(!moving)
                        fittings.Beam(new Vector3(x,-h.y+.23f,end*(h.z-.20f)),new Vector3(x,-h.y+.83f,end*(h.z-.12f)),.035f,.035f,new Color(.64f,.61f,.48f));
                }
            }
        }

        private void AddJumpEndCaps(WorldShiftArtMesh g,float start,float end)
        {
            foreach(var jump in game.RelayLayout)
            {
                bool lip=Mathf.Abs(end-jump.Takeoff)<.02f;
                bool landing=Mathf.Abs(start-jump.Landing)<.02f;
                if(!lip&&!landing) continue;
                var p=route.Sample(lip ? end-.10f : start+.10f);
                Vector3 left=p.Position-p.Right*5.1f-p.Up*.9f,right=p.Position+p.Right*5.1f-p.Up*.9f;
                g.Beam(left,right,.26f,1.4f,new Color(.39f,.46f,.47f));
                foreach(float x in new[] {-4.3f,-2.15f,0f,2.15f,4.3f})
                {
                    Vector3 top=p.Position+p.Right*x-p.Up*.15f;
                    g.Beam(top,top-p.Up*2.5f,.25f,.36f,new Color(.38f,.44f,.44f));
                    g.Beam(top-p.Up*2.4f,top+p.Forward*(lip ? -4f : 4f)-p.Up*.6f,.17f,.17f,new Color(.52f,.56f,.53f));
                }
                foreach(float side in new[] {-1f,1f})
                    g.ChamferBox(p.Position+p.Right*side*5.65f+p.Up*.35f,new Vector3(.85f,.95f,.8f),.12f,Cream);
            }
        }

        private void BuildLandingFrames()
        {
            foreach(var jump in game.RelayLayout)
            {
                var g=new WorldShiftArtMesh();var lights=new WorldShiftArtMesh();var p=route.Sample(jump.Landing+10f);
                Color hue=jump.Destination==ShiftWorld.Day ? new Color(1f,.75f,.27f) : new Color(.48f,.72f,1f);
                foreach(float side in new[] {-1f,1f})
                {
                    Vector3 foot=p.Position+p.Right*side*6.4f;
                    g.Beam(foot-p.Up*2f,foot+p.Up*7.3f,.55f,.55f,Cream);
                    lights.Beam(foot+p.Up*1f,foot+p.Up*6.8f,.10f,.12f,hue);
                    g.Beam(foot+p.Up*7.3f,p.Position+p.Right*side*3.5f+p.Up*8.1f,.30f,.35f,Cream);
                }
                g.Beam(p.Position-p.Right*3.5f+p.Up*8.1f,p.Position+p.Right*3.5f+p.Up*8.1f,.30f,.35f,Cream);
                var root=Render("Framed Ocean landing "+jump.Number,g,metalFinish);
                Render("Landing frame light inlays",lights,luminous,root.transform);
                chunks.Add(new CoastChunk {At=jump.Ramp,End=jump.End,Root=root,OnlyWorld=jump.Destination,RecoveryFor=jump});
            }
        }

        private bool LandmarkReserve(float at,float side)
        {
            for(int section=0;section<game.RelayLayout.Length;section++)
            {
                if(section!=0 && section!=1 && section!=3) continue;
                float from=section==0 ? 0f : game.RelayLayout[section-1].End;
                float reservedSide=section==1 ? -1f : 1f;
                if(side==reservedSide && Mathf.Abs(at-(from+135f))<65f) return true;
            }
            return false;
        }

        private void BuildSectionLandmarks()
        {
            for(int section=0;section<game.RelayLayout.Length;section++)
            {
                float from=section==0 ? 0f : game.RelayLayout[section-1].End;
                float at=from+135f;
                var p=route.Sample(at);
                var g=new WorldShiftArtMesh();var glow=new WorldShiftArtMesh();
                Vector3 centre=p.Position+p.Right*(section==1 ? -55f : 52f);
                centre.y=SeaHeight+1f;
                if(section==0 && ClearOfRoad(centre,29f)) BuildFishingHarbour(g,glow,centre,p);
                else if(section==1 && ClearOfRoad(centre,22f))
                {
                    g.Rock(centre-Vector3.up*2f,new Vector3(20f,22f,26f),Stone,31);
                    g.Rock(centre+new Vector3(-16,0,13),new Vector3(7f,31f,9f),Stone,19);
                    g.Rock(centre+new Vector3(12,0,-13),new Vector3(8f,26f,9f),Stone,37);
                    Palm(g,centre+new Vector3(0,19f,0),8);
                }
                else if(section==2 || section==4)
                {
                    // Cable stays and towers sit outside the carriageway, well before the ramp approach.
                    foreach(float side in new[] {-1f,1f})
                    {
                        Vector3 foot=p.Position+p.Right*side*8f;
                        g.Beam(new Vector3(foot.x,SeaHeight-1f,foot.z),foot+Vector3.up*23f,1.15f,1.15f,Cream);
                        foreach(float offset in new[] {-42f,42f})
                        {
                            var anchor=route.Sample(at+offset);
                            g.Beam(foot+Vector3.up*21f,anchor.Position+anchor.Right*side*8f+Vector3.up*1.2f,.14f,.14f,Stone);
                        }
                        glow.Beam(foot+Vector3.up*12f,foot+Vector3.up*22f,.08f,.08f,section==4 ? new Color(1f,.78f,.36f) : Teal);
                    }
                    g.Beam(p.Position-p.Right*8f+Vector3.up*23f,p.Position+p.Right*8f+Vector3.up*23f,.6f,1.1f,Cream);
                }
                else if(section==3 && ClearOfRoad(centre,22f))
                {
                    float rise=p.Position.y-SeaHeight+2f;
                    g.Rock(centre-Vector3.up*2f,new Vector3(21f,rise,24f),Stone,17);
                    Vector3 top=centre+Vector3.up*(rise*.86f);
                    g.Rock(top-Vector3.up,new Vector3(15f,1.4f,17f),Sand,5);
                    Lighthouse(g,top,2);
                    glow.Cylinder(top+Vector3.up*16.6f,1.52f,1.52f,1.7f,new Color(1f,.90f,.60f),10);
                    House(g,top+new Vector3(-7f,0,2f),1);
                }
                string[] labels={"Fishing harbour","Rocky headland","Open-water cable bridge","Lighthouse approach","Final crossing"};
                var root=Render("Ocean landmark - "+labels[section],g,scenery);
                Render("Landmark light details",glow,luminous,root.transform);
                chunks.Add(new CoastChunk {At=at-90f,End=at+90f,Root=root});
            }
        }

        private void BuildFishingHarbour(WorldShiftArtMesh g,WorldShiftArtMesh glow,Vector3 p,RoadPathPoint road)
        {
            // Piled timber jetty with moorings, a boathouse and a compact fishing launch.
            for(int i=0;i<24;i++)
                g.Box(p+new Vector3(0,0,-14f+i*1.2f),new Vector3(13f,.30f,1.08f),new Color(.55f,.43f,.30f));
            foreach(float x in new[] {-5.7f,5.7f})
                foreach(float z in new[] {-13f,-5f,3f,11f})
                {
                    g.Cylinder(p+new Vector3(x,-3f,z),.24f,.22f,4f,Stone,10);
                    g.Cylinder(p+new Vector3(x,1f,z),.40f,.40f,.10f,Stone,10);
                    g.RopeRing(p+new Vector3(x,1.12f,z),.52f,.08f,Sand);
                }
            House(g,p+new Vector3(0,.2f,6f),0);
            Vector3 boat=p+new Vector3(-12f,-1.5f,-3f);
            g.Rock(boat,new Vector3(2.5f,1.6f,6.5f),Cream,2);
            g.ChamferBox(boat+Vector3.up*1.7f,new Vector3(3f,2.4f,3.2f),.2f,Teal);
            g.Box(boat+new Vector3(0,2f,-1.65f),new Vector3(2.5f,.7f,.08f),new Color(.09f,.20f,.23f));
            g.Beam(boat+Vector3.up*3f,boat+Vector3.up*6f,.13f,.13f,Stone);
            glow.Cylinder(boat+Vector3.up*6f,.17f,.12f,.23f,new Color(1f,.80f,.41f),10);
            for(int i=0;i<3;i++)
            {
                Vector3 crate=p+new Vector3(3f,.7f,-9f+i*2.3f);
                g.ChamferBox(crate,new Vector3(1.5f,1.2f,1.6f),.08f,new Color(.59f,.41f,.23f));
                g.RopeRing(crate+Vector3.up*.64f,.42f,.08f,Sand);
            }
        }
    }
}
