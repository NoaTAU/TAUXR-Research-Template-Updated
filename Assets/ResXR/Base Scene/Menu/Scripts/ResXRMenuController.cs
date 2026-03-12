using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResXR.Menu
{
    /// <summary>
    /// Orchestrates additive scene loading/unloading and transitions.
    /// Persistent scene is the "menu"; portals load one additive scene at a time.
    /// </summary>
    public class ResXRMenuController : ResXRSingleton<ResXRMenuController>
    {
        [Header("Menu scene name (this scene)")]
        [SerializeField] private string _menuSceneName;

        [Header("Fade durations")]
        [SerializeField] private float _fadeOutDuration = 2.5f;
        [SerializeField] private float _fadeInDuration = 1.5f;

        [Header("Optional PassthroughManager (same scene)")]
        [SerializeField] private PassthroughManager _passthroughManager;

        private bool _isTransitioning;
        private string _currentAdditiveSceneName;

        public string CurrentAdditiveSceneName => _currentAdditiveSceneName;
        public bool IsTransitioning => _isTransitioning;

        private void Start()
        {
            if (string.IsNullOrEmpty(_menuSceneName))
                _menuSceneName = gameObject.scene.name;
        }

        /// <summary>
        /// Load target scene additively with optional passthrough. Call from MenuPortal.
        /// </summary>
        public void LoadPortal(string targetSceneName, bool launchWithPassthrough)
        {
            LoadPortalAsync(targetSceneName, launchWithPassthrough).Forget();
        }

        private async UniTaskVoid LoadPortalAsync(string targetSceneName, bool launchWithPassthrough)
        {
            if (_isTransitioning)
            {
                Debug.Log("[Menu] Ignoring LoadPortal: already loading/unloading.");
                return;
            }

            if (!string.IsNullOrEmpty(_currentAdditiveSceneName))
            {
                Debug.LogWarning($"[Menu] An additive scene is already loaded ('{_currentAdditiveSceneName}'). LoadPortal ignored; use ReturnToMenu first.");
                return;
            }

            _isTransitioning = true;
            Debug.Log($"[Menu] LoadPortal: scene='{targetSceneName}', passthrough={launchWithPassthrough}");

            if (ResXRPlayer.Instance == null)
            {
                Debug.LogError("[Menu] ResXRPlayer.Instance is null; cannot fade.");
                _isTransitioning = false;
                return;
            }

            gameObject.SetActive(false);

            await ResXRPlayer.Instance.FadeViewToColor(Color.black, _fadeOutDuration);

            if (_passthroughManager != null)
                _passthroughManager.SetPassthroughEnabled(launchWithPassthrough);
            else
                Debug.LogWarning("[Menu] PassthroughManager not assigned; passthrough state not applied.");

            if (ResXRSceneManager.Instance != null)
                await ResXRSceneManager.Instance.LoadActiveScene(targetSceneName);
            else
            {
                await SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
                Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
                if (targetScene.isLoaded)
                    SceneManager.SetActiveScene(targetScene);
            }

            _currentAdditiveSceneName = targetSceneName;
            await ResXRPlayer.Instance.FadeViewToColor(Color.clear, _fadeInDuration);

            _isTransitioning = false;
            Debug.Log($"[Menu] LoadPortal complete: '{targetSceneName}' active, passthrough={launchWithPassthrough}");
        }

        /// <summary>
        /// Unload current additive scene and return to menu. Call from BackPortal.
        /// </summary>
        public void ReturnToMenu()
        {
            ReturnToMenuAsync().Forget();
        }

        private async UniTaskVoid ReturnToMenuAsync()
        {
            if (_isTransitioning)
            {
                Debug.Log("[Menu] Ignoring ReturnToMenu: already loading/unloading.");
                return;
            }

            if (string.IsNullOrEmpty(_currentAdditiveSceneName))
            {
                Debug.Log("[Menu] ReturnToMenu: no additive scene loaded; nothing to unload.");
                return;
            }

            string toUnload = _currentAdditiveSceneName;
            _isTransitioning = true;
            Debug.Log($"[Menu] ReturnToMenu: unloading '{toUnload}'");

            if (ResXRPlayer.Instance == null)
            {
                Debug.LogError("[Menu] ResXRPlayer.Instance is null; cannot fade.");
                _isTransitioning = false;
                return;
            }
            await ResXRPlayer.Instance.FadeViewToColor(Color.black, _fadeOutDuration);

            if (ResXRSceneManager.Instance != null)
            {
                await ResXRSceneManager.Instance.UnloadActiveScene();
                _currentAdditiveSceneName = null;
            }
            else
            {


                await SceneManager.UnloadSceneAsync(toUnload);
                _currentAdditiveSceneName = null;

                Scene menuScene = SceneManager.GetSceneByName(_menuSceneName);
                if (menuScene.isLoaded)
                    SceneManager.SetActiveScene(menuScene);
            }
            if (_passthroughManager != null)
                _passthroughManager.ApplyMenuPassthrough();

            gameObject.SetActive(true);

            await ResXRPlayer.Instance.FadeViewToColor(Color.clear, _fadeInDuration);

            _isTransitioning = false;
            Debug.Log("[Menu] ReturnToMenu complete; active scene is menu.");
        }
    }
}
