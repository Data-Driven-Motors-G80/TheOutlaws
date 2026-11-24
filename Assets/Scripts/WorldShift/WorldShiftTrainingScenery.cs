using UnityEngine;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private void BuildTrainingYard()
        {
            // An off-road practice depot. No scenery enters the lesson lanes or fills a jump gap.
            var dayRoad=MakeMaterial("Training asphalt Day",0f);
            var nightRoad=MakeMaterial("Training asphalt Night",0f);
            dayRoad.SetColor("_BaseColor",new Color(.20f,.22f,.23f));
            nightRoad.SetColor("_BaseColor",new Color(.08f,.10f,.13f));
            foreach(var material in new[]{dayRoad,nightRoad})
            { material.SetFloat("_Grain",.7f);material.SetFloat("_Smoothness",.3f);material.SetFloat("_Specular",.14f); }
            route.SetSurfaceMaterials(dayRoad,nightRoad);
            for(int section=0;section<route.Course.Checkpoints.Length;section++)
            {
                float start=route.Course.Checkpoints[section].Distance;
                foreach(float side in new[]{-1f,1f})
                {
                    float at=start+45f+(side>0?20f:0f);
                    if(NearJump(at,40)) continue;
                    var depot=Root("Training practice depot",at,side*30f);
                    var body=new WorldShiftArtMesh();var glass=new WorldShiftArtMesh();
                    Color wall=section%2==0?new Color(.24f,.38f,.44f):new Color(.48f,.43f,.32f);
                    Color metal=new Color(.11f,.18f,.22f),trim=new Color(.65f,.68f,.62f);
                    body.Box(new Vector3(0,2.5f,0),new Vector3(14,5,22),wall);
                    // Gabled roof, fascia and a broad shutter facing the practice road.
                    Vector3 a=new Vector3(-7.4f,5,-11.4f),b=new Vector3(7.4f,5,-11.4f),c=new Vector3(0,6.7f,-11.4f);
                    var along=Vector3.forward*22.8f;
                    body.Triangle(a,c,b,metal,true);body.Triangle(a+along,b+along,c+along,metal,true);
                    body.Quad(a,a+along,c+along,c,metal);body.Quad(c,c+along,b+along,b,metal);
                    float front=-side*7.08f;
                    body.Box(new Vector3(front,2f,0),new Vector3(.16f,4,7.6f),trim);
                    body.Box(new Vector3(front-side*.1f,1.9f,0),new Vector3(.06f,3.6f,7.1f),metal);
                    for(int slat=0;slat<12;slat++) body.Box(new Vector3(front-side*.14f,.25f+slat*.30f,0),new Vector3(.04f,.035f,7f),wall);
                    body.Box(new Vector3(front-side*.5f,4.35f,0),new Vector3(1.4f,.13f,8.2f),metal);
                    foreach(float z in new[]{-8f,8f})
                    {
                        body.Box(new Vector3(front,3.1f,z),new Vector3(.15f,1.6f,3.4f),trim);
                        glass.Box(new Vector3(front-side*.09f,3.1f,z),new Vector3(.05f,1.32f,3.1f),new Color(.66f,.82f,.79f));
                        body.Box(new Vector3(front-side*.13f,3.1f,z),new Vector3(.06f,1.35f,.07f),metal);
                    }
                    body.Box(new Vector3(front-side*.02f,4.95f,0),new Vector3(.20f,.13f,22.2f),new Color(.68f,.49f,.27f));
                    for(int container=0;container<2;container++)
                    {
                        var p=new Vector3(-side*9.5f,1.2f,14+container*7);
                        Color paint=container==0?new Color(.56f,.28f,.18f):new Color(.19f,.36f,.39f);
                        body.ChamferBox(p,new Vector3(3,2.4f,6),.08f,paint);
                        for(int rib=-5;rib<=5;rib++) body.Box(p+new Vector3(-side*1.51f,0,rib*.5f),new Vector3(.04f,2.2f,.055f),paint*.75f);
                    }
                    for(int stack=0;stack<3;stack++)
                        for(int tyre=0;tyre<3;tyre++)
                        {
                            var p=new Vector3(-side*9f,.28f*tyre,-6f+stack*1.5f);
                            body.Cylinder(p,.52f,.52f,.25f,new Color(.055f,.065f,.07f),12);
                            body.Cylinder(p+Vector3.up*.251f,.29f,.29f,.015f,metal,12);
                        }
                    // Distant low skyline and planted trees give the yard a setting, not a city obstacle course.
                    for(int i=0;i<3;i++)
                    {
                        float height=8+i*3;
                        body.Box(new Vector3(side*(13+i*4),height*.5f,-15+i*17),new Vector3(9,height,12),new Color(.27f,.36f,.40f));
                        var foot=new Vector3(-side*12,0,-17+i*16);
                        body.Cylinder(foot,.18f,.12f,2.7f,new Color(.28f,.23f,.17f),7);
                        body.Rock(foot+Vector3.up*3.5f,new Vector3(2.7f,2.3f,2.7f),new Color(.24f,.36f,.27f),section+i);
                    }
                    Render(depot,"Hangar, shutters, tyres and trees",body,surface);
                    Render(depot,"Practice depot windows",glass,windows);
                    var apron=Root("Training loading apron",at,side*12.5f);
                    var props=new WorldShiftArtMesh();
                    for(int i=-2;i<=2;i++)
                    {
                        // Cones and parking paint stay beyond the road and its shoulder.
                        var p=new Vector3(0,.04f,i*4.5f);
                        props.Box(p,new Vector3(.70f,.07f,.70f),metal);
                        props.Cylinder(p+Vector3.up*.04f,.26f,.04f,.65f,new Color(.89f,.44f,.20f),8);
                        props.Cylinder(p+Vector3.up*.32f,.16f,.13f,.14f,new Color(.88f,.86f,.73f),8);
                        props.Box(new Vector3(side*4,.03f,i*5),new Vector3(5,.025f,.07f),trim);
                    }
                    for(int post=-4;post<=4;post++)
                    {
                        float z=post*5;
                        props.Box(new Vector3(side*6,1.1f,z),new Vector3(.10f,2.2f,.10f),metal);
                        if(post<4) foreach(float y in new[]{.55f,1.25f,2f})
                            props.Beam(new Vector3(side*6,y,z),new Vector3(side*6,y,z+5),.04f,.035f,trim);
                    }
                    props.Box(new Vector3(side*3,3.5f,12),new Vector3(.16f,7,.16f),metal);
                    props.Box(new Vector3(side*3,7,12),new Vector3(1.5f,.18f,.8f),metal);
                    Render(apron,"Cones, parking bays and perimeter fencing",props,surface);
                    var lens=new WorldShiftArtMesh();lens.Box(new Vector3(side*3,6.87f,12),new Vector3(1.2f,.045f,.6f),new Color(.86f,.91f,.85f));
                    Render(apron,"Practice floodlight lens",lens,windows);
                    var fixture=new GameObject("Practice yard floodlight");fixture.transform.SetParent(apron,false);
                    fixture.transform.localPosition=new Vector3(side*3,6.7f,12);
                    fixture.transform.rotation=Quaternion.LookRotation(Vector3.down-apron.right*side*.45f,apron.forward);
                    var light=fixture.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(.85f,.92f,1f);
                    light.range=22;light.spotAngle=110;light.innerSpotAngle=65;light.intensity=12;light.shadows=LightShadows.None;lamps.Add(light);
                }
            }
            BuildTrainingLife();
        }
    }
}
