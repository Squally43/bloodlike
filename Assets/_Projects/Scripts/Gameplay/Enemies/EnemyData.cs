using System.Collections.Generic;
using UnityEngine;

namespace WH.Gameplay.Enemies
{
    /// <summary>Scriptable enemy: stats, intent scripting, and harvest data.</summary>
    [CreateAssetMenu(menuName = "WH/Enemies/EnemyData", fileName = "SO_Enemy_")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName = "New Enemy";

        [Header("Stats")]
        [Min(1)][SerializeField] private int maxHp = 30;
        [Min(0)][SerializeField] private int startingBlock;

        [Header("Intents")]
        [Tooltip("If true: choose a random step each turn. If false: step through the list and loop.")]
        [SerializeField] private bool randomizeIntents;
        [SerializeField] private List<WH.Gameplay.EnemyIntentStep> intents = new();

        [Header("Threat Mark Priority (what they try to steal on player loss)")]
        [SerializeField] private List<WH.Gameplay.BodyTag> threatMarkPriority = new();

        [Header("Harvestable Nodes (controls post-win reward families/rarities)")]
        [SerializeField] private List<WH.Gameplay.HarvestNode> harvestables = new();

        #region Public API
        public string Id => id;
        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public int StartingBlock => startingBlock;
        public bool RandomizeIntents => randomizeIntents;
        public IReadOnlyList<WH.Gameplay.EnemyIntentStep> Intents => intents;
        public IReadOnlyList<WH.Gameplay.BodyTag> ThreatMarkPriority => threatMarkPriority;
        public IReadOnlyList<WH.Gameplay.HarvestNode> Harvestables => harvestables;
        #endregion

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            if (string.IsNullOrEmpty(id))
                id = System.Guid.NewGuid().ToString("N");
            maxHp = Mathf.Max(1, maxHp);
            startingBlock = Mathf.Max(0, startingBlock);
        }

        public override string ToString() => $"{displayName} (HP:{maxHp})";
    }
}

