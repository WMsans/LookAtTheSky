using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public class CursorItem : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Canvas _parentCanvas;

        private Inventory.BuildingItemSO _heldItem;
        private int _heldCount;
        private RectTransform _rectTransform;

        public Inventory.BuildingItemSO HeldItem => _heldItem;
        public int HeldCount => _heldCount;
        public bool IsHolding => _heldItem != null && _heldCount > 0;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Hide();
        }

        private void Update()
        {
            if (!IsHolding) return;
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _rectTransform.position = mousePos;
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentCanvas.transform as RectTransform,
                    mousePos,
                    _parentCanvas.worldCamera,
                    out Vector2 localPoint);
                _rectTransform.localPosition = localPoint;
            }
        }

        public void Show(Inventory.BuildingItemSO item, int count)
        {
            _heldItem = item;
            _heldCount = count;

            _iconImage.enabled = true;
            _iconImage.sprite = item.icon;
            _countText.text = count > 1 ? count.ToString() : "";
            gameObject.SetActive(true);
        }

        public void UpdateCount(int count)
        {
            _heldCount = count;
            _countText.text = count > 1 ? count.ToString() : "";
            if (count <= 0) Hide();
        }

        public void Hide()
        {
            _heldItem = null;
            _heldCount = 0;
            _iconImage.enabled = false;
            _countText.text = "";
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Take the held item data and clear it. Returns (item, count).
        /// </summary>
        public (Inventory.BuildingItemSO item, int count) Take()
        {
            var result = (_heldItem, _heldCount);
            Hide();
            return result;
        }
    }
}
