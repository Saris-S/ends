using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Choice button prefab view.</summary>
    public sealed class DialogChoiceButtonUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private LayoutElement _layoutElement;
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Button _button;

        public TMP_Text Label => _label;
        public LayoutElement LayoutElement => _layoutElement;
        public RectTransform RectTransform => _rectTransform;
        public Button Button => _button;
    }
}