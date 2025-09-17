using UnityEngine;
using UnityEngine.SceneManagement;

namespace WH.Core
{
    /// <summary>
    /// Loads the Main scene additively at startup.
    /// Keeps Bootstrap persistent as the root of all managers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = "Main";

        private void Awake()
        {
            // Ensure Bootstrap is not destroyed on scene change
            DontDestroyOnLoad(gameObject);

            // If Main is not already loaded, load it additively
            if (!SceneManager.GetSceneByName(mainSceneName).isLoaded)
            {
                SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Additive);
            }
        }
    }
}

