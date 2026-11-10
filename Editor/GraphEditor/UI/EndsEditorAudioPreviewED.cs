#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsEditorAudioPreviewED
    {
        private static MethodInfo _playMethod;
        private static MethodInfo _pauseMethod;
        private static MethodInfo _resumeMethod;
        private static MethodInfo _stopMethod;
        private static MethodInfo _isPlayingMethod;
        private static MethodInfo _getPositionMethod;
        private static bool _resolved;

        private static AudioClip _activeClip;
        private static bool _isPaused;
        private static bool _playbackActive;
        private static float _manualTimeSeconds;
        private static EditorWindow _repaintTarget;
        private static bool _updateSubscribed;

        internal static AudioClip ActiveClip => _activeClip;

        internal static void SetRepaintTarget(EditorWindow window)
        {
            _repaintTarget = window;
        }

        internal static void ReleaseRepaintTarget(EditorWindow window)
        {
            if (_repaintTarget != window) return;

            Stop();
            _repaintTarget = null;
        }

        internal static bool IsActivelyPlaying(AudioClip clip)
        {
            if (clip == null || _activeClip != clip || _isPaused) return false;
            return IsPreviewPlaying();
        }

        internal static float GetDuration(AudioClip clip)
        {
            if (clip == null) return 0f;
            return clip.length;
        }

        internal static float GetCurrentTime(AudioClip clip)
        {
            if (clip == null || _activeClip != clip) return 0f;
            if (!_playbackActive || _isPaused) return Mathf.Clamp(_manualTimeSeconds, 0f, clip.length);
            if (IsPreviewPlaying())
            {
                float position = GetPreviewPositionSeconds();
                return Mathf.Clamp(position, 0f, clip.length);
            }

            return Mathf.Clamp(_manualTimeSeconds, 0f, clip.length);
        }

        internal static void TogglePlayPause(AudioClip clip)
        {
            if (clip == null) return;
            if (IsActivelyPlaying(clip))
            {
                Pause();
                return;
            }
            if (_activeClip == clip && _isPaused)
            {
                Resume();
                return;
            }

            float startTime = 0f;

            if (_activeClip == clip) startTime = _manualTimeSeconds;

            PlayFromTime(clip, startTime);
        }

        internal static void PlayFromTime(AudioClip clip, float timeSeconds)
        {
            if (clip == null) return;

            Resolve();

            if (_playMethod == null) return;

            StopPreviewEngine();
            _activeClip = clip;
            _isPaused = false;
            _playbackActive = true;
            _manualTimeSeconds = Mathf.Clamp(timeSeconds, 0f, clip.length);

            int startSample = 0;

            if (clip.samples > 0 && clip.length > 0f) startSample = Mathf.Clamp(Mathf.RoundToInt((_manualTimeSeconds / clip.length) * clip.samples), 0, Mathf.Max(0, clip.samples - 1));

            if (_playMethod.GetParameters().Length >= 3) _playMethod.Invoke(null, new object[] { clip, startSample, false });
            else _playMethod.Invoke(null, new object[] { clip });

            SubscribeUpdate();

            if (_repaintTarget != null) _repaintTarget.Repaint();
        }

        internal static void Seek(AudioClip clip, float timeSeconds)
        {
            if (clip == null) return;

            Resolve();

            // Move playhead only - never start/restart audio here (scrub uses pause + resume on mouse up).
            _activeClip = clip;
            _manualTimeSeconds = Mathf.Clamp(timeSeconds, 0f, clip.length);
            _isPaused = false;
            _playbackActive = false;
            StopPreviewEngine();
            UnsubscribeUpdate();

            if (_repaintTarget != null) _repaintTarget.Repaint();
        }

        internal static void Pause()
        {
            if (_activeClip == null) return;

            Resolve();

            _manualTimeSeconds = GetCurrentTime(_activeClip);

            if (_pauseMethod != null) _pauseMethod.Invoke(null, null);
            else StopPreviewEngine();

            _isPaused = true;
            _playbackActive = false;
            UnsubscribeUpdate();

            if (_repaintTarget != null) _repaintTarget.Repaint();
        }

        internal static void Resume()
        {
            if (_activeClip == null) return;

            Resolve();

            if (_resumeMethod != null)
            {
                _isPaused = false;
                _playbackActive = true;
                _resumeMethod.Invoke(null, null);

                if (!IsPreviewPlaying()) PlayFromTime(_activeClip, _manualTimeSeconds);
                else
                {
                    SubscribeUpdate();

                    if (_repaintTarget != null) _repaintTarget.Repaint();
                }

                return;
            }

            PlayFromTime(_activeClip, _manualTimeSeconds);
        }

        internal static void Stop()
        {
            Resolve();
            StopPreviewEngine();
            _isPaused = false;
            _playbackActive = false;
            _activeClip = null;
            _manualTimeSeconds = 0f;
            UnsubscribeUpdate();

            if (_repaintTarget != null) _repaintTarget.Repaint();
        }

        internal static string FormatTime(float timeSeconds)
        {
            if (timeSeconds < 0f) timeSeconds = 0f;

            int totalSec = Mathf.FloorToInt(timeSeconds);
            int minutes = totalSec / 60;
            int seconds = totalSec % 60;
            int hundredths = Mathf.FloorToInt((timeSeconds - totalSec) * 100f);

            if (hundredths > 99) hundredths = 99;
            return minutes + ":" + seconds.ToString("00") + "." + hundredths.ToString("00");
        }

        private static bool IsPreviewPlaying()
        {
            Resolve();

            if (_isPlayingMethod == null) return false;

            object result = _isPlayingMethod.Invoke(null, null);

            if (result is bool playing) return playing;
            return false;
        }

        private static float GetPreviewPositionSeconds()
        {
            Resolve();

            if (_getPositionMethod == null) return _manualTimeSeconds;

            object result = _getPositionMethod.Invoke(null, null);

            if (result is float seconds) return seconds;
            return _manualTimeSeconds;
        }

        private static void StopPreviewEngine()
        {
            if (_stopMethod == null) return;

            if (_stopMethod.GetParameters().Length == 0) _stopMethod.Invoke(null, null);
            else _stopMethod.Invoke(null, new object[] { true });
        }

        private static void SubscribeUpdate()
        {
            if (_updateSubscribed) return;

            EditorApplication.update += OnEditorUpdate;
            _updateSubscribed = true;
        }

        private static void UnsubscribeUpdate()
        {
            if (!_updateSubscribed) return;

            EditorApplication.update -= OnEditorUpdate;
            _updateSubscribed = false;
        }

        private static void OnEditorUpdate()
        {
            if (_activeClip == null || _repaintTarget == null)
            {
                Stop();

                return;
            }

            if (_playbackActive && !_isPaused && !IsPreviewPlaying())
            {
                _manualTimeSeconds = 0f;
                _isPaused = false;
                _playbackActive = false;
                _activeClip = null;
                UnsubscribeUpdate();
            }

            if (_repaintTarget != null) _repaintTarget.Repaint();
        }

        private static void Resolve()
        {
            if (_resolved) return;

            _resolved = true;

            Type audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

            if (audioUtilType == null) return;

            _playMethod = audioUtilType.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

            if (_playMethod == null) _playMethod = audioUtilType.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (_playMethod == null) _playMethod = audioUtilType.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip) }, null);

            _pauseMethod = audioUtilType.GetMethod("PausePreviewClip", BindingFlags.Static | BindingFlags.Public);
            _resumeMethod = audioUtilType.GetMethod("ResumePreviewClip", BindingFlags.Static | BindingFlags.Public);
            _isPlayingMethod = audioUtilType.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public);
            _getPositionMethod = audioUtilType.GetMethod("GetPreviewClipPosition", BindingFlags.Static | BindingFlags.Public);
            _stopMethod = audioUtilType.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);

            if (_stopMethod == null) _stopMethod = audioUtilType.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);
        }
    }
}
#endif
