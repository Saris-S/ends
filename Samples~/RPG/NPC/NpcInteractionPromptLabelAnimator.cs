using System.Collections;
using TMPro;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    public class NpcInteractionPromptLabelAnimator : MonoBehaviour
    {
        #region Constants

        private const float ALPHA_HIDDEN = 0f;
        private const float ALPHA_VISIBLE = 1f;

        #endregion Constants

        [Tooltip("Zone relay. Empty - on this object or parent.")][SerializeField] private NpcInteractionTriggerRelay _triggerRelay;
        [Tooltip("Object with TMP. Empty - first TMP on Canvas.")][SerializeField] private GameObject _promptLabel;
        [Tooltip("Fade duration, sec. 0 - instant.")][Range(0f, 20f)][SerializeField] private float _fadeDurationSeconds = 0.5f;

        private float _labelAlpha;
        private bool _isFading;
        private TMP_Text _labelText;

        private void Awake()
        {   
            if (_promptLabel != null) _labelText = _promptLabel.GetComponent<TMP_Text>();

            _labelAlpha = ALPHA_HIDDEN;
            ApplyAlpha(_labelAlpha);
        }

        private void OnEnable()
        {
            _triggerRelay.PlayerInRangeChanged += OnPlayerInRangeChanged;
            _isFading = true;
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        private void OnDisable()
        {
            if (_triggerRelay != null) _triggerRelay.PlayerInRangeChanged -= OnPlayerInRangeChanged;

            StopAllCoroutines();
        }

        private void OnPlayerInRangeChanged(bool isInRange)
        {
            if (isInRange && _isFading)
            {
                _isFading = false;
                StopAllCoroutines();
                StartCoroutine(FadeIn());
            }
            else if (!isInRange && !_isFading)
            {
                _isFading = true;
                StopAllCoroutines();
                StartCoroutine(FadeOut());
            }
        }

        private IEnumerator FadeOut()
        {
            yield return AnimateAlpha(ALPHA_HIDDEN);
        }

        private IEnumerator FadeIn()
        {
            yield return AnimateAlpha(ALPHA_VISIBLE);
        }

        private IEnumerator AnimateAlpha(float targetAlpha)
        {
            if (_fadeDurationSeconds <= 0f)
            {
                _labelAlpha = targetAlpha;
                ApplyAlpha(_labelAlpha);
                yield break;
            }

            float startAlpha = _labelAlpha;
            float elapsed = 0f;

            while (elapsed < _fadeDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDurationSeconds);
                _labelAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                ApplyAlpha(_labelAlpha);
                yield return null;
            }

            _labelAlpha = targetAlpha;
            ApplyAlpha(_labelAlpha);
        }

        private void ApplyAlpha(float amount)
        {
            if (_labelText == null) return;

            Color color = _labelText.color;
            color.a = ALPHA_VISIBLE * amount;
            _labelText.color = color;
        }
    }
}
