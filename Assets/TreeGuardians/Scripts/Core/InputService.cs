using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TreeGuardians.Core
{
    /// Unified mouse/touch pointer layer built on the Input System. Editor mouse and single-finger touch behave identically.
    public sealed class InputService : MonoBehaviour
    {
        public Vector2 PointerScreenPosition { get; private set; }
        public bool PointerDownThisFrame { get; private set; }
        public bool PointerUpThisFrame { get; private set; }
        public bool PointerHeld { get; private set; }
        public bool PointerOverUI { get; private set; }
        public bool HasPointer { get; private set; }

        readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        PointerEventData eventData;
        EventSystem cachedEventSystem;

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                HasPointer = false;
                PointerDownThisFrame = false;
                PointerUpThisFrame = false;
                PointerHeld = false;
                PointerOverUI = false;
                return;
            }

            HasPointer = true;
            PointerScreenPosition = pointer.position.ReadValue();
            PointerDownThisFrame = pointer.press.wasPressedThisFrame;
            PointerUpThisFrame = pointer.press.wasReleasedThisFrame;
            PointerHeld = pointer.press.isPressed;
            PointerOverUI = ComputePointerOverUI(PointerScreenPosition);
        }

        bool ComputePointerOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (eventData == null || cachedEventSystem != es)
            {
                cachedEventSystem = es;
                eventData = new PointerEventData(es);
            }
            eventData.position = screenPos;
            uiHits.Clear();
            es.RaycastAll(eventData, uiHits);
            return uiHits.Count > 0;
        }

        public Vector3 PointerWorldPosition(Camera cam, float z = 0f)
        {
            if (cam == null) return Vector3.zero;
            var p = new Vector3(PointerScreenPosition.x, PointerScreenPosition.y, Mathf.Abs(cam.transform.position.z - z));
            var w = cam.ScreenToWorldPoint(p);
            w.z = z;
            return w;
        }
    }
}
