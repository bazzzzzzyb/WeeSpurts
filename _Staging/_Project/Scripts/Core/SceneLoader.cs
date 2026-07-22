using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeeSpurts.Core
{
    /// <summary>
    /// Loads scenes by name, asynchronously (so the game doesn't freeze on a
    /// scene change — important once real assets make scenes heavier).
    ///
    /// WHY a separate class instead of calling SceneManager everywhere?
    /// One place to later add loading screens, fades, and network-aware
    /// scene sync (Mirror has its own scene handling we'll route through here).
    ///
    /// SETUP: lives on the same GameObject as GameManager.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        /// <summary>True while a load is in progress.</summary>
        public bool IsLoading { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Load(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"SceneLoader: already loading, ignored request for '{sceneName}'.");
                return;
            }
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone)
                yield return null; // wait one frame, check again
            IsLoading = false;
        }
    }
}
