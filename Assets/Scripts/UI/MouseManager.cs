using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public class MouseManager : MonoBehaviour
    {
        public static MouseManager Instance { get; private set; }

        private HashSet<object> _unlockRequesters = new();

        public bool IsCursorFree => _unlockRequesters.Count > 0;
        public event Action<bool> OnCursorStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            UpdateCursorState();
        }

        public void RequestUnlock(object requester)
        {
            if (_unlockRequesters.Add(requester))
            {
                UpdateCursorState();
            }
        }

        public void ReleaseLock(object requester)
        {
            if (_unlockRequesters.Remove(requester))
            {
                UpdateCursorState();
            }
        }

        private void UpdateCursorState()
        {
            bool isFree = IsCursorFree;
            Cursor.lockState = isFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isFree;
            OnCursorStateChanged?.Invoke(isFree);
        }
    }
}
