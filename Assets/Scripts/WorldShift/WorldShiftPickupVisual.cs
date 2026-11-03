using TMPro;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Wide labeled supply blocks, distinct from tall X-marked hazards.</summary>
    public static class WorldShiftPickupVisual
    {
        public static void Build(Transform parent, string label, Material colour)
        {
            Cube(parent, "Pickup block", new Vector3(0f, .65f, 0f), new Vector3(2.7f, 1.15f, .8f), colour);
            Cube(parent, "Label backing", new Vector3(0f, .65f, -.415f), new Vector3(2.5f, .95f, .04f),
                RuntimeMaterials.Coloured(new Color(.035f, .045f, .055f)));
            var face = new GameObject("Pickup label", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            face.transform.SetParent(parent, false);
            face.transform.localPosition = new Vector3(0f, .65f, -.445f);
            face.font = TMP_Settings.defaultFontAsset;
            face.text = label;
            face.color = Color.white;
            face.fontStyle = FontStyles.Bold;
            face.alignment = TextAlignmentOptions.Center;
            face.textWrappingMode = TextWrappingModes.NoWrap;
            face.enableAutoSizing = true;
            face.fontSizeMin = 3f; face.fontSizeMax = 6f;
            face.rectTransform.sizeDelta = new Vector2(2.3f, .78f);
        }

        private static void Cube(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = centre;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.GetComponent<Collider>().enabled = false;
        }
    }
}
