using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace UI
{
    public class SlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Image _highlightBorder;
        [SerializeField] private Image _background;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 1f, 0.5f);

        private Inventory.ItemSlot _slot;
        private int _slotIndex;
        private bool _isHotbar;

        public event Action<SlotUI, PointerEventData.InputButton> OnSlotClicked;

        public Inventory.ItemSlot Slot => _slot;
        public int SlotIndex => _slotIndex;
        public bool IsHotbar => _isHotbar;

        public void Initialize(Inventory.ItemSlot slot, int index, bool isHotbar)
        {
            _slot = slot;
            _slotIndex = index;
            _isHotbar = isHotbar;
            Refresh();
        }

        public void Refresh()
        {
            if (_slot == null || _slot.IsEmpty)
            {
                _iconImage.enabled = false;
                _countText.text = "";
            }
            else
            {
                _iconImage.enabled = true;
                _iconImage.sprite = _slot.Item.icon;
                _countText.text = _slot.Count > 1 ? _slot.Count.ToString() : "";
            }
        }

        public void SetHighlight(bool active)
        {
            if (_highlightBorder != null)
                _highlightBorder.enabled = active;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnSlotClicked?.Invoke(this, eventData.button);
        }
    }
}
