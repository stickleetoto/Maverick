using UnityEngine;
using UnityEngine.UI;

namespace MaverickFresh
{
    /// <summary>
    /// Small safety wrapper that keeps the targeting pod display state explicit.
    /// It does not replace MavTargetingPodSystem controls; it only centralizes startup state.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavTGPStateManager : MonoBehaviour
    {
        [Header("References")]
        public MavTargetingPodSystem targetingPod;
        public CanvasGroup pipCanvasGroup;
        public RawImage pipRawImage;

        [Header("Startup")]
        public bool autoFindTargetingPod = true;
        public bool forceOffOnStart = true;
        public bool startHidden = true;

        [Header("Runtime")]
        public bool isPipVisible;
        public bool isFocusActive;
        public string state = "ready";

        private void Awake()
        {
            Resolve();
            ApplyReferencesToPod();

            if (forceOffOnStart || startHidden)
                SetOff();
        }

        private void Start()
        {
            Resolve();
            ApplyReferencesToPod();

            if (forceOffOnStart)
                SetOff();
        }

        private void LateUpdate()
        {
            Resolve();
            SyncRuntimeState();
        }

        public void SetOff()
        {
            Resolve();
            ApplyReferencesToPod();

            if (targetingPod != null)
            {
                targetingPod.forceOffOnStart = forceOffOnStart;
                targetingPod.startHidden = startHidden;
                targetingPod.SetOff();
            }

            SyncRuntimeState();
            state = "off";
        }

        public void SetPip(bool enabled)
        {
            Resolve();
            ApplyReferencesToPod();

            if (targetingPod != null)
                targetingPod.SetPip(enabled);

            SyncRuntimeState();
            state = enabled ? "pip" : "off";
        }

        public void SetFocus(bool enabled)
        {
            Resolve();
            ApplyReferencesToPod();

            if (targetingPod != null)
                targetingPod.SetFocus(enabled);

            SyncRuntimeState();
            state = enabled ? "focus" : isPipVisible ? "pip" : "off";
        }

        private void Resolve()
        {
            if (targetingPod == null && autoFindTargetingPod)
                targetingPod = GetComponent<MavTargetingPodSystem>();

            if (targetingPod == null && autoFindTargetingPod)
                targetingPod = FindObjectOfType<MavTargetingPodSystem>();

            if (targetingPod != null)
            {
                if (pipCanvasGroup == null)
                    pipCanvasGroup = targetingPod.pipCanvasGroup;

                if (pipRawImage == null)
                    pipRawImage = targetingPod.pipRawImage;
            }
        }

        private void ApplyReferencesToPod()
        {
            if (targetingPod == null)
                return;

            if (pipCanvasGroup != null)
                targetingPod.pipCanvasGroup = pipCanvasGroup;

            if (pipRawImage != null)
                targetingPod.pipRawImage = pipRawImage;
        }

        private void SyncRuntimeState()
        {
            if (targetingPod == null)
            {
                isPipVisible = false;
                isFocusActive = false;
                state = "no_tgp";
                return;
            }

            isPipVisible = targetingPod.displayMode == MavTargetingPodDisplayMode.PictureInPicture;
            isFocusActive = targetingPod.displayMode == MavTargetingPodDisplayMode.Fullscreen;

            if (isFocusActive) state = "focus";
            else if (isPipVisible) state = "pip";
            else state = "off";
        }
    }
}
