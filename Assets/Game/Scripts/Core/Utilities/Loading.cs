using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using UnityEngine.Serialization;

namespace NexZap.Utilities
{
    public class Loading : MonoBehaviour
    {
        public enum SceneOption
        {
            Loading,
            Gameplay,
            MainHome
        }

        [Header("UI")]
        [FormerlySerializedAs("progressBar")]
        public Image progressFill;
        public TMP_Text percentText;
        public TMP_Text loadingText;
        public TMP_Text versionText;
        public RectTransform logoRoot;
        public RectTransform progressHeadIcon;
        public Graphic progressFrame;

        [Header("Config")]
        public SceneOption targetScene = SceneOption.Gameplay;
        public float fakeLoadSpeed = 0.9f;
        public float progressTweenDuration = 0.28f;
        public float progressHeadPadding = 0f;
        public int signalStepCount = 5;
        public float progressStepSize = 0.045f;
        public float progressStepPause = 0.08f;
        public float firstNetworkPauseAt = 0.4f;
        public float secondNetworkPauseAt = 0.7f;
        public float firstNetworkPauseDuration = 0.55f;
        public float secondNetworkPauseDuration = 0.7f;
        public float logoDropDistance = 520f;
        public float logoDropDuration = 0.65f;
        public float logoDropOvershoot = 18f;

        private float currentProgress = 0f;
        private Sequence loadingTextSequence;
        private Tween progressFillTween;
        private Tween logoPulseTween;
        private Tween logoFloatTween;
        private Tween framePulseTween;
        private float nextProgressStepTime;
        private float networkPauseUntil;
        private bool firstPauseCompleted;
        private bool secondPauseCompleted;
        private Vector2 logoBaseAnchoredPosition;
        private bool hasLogoBaseAnchoredPosition;
        private Vector2 progressHeadIconBaseAnchoredPosition;
        private bool hasProgressHeadIconBaseAnchoredPosition;

        void Start()
        {
            CacheReferences();
            NormalizeProgressVisuals();
            ApplyVersionText();
            UpdateUI(0f);
            PlayLogoDropAndSignalVisuals();
            StartCoroutine(LoadSceneAsync());
            AnimateLoadingText();
        }

        private void OnDisable()
        {
            loadingTextSequence?.Kill();
            progressFillTween?.Kill();
            logoPulseTween?.Kill();
            logoFloatTween?.Kill();
            framePulseTween?.Kill();
            progressFill?.DOKill();
            progressHeadIcon?.DOKill();
            logoRoot?.DOKill();
            progressFrame?.DOKill();
        }

        IEnumerator LoadSceneAsync()
        {
            string targetSceneName = ResolveSceneName(targetScene);
            if (SceneManager.GetActiveScene().name == targetSceneName)
            {
                UpdateUI(1f);
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);
            operation.allowSceneActivation = false;

            float targetProgress = 0f;
            currentProgress = 0f;
            nextProgressStepTime = 0f;
            networkPauseUntil = 0f;
            firstPauseCompleted = false;
            secondPauseCompleted = false;

            while (!operation.isDone)
            {
                targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
                currentProgress = MoveProgressInSteps(currentProgress, targetProgress);

                UpdateUI(currentProgress);

                if (currentProgress >= 1f)
                {
                    yield return new WaitForSeconds(0.5f);
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }
        }

        private static string ResolveSceneName(SceneOption sceneOption)
        {
            switch (sceneOption)
            {
                case SceneOption.Loading:
                    return "Loading";
                case SceneOption.MainHome:
                    return "MainHome";
                case SceneOption.Gameplay:
                default:
                    return "Gameplay";
            }
        }

        void UpdateUI(float value)
        {
            float clampedValue = Mathf.Clamp01(value);
            if (progressFill != null)
            {
                progressFillTween?.Kill();
                progressFill.DOKill();
                progressFillTween = progressFill
                    .DOFillAmount(clampedValue, progressTweenDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .OnUpdate(() => UpdateProgressHeadIcon(progressFill.fillAmount));
            }
            else
            {
                UpdateProgressHeadIcon(clampedValue);
            }

            if (percentText != null)
            {
                int percent = Mathf.RoundToInt(clampedValue * 100);
                percentText.text = percent + "%";
            }
        }

        void AnimateLoadingText()
        {
            if (loadingText == null)
                return;

            loadingTextSequence?.Kill();
            loadingTextSequence = DOTween.Sequence().SetUpdate(true);
            loadingTextSequence.AppendCallback(() => loadingText.text = "Loading.");
            loadingTextSequence.AppendInterval(0.28f);
            loadingTextSequence.AppendCallback(() => loadingText.text = "Loading..");
            loadingTextSequence.AppendInterval(0.28f);
            loadingTextSequence.AppendCallback(() => loadingText.text = "Loading...");
            loadingTextSequence.AppendInterval(0.34f);

            loadingTextSequence.SetLoops(-1);
        }

        private void CacheReferences()
        {
            if (progressFill == null)
            {
                progressFill = FindImageByName("Fill")
                    ?? FindImageByName("Progress Fill")
                    ?? FindImageByName("Loading Fill")
                    ?? FindImageByName("Bar Fill");

                if (progressFill == null)
                {
                    Image[] images = GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < images.Length; i++)
                    {
                        Image image = images[i];
                        if (image == null || image.type != Image.Type.Filled)
                            continue;

                        progressFill = image;
                        break;
                    }
                }
            }

            if (percentText == null)
                percentText = FindTextByName("Percent Text") ?? FindTextByName("Text %") ?? FindTextByName("Percent");

            if (loadingText == null)
                loadingText = FindTextByName("Text Loading") ?? FindTextByName("Loading Text");

            if (versionText == null)
                versionText = FindTextByName("Version Text") ?? FindTextByName("Text Version") ?? FindTextByName("Version");

            if (logoRoot == null)
            {
                GameObject logoObject = FindChildByName(transform, "Logo");
                if (logoObject != null)
                    logoRoot = logoObject.transform as RectTransform;
            }

            if (logoRoot != null && !hasLogoBaseAnchoredPosition)
            {
                logoBaseAnchoredPosition = logoRoot.anchoredPosition;
                hasLogoBaseAnchoredPosition = true;
            }

            if (progressHeadIcon == null)
            {
                GameObject iconObject = progressFill != null
                    ? FindChildByName(progressFill.transform, "icon") ?? FindChildByName(progressFill.transform, "Icon")
                    : null;

                if (iconObject == null)
                    iconObject = FindChildByName(transform, "icon") ?? FindChildByName(transform, "Icon");

                if (iconObject != null)
                    progressHeadIcon = iconObject.transform as RectTransform;
            }

            if (progressHeadIcon != null && !hasProgressHeadIconBaseAnchoredPosition)
            {
                progressHeadIconBaseAnchoredPosition = progressHeadIcon.anchoredPosition;
                hasProgressHeadIconBaseAnchoredPosition = true;
            }

            if (progressFrame == null)
            {
                progressFrame = FindGraphicByName("Bg")
                    ?? FindGraphicByName("Background")
                    ?? FindGraphicByName("Frame")
                    ?? progressFill;
            }

            if (progressFrame == progressFill)
                progressFrame = ResolveProgressFrameGraphic();

            if (progressFill != null)
            {
                progressFill.DOKill();
                progressFill.fillAmount = 0f;
            }

            if (progressHeadIcon != null)
            {
                progressHeadIcon.DOKill();
                UpdateProgressHeadIcon(0f);
            }
        }

        private void ApplyVersionText()
        {
            if (versionText != null)
                versionText.text = "v" + Application.version;
        }

        private void PlayLogoDropAndSignalVisuals()
        {
            if (logoRoot != null)
            {
                logoRoot.DOKill();
                logoRoot.localScale = Vector3.one * 0.92f;

                Vector2 landingPosition = hasLogoBaseAnchoredPosition ? logoBaseAnchoredPosition : logoRoot.anchoredPosition;
                logoRoot.anchoredPosition = landingPosition + Vector2.up * Mathf.Max(120f, logoDropDistance);

                CanvasGroup logoCanvasGroup = logoRoot.GetComponent<CanvasGroup>();
                if (logoCanvasGroup == null)
                    logoCanvasGroup = logoRoot.gameObject.AddComponent<CanvasGroup>();

                logoCanvasGroup.alpha = 0f;

                Sequence logoIntroSequence = DOTween.Sequence().SetUpdate(true);
                logoIntroSequence.Append(logoCanvasGroup.DOFade(1f, 0.16f));
                logoIntroSequence.Join(logoRoot.DOAnchorPosY(landingPosition.y, logoDropDuration).SetEase(Ease.InQuad));
                logoIntroSequence.Join(logoRoot.DOScale(1.04f, logoDropDuration * 0.72f).SetEase(Ease.OutQuad));
                logoIntroSequence.Append(logoRoot.DOAnchorPosY(landingPosition.y - logoDropOvershoot, 0.12f).SetEase(Ease.OutQuad));
                logoIntroSequence.Append(logoRoot.DOAnchorPosY(landingPosition.y, 0.18f).SetEase(Ease.OutBack));
                logoIntroSequence.Join(logoRoot.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
                logoIntroSequence.OnComplete(StartLogoIdleLoop);
            }
            else
            {
                StartLogoIdleLoop();
            }

            if (progressFrame != null && progressFrame != progressFill)
            {
                progressFrame.DOKill();
                Color baseColor = progressFrame.color;
                framePulseTween = progressFrame
                    .DOFade(Mathf.Clamp01(baseColor.a * 0.72f), 0.9f)
                    .From(baseColor.a)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        private void StartLogoIdleLoop()
        {
            if (logoRoot == null)
                return;

            logoRoot.DOKill();
            if (hasLogoBaseAnchoredPosition)
                logoRoot.anchoredPosition = logoBaseAnchoredPosition;

            logoRoot.localScale = Vector3.one;

            logoPulseTween = logoRoot
                .DOScale(1.05f, 0.7f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);

            float baseY = hasLogoBaseAnchoredPosition ? logoBaseAnchoredPosition.y : logoRoot.anchoredPosition.y;
            logoFloatTween = logoRoot
                .DOAnchorPosY(baseY + 10f, 1.1f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private float MoveProgressInSteps(float currentValue, float targetValue)
        {
            if (targetValue <= currentValue)
                return currentValue;

            if (Time.unscaledTime < networkPauseUntil)
                return currentValue;

            if (Time.unscaledTime < nextProgressStepTime)
                return currentValue;

            float firstPausePoint = Mathf.Clamp01(firstNetworkPauseAt);
            float secondPausePoint = Mathf.Clamp01(secondNetworkPauseAt);

            if (!firstPauseCompleted && currentValue >= firstPausePoint && targetValue >= firstPausePoint)
            {
                firstPauseCompleted = true;
                networkPauseUntil = Time.unscaledTime + Mathf.Max(0.05f, firstNetworkPauseDuration);
                return firstPausePoint;
            }

            if (!secondPauseCompleted && currentValue >= secondPausePoint && targetValue >= secondPausePoint)
            {
                secondPauseCompleted = true;
                networkPauseUntil = Time.unscaledTime + Mathf.Max(0.05f, secondNetworkPauseDuration);
                return secondPausePoint;
            }

            float stageCap = targetValue;
            if (!firstPauseCompleted)
                stageCap = Mathf.Min(stageCap, firstPausePoint);
            else if (!secondPauseCompleted)
                stageCap = Mathf.Min(stageCap, secondPausePoint);

            float stepSize = Mathf.Clamp(progressStepSize, 0.02f, 0.35f);
            float moveAmount = Mathf.Max(stepSize, Time.unscaledDeltaTime * fakeLoadSpeed);
            float nextValue = Mathf.Min(stageCap, currentValue + moveAmount);

            if (nextValue > currentValue)
                nextProgressStepTime = Time.unscaledTime + Mathf.Max(0.02f, progressStepPause);

            return nextValue;
        }

        private void UpdateProgressHeadIcon(float progress)
        {
            if (progressFill == null || progressHeadIcon == null)
                return;

            RectTransform fillRect = progressFill.rectTransform;
            Rect rect = fillRect.rect;
            float totalWidth = rect.width;
            if (totalWidth <= 0f)
                return;

            float normalizedProgress = Mathf.Clamp01(progress);
            float iconHalfWidth = progressHeadIcon.rect.width * Mathf.Abs(progressHeadIcon.localScale.x) * 0.5f;
            float headX = Mathf.Lerp(rect.xMin, rect.xMax, normalizedProgress);
            float minX = rect.xMin + iconHalfWidth;
            float maxX = rect.xMax - iconHalfWidth;
            float targetX = Mathf.Clamp(headX - progressHeadPadding, minX, maxX);
            float targetY = hasProgressHeadIconBaseAnchoredPosition
                ? progressHeadIconBaseAnchoredPosition.y
                : progressHeadIcon.anchoredPosition.y;

            progressHeadIcon.anchoredPosition = new Vector2(targetX, targetY);
        }

        private void NormalizeProgressVisuals()
        {
            if (progressFill != null)
            {
                progressFill.DOKill();
                progressFill.material = null;
                progressFill.color = Color.white;
                progressFill.canvasRenderer.SetAlpha(1f);
            }

            if (progressHeadIcon != null)
            {
                progressHeadIcon.DOKill();
                progressHeadIcon.anchoredPosition = hasProgressHeadIconBaseAnchoredPosition
                    ? progressHeadIconBaseAnchoredPosition
                    : progressHeadIcon.anchoredPosition;
            }

            if (progressFrame != null && progressFrame != progressFill)
            {
                progressFrame.DOKill();
                progressFrame.color = new Color(progressFrame.color.r, progressFrame.color.g, progressFrame.color.b, 1f);
                progressFrame.canvasRenderer.SetAlpha(1f);
            }
        }

        private Graphic ResolveProgressFrameGraphic()
        {
            if (progressFill == null)
                return FindGraphicByName("Bg") ?? FindGraphicByName("Background") ?? FindGraphicByName("Frame");

            Transform parent = progressFill.transform.parent;
            if (parent != null)
            {
                Graphic siblingBackground = FindGraphicByName(parent, "Bg")
                    ?? FindGraphicByName(parent, "Background")
                    ?? FindGraphicByName(parent, "Frame");
                if (siblingBackground != null && siblingBackground != progressFill)
                    return siblingBackground;

                Graphic parentGraphic = parent.GetComponent<Graphic>();
                if (parentGraphic != null && parentGraphic != progressFill)
                    return parentGraphic;
            }

            return null;
        }

        private Image FindImageByName(string childName)
        {
            GameObject child = FindChildByName(transform, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private Graphic FindGraphicByName(string childName)
        {
            GameObject child = FindChildByName(transform, childName);
            return child != null ? child.GetComponent<Graphic>() : null;
        }

        private Graphic FindGraphicByName(Transform root, string childName)
        {
            GameObject child = FindChildByName(root, childName);
            return child != null ? child.GetComponent<Graphic>() : null;
        }

        private TMP_Text FindTextByName(string childName)
        {
            GameObject child = FindChildByName(transform, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static GameObject FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root.gameObject;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindChildByName(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
