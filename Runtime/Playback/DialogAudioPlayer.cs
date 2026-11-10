using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Default Audio: plays LineReady.VoiceClip; Runner does not reference this class.</summary>
    public sealed class DialogAudioPlayer : MonoBehaviour
    {

        #region Developer API

            /// <summary>Stop current voice clip immediately.</summary>
            public void StopVoice() => StopVoiceImpl();

        #endregion Developer API



        [Tooltip("Plays Line voice clips from LineReady.VoiceClip. Required.")][SerializeField] private AudioSource _voiceAudioSource;

        private bool _started;
        private DialogRunner _runner;

        private void Start()
        {
            _runner = DialogRunner.Instance;
            if (_runner == null) throw new InvalidOperationException("DialogAudioPlayer: DialogRunner.Instance is missing - place DialogRunner in the scene.");
            if (_voiceAudioSource == null) throw new InvalidOperationException("DialogAudioPlayer: AudioSource is not assigned in the Inspector.");
            
            UnsubscribeRunner();
            SubscribeRunner();
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
            StopVoice();
        }

        private void SubscribeRunner()
        {
            _runner.LineReady += HandleLineReady;
            _runner.LineSkipRequested += HandleLineSkipRequested;
            _runner.ChoicesReady += HandleChoicesReady;
            _runner.DialogEnded += HandleSessionClosed;
            _runner.DialogCancelled += HandleSessionClosed;
        }

        private void UnsubscribeRunner()
        {
            _runner.LineReady -= HandleLineReady;
            _runner.LineSkipRequested -= HandleLineSkipRequested;
            _runner.ChoicesReady -= HandleChoicesReady;
            _runner.DialogEnded -= HandleSessionClosed;
            _runner.DialogCancelled -= HandleSessionClosed;
        }

        private void StopVoiceImpl()
        {
            if (_voiceAudioSource.isPlaying) _voiceAudioSource.Stop();

            _voiceAudioSource.clip = null;
        }

        private void HandleLineReady(DialogLinePresentation line)
        {
            StopVoiceImpl();

            if (line.VoiceClip == null) return;

            _voiceAudioSource.clip = line.VoiceClip;
            _voiceAudioSource.Play();
        }

        private void HandleLineSkipRequested()
        {
            StopVoiceImpl();
        }

        private void HandleChoicesReady(IReadOnlyList<DialogChoicePresentation> choices)
        {
            StopVoiceImpl();
        }

        private void HandleSessionClosed(DialogSessionInfo info)
        {
            StopVoiceImpl();
        }
    }
}
