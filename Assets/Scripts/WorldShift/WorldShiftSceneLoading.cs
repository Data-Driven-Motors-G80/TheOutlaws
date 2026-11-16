using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>Immediate input feedback while assets load; persists until the new scene has rendered.</summary>
    public sealed class WorldShiftSceneLoading : MonoBehaviour
    {
        private static WorldShiftSceneLoading instance;
        private RectTransform fill, symbol;
        private float progress;
        [SerializeField, HideInInspector] private AudioClip preloadedMusic;
        public static bool Active => instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => instance = null;

        public static void Load(string scene, string destination, string music = null)
        {
            if (Active) return;
            var root = new GameObject("Worldshift scene loading");
            instance = root.AddComponent<WorldShiftSceneLoading>();
            DontDestroyOnLoad(root);
            instance.Build(destination);
            instance.StartCoroutine(instance.LoadScene(scene,music));
        }

        private void Build(string destination)
        {
            var canvas = WorldShiftMenu.CreateCanvas(transform,"Loading canvas");
            canvas.GetComponent<Canvas>().sortingOrder = 1000;
            var backdrop = WorldShiftMenu.Panel(canvas,"Loading backdrop",Vector2.zero,Vector2.one,WorldShiftUiStyle.Backdrop);
            backdrop.GetComponent<Image>().raycastTarget = true;
            symbol = WorldShiftMenu.Panel(canvas,"Loading sun",new Vector2(.5f,.66f),new Vector2(.5f,.66f),WorldShiftUiStyle.Gold);
            symbol.sizeDelta = new Vector2(34,34); symbol.GetComponent<Image>().sprite = WorldShiftIcons.Get(ShiftIcon.Sun);
            var title = WorldShiftMenu.Label(canvas,destination,.2f,.50f,.8f,.61f,38);
            WorldShiftTypography.Apply(title,FontStyles.Bold,38);
            title.color = WorldShiftUiStyle.Cream; title.alignment = TextAlignmentOptions.Center;
            var track = WorldShiftMenu.Panel(canvas,"Loading track",new Vector2(.35f,.455f),new Vector2(.65f,.46f),WorldShiftUiStyle.Track);
            fill = WorldShiftMenu.Panel(track,"Loading progress",Vector2.zero,new Vector2(.03f,1),WorldShiftUiStyle.Accent);
        }

        private IEnumerator LoadScene(string scene,string music)
        {
            // Let the click and loading surface reach the screen before doing any load work.
            yield return null;
            if (music != null)
            {
                var request = Resources.LoadAsync<AudioClip>(music);
                while (!request.isDone) { progress = .15f*request.progress; yield return null; }
                // Keep the clip rooted through Single-scene activation/unloading.
                preloadedMusic = request.asset as AudioClip;
                if (preloadedMusic != null && preloadedMusic.loadState == AudioDataLoadState.Unloaded)
                    preloadedMusic.LoadAudioData();
            }
            var operation = SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
            while (!operation.isDone)
            {
                progress = .15f+.85f*Mathf.Clamp01(operation.progress/.9f);
                yield return null;
            }
            progress = 1f;
            // New scene UI/camera gets one settled frame behind the loading surface.
            yield return null;
            Destroy(gameObject);
        }

        private void Update()
        {
            if (symbol != null) symbol.Rotate(0,0,-45f*Time.unscaledDeltaTime);
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Max(.03f,progress),1);
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
