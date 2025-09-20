// in UI code or a presenter/controller:
using WH.Gameplay.Cards;
using WH.UI;
using UnityEngine;

public class CardPresenter : MonoBehaviour
{
    [SerializeField] private CardView view;

    public void Show(CardData data, System.Action<CardView> onClick = null, string pulseReadout = "")
    {
        var tint = CardFamilyColors.Get(data.Family);
        view.Bind(data, data.DisplayName, data.RulesLine, data.CostPulse, pulseReadout, tint, onClick);
    }
}
