using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A small rear-facing camera, rendered to a texture for the HUD. It sits just above
/// and ahead of the player and always aims at the chasing police, so they stay in frame.
/// </summary>
public sealed class RearViewCamera : MonoBehaviour
{
    private const int TextureWidth = 512;
    private const int TextureHeight = 128;

    private Transform player;
    private ChaseCar[] police;
    private Camera view;

    public RenderTexture Texture { get; private set; }

    public static RearViewCamera Create(Transform player, ChaseCar[] police)
    {
        var cameraObject = new GameObject("Rear View Camera");
        RearViewCamera rear = cameraObject.AddComponent<RearViewCamera>();
        rear.player = player;
        rear.police = police;
        rear.Texture = new RenderTexture(TextureWidth, TextureHeight, 24) { name = "Rear View" };

        rear.view = cameraObject.AddComponent<Camera>();
        rear.view.targetTexture = rear.Texture;
        rear.view.fieldOfView = 62f;
        rear.view.aspect = (float)TextureWidth / TextureHeight;
        rear.view.nearClipPlane = 0.3f;
        rear.view.farClipPlane = 140f;
        Camera main = Camera.main;
        if (main != null)
        {
            rear.view.clearFlags = main.clearFlags;
            rear.view.backgroundColor = main.backgroundColor;
            rear.view.cullingMask = main.cullingMask;
            rear.view.depth = main.depth - 1f;
        }

        UniversalAdditionalCameraData data = rear.view.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
        }

        ChaseCar.RearViewActive = true;
        rear.LateUpdate();
        return rear;
    }

    private void LateUpdate()
    {
        if (player == null) return;
        Camera main = Camera.main;
        if (main != null)
        {
            view.clearFlags = main.clearFlags;
            view.backgroundColor = main.backgroundColor;
        }
        Vector3 forward = player.forward;
        Vector3 up = player.up;
        // Above the car's middle, looking back: the rear of the player's car sits at the
        // bottom of the frame so you can judge how close the police are.
        transform.position = player.position + up * 1.9f + forward * 1.8f;

        Vector3 target = player.position - forward * 6f;
        if (police != null && police.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (ChaseCar car in police)
            {
                if (car == null || !car.gameObject.activeInHierarchy || (!car.isActiveAndEnabled && !car.IsFiniteRoute)) continue;
                sum += car.transform.position;
                count++;
            }
            if (count > 0) target = sum / count;
        }

        transform.rotation = Quaternion.LookRotation(target + up * 0.2f - transform.position, up);
    }

    /// <summary>Refresh the existing mirror immediately after an explicit simulation step.</summary>
    public void RefreshView() => LateUpdate();

    public void RenderPreview()
    {
        LateUpdate();
        if (view != null && view.targetTexture != null) view.Render();
    }

    private void OnDestroy()
    {
        ChaseCar.RearViewActive = false;
        if (view != null) view.targetTexture = null;
        if (Texture != null)
        {
            Texture.Release();
            Destroy(Texture);
        }
    }
}
