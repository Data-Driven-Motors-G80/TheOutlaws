using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Bounded local-light input for the shared stylized surfaces, including WebGL.</summary>
    public sealed class WorldShiftLocalLighting : MonoBehaviour
    {
        private const int Budget = 8;
        private readonly List<Light> sources = new List<Light>();
        private readonly List<Light> visible = new List<Light>();
        private readonly Vector4[] positions = new Vector4[Budget], colours = new Vector4[Budget], directions = new Vector4[Budget];
        private WorldShiftCity game;
        public int ActiveLightCount { get; private set; }
        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            foreach (var light in controller.GetComponentsInChildren<Light>(true))
                if ((light.type == LightType.Point || light.type == LightType.Spot)
                    && light.GetComponentInParent<WorldShiftPoliceEmergencyEffects>() == null) sources.Add(light);
            Refresh();
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (game == null || game.Player == null) return;
            visible.Clear();
            foreach (var light in sources)
                if (light != null && light.isActiveAndEnabled && light.intensity > 0f
                    && (light.transform.position - game.Player.position).sqrMagnitude < 90f * 90f) visible.Add(light);
            visible.Sort((a,b) => (a.transform.position-game.Player.position).sqrMagnitude.CompareTo((b.transform.position-game.Player.position).sqrMagnitude));
            ActiveLightCount = Mathf.Min(Budget, visible.Count);
            for (int i = 0; i < ActiveLightCount; i++)
            {
                var light = visible[i]; var p = light.transform.position; var f = light.transform.forward;
                bool spot = light.type == LightType.Spot;
                float outer = spot ? Mathf.Cos(light.spotAngle * .5f * Mathf.Deg2Rad) : -2f;
                float inner = spot ? Mathf.Cos(light.innerSpotAngle * .5f * Mathf.Deg2Rad) : -1f;
                positions[i] = new Vector4(p.x,p.y,p.z,1f/Mathf.Max(.01f,light.range*light.range));
                Color c = light.color.linear * Mathf.Min(light.intensity,16f) * .65f;
                colours[i] = new Vector4(c.r,c.g,c.b,outer);
                directions[i] = new Vector4(f.x,f.y,f.z,inner);
            }
            Shader.SetGlobalInt("_OutlawsLightCount",ActiveLightCount);
            Shader.SetGlobalVectorArray("_OutlawsLightPositions",positions);
            Shader.SetGlobalVectorArray("_OutlawsLightColours",colours);
            Shader.SetGlobalVectorArray("_OutlawsLightDirections",directions);
        }
        private void OnDisable()
        {
            ActiveLightCount = 0;
            Shader.SetGlobalInt("_OutlawsLightCount",0);
        }
    }
}
