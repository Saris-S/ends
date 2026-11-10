using UnityEngine;
using UnityEngine.UI;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Character portrait prefab view.</summary>
    public sealed class DialogCharacterPortraitSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private RectTransform _rectTransform;

        public Image Image => _image;
        public RectTransform RectTransform => _rectTransform;
    }
}