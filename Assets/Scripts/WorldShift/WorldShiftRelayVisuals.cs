using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Prototype launch arrows and landing targets, deliberately different from pit warnings.</summary>
    public sealed partial class WorldShiftRelayVisuals : MonoBehaviour
    {
        private sealed class View
        {
            public ShiftRelay Data;
            public GameObject Root, Ramp, SourceVoid, SourceRecoveryVoid, DestinationVoid, Landing, Preview, RampPreview;
        }
        private readonly List<View> views = new List<View>();
        private readonly List<Material> owned = new List<Material>();
        private WorldShiftRoute route;
        private Material boost, white, dark;

        public void Build(WorldShiftCity controller, WorldShiftRoute sharedRoute)
            => Build(sharedRoute, controller.RelayLayout, controller.LevelMode && controller.Environment == ShiftLevel.Ocean);

        public void Build(WorldShiftRoute sharedRoute, ShiftRelay[] sections, bool openWater = false)
        {
            route = sharedRoute;
            boost = Unlit(new Color(.25f, 1f, .54f));
            white = Unlit(new Color(.9f, 1f, .96f));
            dark = Unlit(new Color(.025f, .04f, .07f));
            WorldShiftLevelDropScenery drops = null;
            if (route.Course != null)
            {
                drops = gameObject.AddComponent<WorldShiftLevelDropScenery>();
                drops.Bind(GetComponentInParent<WorldShiftCity>(), route);
            }
            foreach (var data in sections)
            {
                var view = new View { Data = data, Root = Child(transform, "Relay " + data.Number + " - launch and landing") };
                views.Add(view);
                view.Ramp = Child(view.Root.transform, "Green boost arrows on the actual uphill deck");
                for (float d = data.Ramp + 3f; d < data.Takeoff - 2f; d += 6f)
                {
                    RoadPathPoint p = route.Sample(d);
                    if (d < data.Ramp + 25f)
                        Box(view.Ramp.transform, "Full-road acceleration strip", p.Position + p.Up * .055f,
                            Quaternion.LookRotation(p.Forward, p.Up), new Vector3(9.3f, .06f, 1.15f), boost);
                    foreach (float x in new[] { -3.2f, 0f, 3.2f })
                        Line(view.Ramp.transform, "Forward boost chevron", white, .11f,
                            p.Position + p.Right * (x - .65f) + p.Up * .10f - p.Forward * .8f,
                            p.Position + p.Right * x + p.Up * .10f + p.Forward * .4f,
                            p.Position + p.Right * (x + .65f) + p.Up * .10f - p.Forward * .8f);
                }
                view.RampPreview = Child(view.Root.transform, "Missing ramp dashed outline");
                EdgeFrame(view.Ramp.transform, data.Ramp, data.Takeoff, false, "Ramp");
                EdgeFrame(view.RampPreview.transform, data.Ramp, data.Takeoff, true, "Missing ramp");
                Threshold(view.Ramp.transform, data.Takeoff, false, "Takeoff edge");
                Threshold(view.RampPreview.transform, data.Takeoff, true, "Missing takeoff edge");
                if (route.Course != null && !route.Course.Training && route.Course.Environment == ShiftLevel.Mountain)
                    BuildSkiJump(view.Ramp.transform, data);
                BuildSign(view.Root.transform, route.Sample(data.Ramp - 22f), data.Source, data.Destination, true);
                // The destination marker belongs to the touchdown threshold, never the approach ramp.
                BuildSign(view.Root.transform, route.Sample(data.Landing), data.Destination, data.Destination, false);
                view.SourceVoid = Child(view.Root.transform, "Launch world missing landing");
                view.DestinationVoid = Child(view.Root.transform, "Landing world missing ramp");
                view.SourceRecoveryVoid = Child(view.Root.transform, "Launch world recovery before successful landing");
                // Level scenery is continuous across both worlds; only the road changes solidity.
                if (drops != null) drops.BuildJump(view.Root.transform, data);
                else if (!openWater)
                {
                    WorldShiftPitVisuals.Build(view.SourceVoid.transform, route,
                        new ShiftHazard(data.Source, ShiftHazardKind.Gap, data.Takeoff, route.Course != null ? data.Landing : data.End, 7), false);
                    if (route.Course != null)
                        WorldShiftPitVisuals.Build(view.SourceRecoveryVoid.transform, route,
                            new ShiftHazard(data.Source, ShiftHazardKind.Gap, data.Landing, data.End, 7), false);
                    WorldShiftPitVisuals.Build(view.DestinationVoid.transform, route,
                        new ShiftHazard(data.Destination, ShiftHazardKind.Gap, data.Ramp, data.Landing, 7), false);
                }
                view.Landing = Child(view.Root.transform, "Solid landing markers");
                view.Preview = Child(view.Root.transform, "Other-world landing outline - no road here yet");
                LandingFrame(view.Landing.transform, data, false);
                LandingFrame(view.Preview.transform, data, true);
            }
            SetWorld(ShiftWorld.Day);
            Refresh(0f);
        }

        public void SetWorld(ShiftWorld world)
        {
            foreach (var view in views)
            {
                bool launch = world == view.Data.Source;
                bool recovered = route.IsLandingRecovered(view.Data);
                view.Ramp.SetActive(launch);
                view.RampPreview.SetActive(!launch);
                view.SourceVoid.SetActive(launch);
                view.SourceRecoveryVoid.SetActive(launch && !recovered);
                view.DestinationVoid.SetActive(!launch);
                view.Landing.SetActive(!launch || recovered);
                view.Preview.SetActive(launch && !recovered);
            }
        }

        public void Refresh(float distance)
        {
            foreach (var view in views)
            {
                bool visible = distance >= view.Data.Ramp - 180f && distance < view.Data.End + 80f;
                if (view.Root.activeSelf != visible) view.Root.SetActive(visible);
            }
        }

        private void LandingFrame(Transform parent, ShiftRelay jump, bool dashed)
        {
            EdgeFrame(parent, jump.Landing, Mathf.Min(jump.Landing + 24f, jump.End), dashed, "Landing");
            Threshold(parent, jump.Landing, dashed, "Landing threshold");
            LandingCornerMarkers(parent, jump, dashed);
            if (dashed) return;
            // Runway bars exist only when the landing deck really exists.
            for (int i = 0; i < 8; i++)
            {
                var p = route.Sample(jump.Landing + 2f);
                float available = p.Width - 1.2f;
                float x = ((i + .5f) / 8f - .5f) * available;
                Box(parent, "Touchdown runway bar", p.Position + p.Right*x + p.Up*.07f,
                    Quaternion.LookRotation(p.Forward,p.Up), new Vector3(available/8f*.55f,.035f,2.4f), white);
            }
        }

        private void EdgeFrame(Transform parent, float start, float end, bool dashed, string label)
        {
            for(float d=start;d<end;d+=4f)
                foreach(float side in new[]{-1f,1f})
                {
                    var a=route.Sample(d);var b=route.Sample(Mathf.Min(end,d+(dashed?2f:4f)));
                    BorderedLine(parent,label+" edge",a.Position+a.Right*side*(a.Width*.5f-.25f)+a.Up*.13f,
                        b.Position+b.Right*side*(b.Width*.5f-.25f)+b.Up*.13f);
                }
        }
        private void Threshold(Transform parent,float at,bool dashed,string label)
        {
            var p=route.Sample(at);float half=p.Width*.5f-.25f;
            if(!dashed) BorderedLine(parent,label,p.Position-p.Right*half+p.Up*.13f,p.Position+p.Right*half+p.Up*.13f);
            else for(float x=-half;x<half;x+=1.4f)
                BorderedLine(parent,label,p.Position+p.Right*x+p.Up*.13f,
                    p.Position+p.Right*Mathf.Min(x+.8f,half)+p.Up*.13f);
        }
        private void BorderedLine(Transform parent,string label,Vector3 a,Vector3 b, Vector3? faceOffset = null)
        {
            float width = label.StartsWith("Landing") ? .55f : .30f;
            Line(parent,label+" dark backing",dark,width+.34f,a,b);
            Vector3 offset = faceOffset ?? Vector3.up*.035f;
            Line(parent,label,white,width,a+offset,b+offset);
        }

        // Upright corner brackets give the touchdown edge a silhouette against the drop below.
        // Keep the middle open and every marker outside the drivable road width.
        private void LandingCornerMarkers(Transform parent, ShiftRelay jump, bool dashed)
        {
            var p = route.Sample(jump.Landing);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 foot = p.Position + p.Right * side * (p.Width * .5f + .32f);
                if (dashed)
                {
                    for (float y = .15f; y < 2.1f; y += .75f)
                        BorderedLine(parent, "Landing corner dash", foot+p.Up*y, foot+p.Up*(y+.45f), -p.Forward*.04f);
                }
                else BorderedLine(parent, "Landing corner", foot+p.Up*.15f, foot+p.Up*2.1f, -p.Forward*.04f);
                // Outward caps never cover the landing surface or imply an overhead barrier.
                BorderedLine(parent, "Landing corner cap", foot+p.Up*2.1f, foot+p.Up*2.1f+p.Right*side*.8f);
            }
        }

        private void BuildSign(Transform parent,RoadPathPoint point,ShiftWorld source,ShiftWorld destination,bool sequence)
        {
            var root=Child(parent,sequence?"Launch and landing icon sign":"Destination icon at touchdown").transform;
            root.SetPositionAndRotation(point.Position+point.Right*(point.Width*.5f+2f),Quaternion.LookRotation(point.Forward,point.Up));
            float width=3.4f, height=sequence?2f:3.4f;
            float faceHeight=sequence?2.7f:3.5f;
            Vector3 W(float x,float y,float z=0f)=>root.TransformPoint(new Vector3(x,y,z));
            Box(root,"Sign post",W(0,1.25f),root.rotation,new Vector3(.12f,2.5f,.12f),dark);
            Box(root,"Sign border",W(0,faceHeight),root.rotation,new Vector3(width+.12f,height+.12f,.12f),white);
            Box(root,"Sign dark face",W(0,faceHeight,-.075f),root.rotation,new Vector3(width,height,.04f),dark);
            // World-space UI reuses the same sun/moon artwork as the HUD, without text or input.
            var canvasObject=new GameObject("World symbols",typeof(RectTransform),typeof(Canvas));
            canvasObject.transform.SetParent(root,false);
            canvasObject.transform.localPosition=new Vector3(0,faceHeight,-.11f);
            canvasObject.transform.localScale=Vector3.one*.01f;
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta=new Vector2(width*100f,height*100f);
            void Icon(ShiftWorld world,float x,float y,float size)
            {
                var obj=new GameObject(world+" symbol",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                var rect=(RectTransform)obj.transform;rect.SetParent(canvasObject.transform,false);
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=Vector2.one*size;
                var image=obj.GetComponent<UnityEngine.UI.Image>();image.sprite=WorldShiftIcons.Get(world==ShiftWorld.Day?ShiftIcon.Sun:ShiftIcon.Moon);
                image.color=world==ShiftWorld.Day?new Color(1f,.84f,.3f):Color.white;image.raycastTarget=false;
            }
            if(!sequence){Icon(destination,0,0,285f);return;}
            Icon(source,-100,32,85);Icon(destination,100,32,85);
            Line(root,"Takeoff pictogram",white,.09f,W(-1.48f,2.08f,-.13f),W(-.68f,2.45f,-.13f),W(-.68f,2.08f,-.13f));
            Line(root,"Landing pictogram",white,.09f,W(.6f,2.18f,-.13f),W(1.4f,2.18f,-.13f));
            Line(root,"Sequence arrow",white,.09f,W(-.28f,2.75f,-.13f),W(.28f,2.75f,-.13f));
            Line(root,"Sequence arrowhead",white,.09f,W(.08f,2.92f,-.13f),W(.28f,2.75f,-.13f),W(.08f,2.58f,-.13f));
        }

        private static GameObject Child(Transform parent, string name)
        { var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj; }
        private Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            owned.Add(material);
            return material;
        }
        private static void Line(Transform parent, string name, Material material, float width, params Vector3[] points)
        {
            var line = Child(parent, name).AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
        }
        private static void Box(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.localScale = size;
            obj.GetComponent<Collider>().enabled = false;
            obj.GetComponent<Renderer>().sharedMaterial = material;
        }
        private void OnDestroy() { foreach (var material in owned) if (material != null) Destroy(material); }
    }
}
