using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Default dialog View; talks to DialogRunner.</summary>
    public class DialogWindow : MonoBehaviour
    {
        #region Developer API

            // Default text bar, choices, and panel animations. Character sprites are optional (DialogCharacterPortraitPresenter).

            #region View - text bar

                /// <summary>Short message on text bar. No-op while a dialog runs.</summary>
                public void ShowTextBarMessage(string message) => ShowTextBarMessageImpl(message);

                /// <summary>Dev/diagnostic line on the text bar while a dialog runs. Does not replace the current line.</summary>
                public void ShowDiagnosticLineDuringDialog(string message) => ShowDiagnosticLineDuringDialogImpl(message);

            #endregion View - text bar

        #endregion Developer API

        #region Constants

        private const float CANVAS_SCALE_MIN = 0.001f;
        private const float TEXTBAR_TARGET_Y = -40f;
        private const int MIN_CHOICE_LINE_COUNT = 1;

        #endregion Constants

        [SerializeField] private GameObject _canvasDialogWindow;
        [SerializeField] private RectTransform _canvasRectTransform;
        [SerializeField] private Canvas _dialogCanvas;
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private TMP_Text _mainTextField;
        [Tooltip("Optional. Shown above the dialog line when DialogDebugger reports issues during an active dialog.")][SerializeField] private TMP_Text _diagnosticLineText;
        [SerializeField] private Image _backFade;
        [SerializeField] private RectTransform _textBar;
        [SerializeField] private RectTransform _choicePanel;
        [SerializeField] private DialogChoiceButtonUI _choiceButtonPrefab;

        [SerializeField] private float _overallAnimSpeed = 1f;
        [SerializeField] private float _fadeSpeed = 0.75f;
        [SerializeField] private float _textBarSpeed = 0.5f;
        [SerializeField] private float _textTypeSpeed = 0.02f;

        [SerializeField] private float _buttonWidth = 1280f;
        [SerializeField] private float _linePadding = 15f;
        [SerializeField] private float _textHeight = 40f;
        [SerializeField] private float _buttonPadding = 5f;

        private readonly List<DialogChoiceButtonUI> _choiceButtons = new List<DialogChoiceButtonUI>();
        private string _currentTypingLine;
        private Coroutine _typeTextCoroutine;
        private Coroutine _textBarMessageCoroutine;
        private Coroutine _panelCoroutine;
        private bool _isTextRunning;
        private bool _isTransientMessage;
        private bool _wasFrozenBeforeTransient;
        private bool _started = false;
        private DialogRunner _runner;

        public static DialogWindow Instance { get; private set; }

        private void ShowTextBarMessageImpl(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_runner.IsTalking) return;
            if (_textBarMessageCoroutine != null) StopCoroutine(_textBarMessageCoroutine);

            _textBarMessageCoroutine = StartCoroutine(ShowTextBarMessageRoutine(message));
        }

        private void ShowDiagnosticLineDuringDialogImpl(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_runner == null || !_runner.IsTalking) return;

            if (_diagnosticLineText != null)
            {
                _diagnosticLineText.gameObject.SetActive(true);
                _diagnosticLineText.text = message;
                return;
            }

            Debug.LogWarning("DialogWindow: Diagnostic Line Text is not assigned - assign it on DialogWindow to show in-dialog diagnostics on the text bar.", this);
        }

        private void ClearDiagnosticLine()
        {
            if (_diagnosticLineText == null) return;

            _diagnosticLineText.text = "";
            _diagnosticLineText.gameObject.SetActive(false);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("DialogWindow: duplicate instance - destroying this component.", this);
                Destroy(this);

                return;
            }

            Instance = this;
        }

        private void Start()
        {
            _runner = DialogRunner.Instance;
            if (_runner == null) throw new InvalidOperationException("DialogWindow: DialogRunner.Instance is missing - place DialogRunner in the scene.");
            if (_canvasDialogWindow == null) throw new InvalidOperationException("DialogWindow: Canvas Dialog Window is not assigned in the Inspector.");
            if (_canvasRectTransform == null) throw new InvalidOperationException("DialogWindow: Canvas Rect Transform is not assigned in the Inspector.");
            if (_dialogCanvas == null) throw new InvalidOperationException("DialogWindow: Dialog Canvas is not assigned in the Inspector.");
            if (_choicePanel == null) throw new InvalidOperationException("DialogWindow: Choice Panel is not assigned in the Inspector.");
            if (_choiceButtonPrefab == null) throw new InvalidOperationException("DialogWindow: Choice Button Prefab is not assigned in the Inspector.");
            if (_choiceButtonPrefab.Label == null) throw new InvalidOperationException("DialogWindow: choice button prefab has no Label assigned.");
            if (_choiceButtonPrefab.LayoutElement == null) throw new InvalidOperationException("DialogWindow: choice button prefab has no LayoutElement assigned.");
            if (_choiceButtonPrefab.RectTransform == null) throw new InvalidOperationException("DialogWindow: choice button prefab has no RectTransform assigned.");
            if (_choiceButtonPrefab.Button == null) throw new InvalidOperationException("DialogWindow: choice button prefab has no Button assigned.");
            if (_characterNameText == null) throw new InvalidOperationException("DialogWindow: Character Name Text is not assigned in the Inspector.");
            if (_mainTextField == null) throw new InvalidOperationException("DialogWindow: Main Text Field is not assigned in the Inspector.");
            if (_backFade == null) throw new InvalidOperationException("DialogWindow: Back Fade is not assigned in the Inspector.");
            if (_textBar == null) throw new InvalidOperationException("DialogWindow: Text Bar is not assigned in the Inspector.");

            UnsubscribeRunner();
            SubscribeRunner();
            ConfigureDecorativeRaycastTargets(false);
            _canvasDialogWindow.SetActive(false);
            _started = true;
        }

        private void OnEnable()
        {
            if (!_started) return;

            UnsubscribeRunner();
            SubscribeRunner();
        }

        private void OnDisable()
        {
            if (!_started) return;

            UnsubscribeRunner();
        }

        private void SubscribeRunner()
        {
            _runner.DialogStarted += HandleDialogStarted;
            _runner.LineReady += HandleLineReady;
            _runner.LineSkipRequested += HandleLineSkipRequested;
            _runner.ChoicesReady += HandleChoicesReady;
            _runner.ChoiceSelected += HandleChoiceSelected;
            _runner.DialogEnded += HandleDialogSessionClosed;
            _runner.DialogCancelled += HandleDialogSessionClosed;
        }

        private void UnsubscribeRunner()
        {
            _runner.DialogStarted -= HandleDialogStarted;
            _runner.LineReady -= HandleLineReady;
            _runner.LineSkipRequested -= HandleLineSkipRequested;
            _runner.ChoicesReady -= HandleChoicesReady;
            _runner.ChoiceSelected -= HandleChoiceSelected;
            _runner.DialogEnded -= HandleDialogSessionClosed;
            _runner.DialogCancelled -= HandleDialogSessionClosed;
        }

        private void Update()
        {
            if (!_isTransientMessage) return;
            if (!(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) return;

            if (_textBarMessageCoroutine != null)
            {
                StopCoroutine(_textBarMessageCoroutine);
                _textBarMessageCoroutine = null;
            }

            if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);

            _panelCoroutine = StartCoroutine(CloseTransientRoutine());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleDialogStarted(DialogSessionInfo info)
        {
            if (_isTransientMessage)
            {
                if (_textBarMessageCoroutine != null)
                {
                    StopCoroutine(_textBarMessageCoroutine);
                    _textBarMessageCoroutine = null;
                }

                _isTransientMessage = false;
                _mainTextField.text = "";
            }

            ClearDiagnosticLine();

            _canvasDialogWindow.SetActive(true);
            EnsureCanvasReady();
            ConfigureDecorativeRaycastTargets(false);
            ClearChoices();

            if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);

            _panelCoroutine = StartCoroutine(OpenSessionRoutine());
        }

        private IEnumerator OpenSessionRoutine()
        {
            yield return AnimatePanelIn();
            _panelCoroutine = null;

            _runner.NotifySessionOpened();
        }

        private void HandleLineReady(DialogLinePresentation line)
        {
            ClearChoices();
            StopTypewriter();
            ClearDiagnosticLine();

            if (line.IsInstant)
            {
                if (!string.IsNullOrEmpty(line.Text))
                {
                    _mainTextField.text = line.Text;
                    ApplySpeakerLabel(line.SpeakerKey, line.SpeakerDisplayName, line.SpeakerColor);
                }

                return;
            }

            ApplySpeakerLabel(line.SpeakerKey, line.SpeakerDisplayName, line.SpeakerColor);

            _typeTextCoroutine = StartCoroutine(TypeText(line.Text));
        }

        private void StopTypewriter()
        {
            if (_typeTextCoroutine != null)
            {
                StopCoroutine(_typeTextCoroutine);
                _typeTextCoroutine = null;
            }

            _isTextRunning = false;
        }

        private void HandleLineSkipRequested()
        {
            StopTypewriter();

            if (!string.IsNullOrEmpty(_currentTypingLine)) _mainTextField.text = _currentTypingLine;

            _runner.NotifyLinePresentationReady();
        }

        private void HandleChoicesReady(IReadOnlyList<DialogChoicePresentation> choices)
        {
            ClearChoices();
            _choicePanel.gameObject.SetActive(true);
            ConfigureDecorativeRaycastTargets(true);

            for (int i = 0; i < choices.Count; i++)
            {
                DialogChoicePresentation choice = choices[i];
                DialogChoiceButtonUI button = Instantiate(_choiceButtonPrefab, _choicePanel);

                TMP_Text buttonText = button.Label;
                buttonText.raycastTarget = false;
                buttonText.text = choice.Text;
                buttonText.textWrappingMode = TextWrappingModes.Normal;
                buttonText.overflowMode = TextOverflowModes.Overflow;
                buttonText.ForceMeshUpdate();

                int lineCount = buttonText.textInfo.lineCount;

                if (lineCount == 0) lineCount = MIN_CHOICE_LINE_COUNT;

                LayoutElement layoutElement = button.LayoutElement;
                float calculateHeight = _buttonPadding + (_textHeight + _linePadding) * lineCount;
                layoutElement.preferredHeight = calculateHeight;
                layoutElement.preferredWidth = _buttonWidth;

                RectTransform buttonRect = button.RectTransform;
                buttonRect.sizeDelta = new Vector2(_buttonWidth, calculateHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRect);

                int choiceIndex = choice.Index;
                button.Button.onClick.AddListener(() => _runner.SelectChoice(choiceIndex));
                _choiceButtons.Add(button);
            }
        }

        private void HandleChoiceSelected(DialogChoicePresentation choice)
        {
            ClearChoices();
        }

        private void HandleDialogSessionClosed(DialogSessionInfo info)
        {
            StopTypewriter();
            ClearDiagnosticLine();

            _mainTextField.text = "";
            _characterNameText.text = "";

            ClearChoices();

            if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);

            _panelCoroutine = StartCoroutine(CloseSessionRoutine());
        }

        private IEnumerator CloseSessionRoutine()
        {
            yield return HidePanelVisuals();

            _panelCoroutine = null;
            _runner.NotifySessionClosed();
        }

        private void ApplySpeakerLabel(string characterName, string sayerName, Color speakerColor)
        {
            _characterNameText.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(speakerColor)}>{sayerName}</color>";
        }

        private void EnsureCanvasReady()
        {
            if (_canvasRectTransform.localScale.x < CANVAS_SCALE_MIN || _canvasRectTransform.localScale.y < CANVAS_SCALE_MIN) _canvasRectTransform.localScale = Vector3.one;

            if (_dialogCanvas.renderMode == RenderMode.ScreenSpaceCamera && _dialogCanvas.worldCamera == null) _dialogCanvas.worldCamera = Camera.main;
        }

        private void ConfigureDecorativeRaycastTargets(bool choiceMode)
        {
            if (_backFade != null) _backFade.raycastTarget = false;
            if (_characterNameText != null) _characterNameText.raycastTarget = false;
            if (_mainTextField != null) _mainTextField.raycastTarget = false;
            if (_diagnosticLineText != null) _diagnosticLineText.raycastTarget = false;
            if (choiceMode) _choicePanel.SetAsLastSibling();
        }

        private void ClearChoices()
        {
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Destroy(_choiceButtons[i].gameObject);
            }

            _choiceButtons.Clear();

            _choicePanel.gameObject.SetActive(false);
        }

        private IEnumerator TypeText(string line)
        {
            _isTextRunning = true;
            _currentTypingLine = line;
            _mainTextField.text = "";
            StringBuilder sb = new StringBuilder(line.Length);

            foreach (char letter in line)
            {
                sb.Append(letter);
                _mainTextField.text = sb.ToString();
                yield return new WaitForSecondsRealtime(_textTypeSpeed);
            }

            _isTextRunning = false;
            _typeTextCoroutine = null;
            _runner.NotifyLinePresentationReady();
        }

        private IEnumerator ShowTextBarMessageRoutine(string message)
        {
            _isTransientMessage = true;
            _wasFrozenBeforeTransient = DialogRunner.QueryGameplayBlocked != null && DialogRunner.QueryGameplayBlocked();

            _canvasDialogWindow.SetActive(true);
            EnsureCanvasReady();
            ConfigureDecorativeRaycastTargets(false);
            ClearChoices();

            _characterNameText.text = "";
            _mainTextField.text = message;

            if (!_wasFrozenBeforeTransient)
            {
                DialogRunner.RequestGameplayFreeze(true, true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            yield return AnimatePanelIn();
            _textBarMessageCoroutine = null;
        }

        private IEnumerator CloseTransientRoutine()
        {
            if (!_wasFrozenBeforeTransient) DialogRunner.RequestGameplayFreeze(false, true);

            yield return HidePanelVisuals();
            _isTransientMessage = false;
            _panelCoroutine = null;
        }

        private IEnumerator AnimatePanelIn()
        {
            float elapsedTime = 0f;
            Vector2 startPos = new Vector2(_textBar.anchoredPosition.x, -Screen.height);
            Vector2 targetPos = new Vector2(_textBar.anchoredPosition.x, TEXTBAR_TARGET_Y);

            while (elapsedTime < _overallAnimSpeed)
            {
                float alpha = Mathf.Lerp(0f, 1f, elapsedTime / _fadeSpeed);
                _backFade.canvasRenderer.SetAlpha(alpha);
                _textBar.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsedTime / _textBarSpeed);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            _backFade.canvasRenderer.SetAlpha(1f);
            _textBar.anchoredPosition = targetPos;
        }

        private IEnumerator HidePanelVisuals()
        {
            float elapsedTime = 0f;
            Vector2 startPos = _textBar.anchoredPosition;
            Vector2 targetPos = new Vector2(startPos.x, -Screen.height);

            while (elapsedTime < _overallAnimSpeed)
            {
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / _fadeSpeed);
                _backFade.canvasRenderer.SetAlpha(alpha);
                _textBar.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsedTime / _textBarSpeed);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            _backFade.canvasRenderer.SetAlpha(0f);
            _textBar.anchoredPosition = targetPos;
            _canvasDialogWindow.SetActive(false);
        }
    }
}
