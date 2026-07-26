using UnityEngine;
using DG.Tweening;

public class GameObjectVisibilityHandler : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _autoHideDelay = 2f;
    [SerializeField] private bool _playPunchScaleOnShow = true;
    [SerializeField] private Vector3 _punchScale = new Vector3(0.15f, 0.15f, 0.15f);
    [SerializeField, Min(0f)] private float _punchDuration = 0.22f;
    [SerializeField, Min(0)] private int _punchVibrato = 6;
    [SerializeField, Range(0f, 1f)] private float _punchElasticity = 0.6f;

    private Coroutine _autoHideRoutine = null;
    private Tween _punchTween = null;
    private Vector3 _defaultLocalScale = Vector3.one;
    private bool _hasCachedDefaultLocalScale = false;

    private void Awake()
    {
        CacheDefaultLocalScale();
    }

    private void OnDisable()
    {
        StopPunchScale();
        StopAutoHide();
    }

    public void Show()
    {
        CacheDefaultLocalScale();
        gameObject.SetActive(true);
        PlayPunchScale();
        StartAutoHide();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        StopAutoHide();
        StopPunchScale();
        gameObject.SetActive(false);
    }

    private void PlayPunchScale()
    {
        if (!_playPunchScaleOnShow || _punchDuration <= 0f)
        {
            transform.localScale = _defaultLocalScale;
            return;
        }

        StopPunchScale();
        transform.localScale = _defaultLocalScale;
        _punchTween = transform
            .DOPunchScale(_punchScale, _punchDuration, _punchVibrato, _punchElasticity)
            .SetTarget(this)
            .OnComplete(() =>
            {
                _punchTween = null;
                transform.localScale = _defaultLocalScale;
            });
    }

    private void StopPunchScale()
    {
        if (_punchTween == null)
        {
            return;
        }

        _punchTween.Kill();
        _punchTween = null;
        transform.localScale = _defaultLocalScale;
    }

    private void CacheDefaultLocalScale()
    {
        if (_hasCachedDefaultLocalScale)
        {
            return;
        }

        _defaultLocalScale = transform.localScale;
        _hasCachedDefaultLocalScale = true;
    }

    private void StartAutoHide()
    {
        StopAutoHide();
        if (_autoHideDelay <= 0f)
        {
            return;
        }

        _autoHideRoutine = StartCoroutine(AutoHideRoutine());
    }

    private void StopAutoHide()
    {
        if (_autoHideRoutine == null)
        {
            return;
        }

        StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = null;
    }

    private System.Collections.IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(_autoHideDelay);
        _autoHideRoutine = null;
        Hide();
    }
}
