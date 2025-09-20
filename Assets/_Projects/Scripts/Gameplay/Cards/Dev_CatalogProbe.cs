using UnityEngine;
using WH.Gameplay.Cards;

namespace WH.Gameplay.Debugging
{
    public sealed class Dev_CatalogProbe : MonoBehaviour
    {
        [SerializeField] private CardCatalog catalog;

        private void Start()
        {
            if (!catalog) { Debug.LogWarning("[CatalogProbe] No catalog set"); return; }
            var starter = catalog.BuildStarterDeck();
            Debug.Log($"[CatalogProbe] Starter size: {starter.Count}");
            foreach (var c in starter) Debug.Log($" - {c.DisplayName}");
        }
    }
}


