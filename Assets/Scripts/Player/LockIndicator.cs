using UnityEngine;
using TMPro;
using DG.Tweening;

public class LockIndicator : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform _ring;
    [SerializeField] private TMP_Text _distanceText;
    [SerializeField] private TMP_Text _velocityText;
    [SerializeField] private TMP_Text _nameText;

    [Header("Settings")]
    [SerializeField] private float _padding = 1.2f;
    [SerializeField] private Color _lockedColor = Color.cyan;
    [SerializeField] private Color _candidateColor = new Color(1f, 1f, 1f, 0.5f);

    private CanvasGroup _canvasGroup;
    private Collider _target;
    private Camera _camera;
    private bool _isLocked;
    private Sequence _pulseSequence;
    private Vector3 _lastPosition;

    public Collider Target => _target;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(Camera camera)
    {
        _camera = camera;
        HideImmediate();
    }

    public void Show(Collider target, bool isLocked)
    {
        _target = target;
        _isLocked = isLocked;
        _lastPosition = target.transform.position + Vector3.one;
        
        UpdateVisuals();
        AnimateIn();
    }

    public void Hide()
    {
        _target = null;
        AnimateOut();
    }

    public void HideImmediate()
    {
        _target = null;
        _canvasGroup.alpha = 0f;
        transform.localScale = Vector3.zero;
        _pulseSequence?.Kill();
    }

    private void Update()
    {
        if (_target == null) return;

        UpdatePosition();
        UpdateSize();
        
        if (_isLocked)
        {
            UpdateTextInfo();
        }
    }

    private void UpdatePosition()
    {
        Vector3 screenPos = _camera.WorldToScreenPoint(_target.bounds.center);

        if (screenPos.z < 0)
        {
            _canvasGroup.alpha = 0f;
            return;
        }

        _canvasGroup.alpha = _isLocked ? 1f : 0.5f;
        transform.position = screenPos;
    }

    private void UpdateSize()
    {
        Bounds bounds = _target.bounds;
        Vector3 min = _camera.WorldToScreenPoint(bounds.min);
        Vector3 max = _camera.WorldToScreenPoint(bounds.max);
        
        float size = Mathf.Max(max.x - min.x, max.y - min.y) * _padding;
        size = Mathf.Max(size, 50f);
        
        _ring.sizeDelta = new Vector2(size, size);
    }

    private void UpdateTextInfo()
    {
        if (_distanceText != null)
        {
            float distance = Vector3.Distance(_camera.transform.position, _target.transform.position);
            _distanceText.text = $"{distance:F1}m";
        }

        if (_velocityText != null)
        {
            Vector3 toTarget = _target.transform.position - _camera.transform.position;
            float currentDistance = toTarget.magnitude;
            
            if (_lastPosition.sqrMagnitude > 0.001f)
            {
                Vector3 lastToTarget = _lastPosition - _camera.transform.position;
                float lastDistance = lastToTarget.magnitude;
                float relativeSpeed = (lastDistance - currentDistance) / Time.deltaTime;
                
                string arrow = relativeSpeed < -0.1f ? "↓ " : (relativeSpeed > 0.1f ? "↑ " : "");
                _velocityText.text = Mathf.Abs(relativeSpeed) > 0.1f 
                    ? $"{arrow}{Mathf.Abs(relativeSpeed):F1} m/s" 
                    : "";
            }
            
            _lastPosition = _target.transform.position;
        }

        if (_nameText != null)
        {
            _nameText.text = _target.name;
        }
    }

    private void UpdateVisuals()
    {
        Color color = _isLocked ? _lockedColor : _candidateColor;
        
        if (_ring != null)
        {
            var ringImage = _ring.GetComponent<UnityEngine.UI.Image>();
            if (ringImage != null)
                ringImage.color = color;
        }

        bool showText = _isLocked;
        if (_distanceText != null) _distanceText.gameObject.SetActive(showText);
        if (_velocityText != null) _velocityText.gameObject.SetActive(showText);
        if (_nameText != null) _nameText.gameObject.SetActive(showText);
    }

    private void AnimateIn()
    {
        _pulseSequence?.Kill();
        
        transform.localScale = Vector3.zero;
        _canvasGroup.alpha = _isLocked ? 1f : 0.5f;

        transform.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad)
            .OnComplete(() => transform.DOScale(1f, 0.15f).SetEase(Ease.InOutQuad));

        StartPulse();
    }

    private void AnimateOut()
    {
        _pulseSequence?.Kill();
        
        transform.DOScale(0f, 0.2f).SetEase(Ease.InBack);
        _canvasGroup.DOFade(0f, 0.2f);
    }

    private void StartPulse()
    {
        _pulseSequence?.Kill();
        
        float targetAlpha = _isLocked ? 0.7f : 0.3f;
        float originalAlpha = _isLocked ? 1f : 0.5f;
        
        _pulseSequence = DOTween.Sequence();
        _pulseSequence.Append(_canvasGroup.DOFade(targetAlpha, 0.75f));
        _pulseSequence.Append(_canvasGroup.DOFade(originalAlpha, 0.75f));
        _pulseSequence.SetLoops(-1);
    }
}
