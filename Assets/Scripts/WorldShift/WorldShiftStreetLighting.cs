using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private readonly List<Renderer> lampGlows = new List<Renderer>();
        private readonly List<Renderer> lampLenses = new List<Renderer>();
        private Mesh glowQuad;
        private Material glowMaterial;
        private MaterialPropertyBlock lampProperties;

        private void BuildCityRoadFinish()
        {
            Material day = MakeMaterial("City textured asphalt Day",0f);
            Material nightRoad = MakeMaterial("City reflective asphalt Night",0f);
            day.SetColor("_BaseColor",new Color(.22f,.23f,.24f));
            nightRoad.SetColor("_BaseColor",new Color(.085f,.105f,.14f));
            foreach(var material in new[]{day,nightRoad})
            {
                material.SetFloat("_Grain",.85f);
                material.SetFloat("_Smoothness",.55f);
                material.SetFloat("_Specular",.42f);
            }
            route.SetSurfaceMaterials(day,nightRoad);
            glowMaterial = new Material(Resources.Load<Shader>("WorldShiftLampGlow"));
            materials.Add(glowMaterial);
            glowQuad = new Mesh { name="Shared streetlamp optical glow quad" };
            glowQuad.vertices = new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            glowQuad.uv = new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            glowQuad.triangles = new[]{0,1,2,0,2,3};
            glowQuad.bounds = new Bounds(Vector3.zero,Vector3.one*2f); meshes.Add(glowQuad);
            lampProperties = new MaterialPropertyBlock();
        }

        private void DressStreetlight(GameObject root,float side)
        {
            // Taller fixtures and inward arms put their light onto the carriageway.
            var pole=root.transform.Find("Streetlight Pole");
            pole.localPosition=new Vector3(0,2.75f,0);pole.localScale=new Vector3(.14f,5.5f,.14f);
            var arm=root.transform.Find("Streetlight Arm");
            arm.localPosition=new Vector3(-side*.45f,5.5f,0);arm.localScale=new Vector3(1.8f,.11f,.11f);
            var lens=root.transform.Find("Streetlight Lamp");lens.localPosition=new Vector3(-side*1.2f,5.4f,0);
            foreach(var light in root.GetComponentsInChildren<Light>())
            {
                light.type=LightType.Spot;light.shadows=LightShadows.None;
                light.range=17f;light.intensity=12f;light.spotAngle=110f;light.innerSpotAngle=66f;
                light.color=new Color(1f,.76f,.47f);light.renderMode=LightRenderMode.ForcePixel;
                // The prefab's lens faces down; a slight inward tilt centres the pool on the road edge.
                light.transform.rotation=Quaternion.LookRotation(-root.transform.up-root.transform.right*side*.24f,root.transform.forward);
                lamps.Add(light);lampLenses.Add(lens.GetComponent<Renderer>());
                var halo=new GameObject("Streetlamp optical glow",typeof(MeshFilter),typeof(MeshRenderer));
                halo.transform.SetParent(root.transform,false);halo.transform.position=light.transform.position;
                halo.GetComponent<MeshFilter>().sharedMesh=glowQuad;
                var renderer=halo.GetComponent<MeshRenderer>();renderer.sharedMaterial=glowMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                lampGlows.Add(renderer);
            }
        }
        private void RefreshStreetlightFinish()
        {
            for(int i=0;i<lampLenses.Count;i++)
            {
                lampProperties.Clear();
                lampProperties.SetColor("_EmissionColor",night?new Color(3.5f,2.1f,.8f):new Color(.06f,.045f,.025f));
                lampLenses[i].SetPropertyBlock(lampProperties);
                lampGlows[i].enabled=lamps[i].isActiveAndEnabled;
            }
        }
    }
}
