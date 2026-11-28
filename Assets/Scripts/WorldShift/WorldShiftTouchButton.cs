using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Outlaws.WorldShift
{
    public sealed class WorldShiftTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private readonly HashSet<int> pointers = new HashSet<int>();
        public bool Held => isActiveAndEnabled && pointers.Count > 0;
        public void OnPointerDown(PointerEventData data) => pointers.Add(data.pointerId);
        public void OnPointerUp(PointerEventData data) => pointers.Remove(data.pointerId);
        public void OnPointerExit(PointerEventData data) => pointers.Remove(data.pointerId);
        public void Clear() => pointers.Clear();
        private void OnDisable() => Clear();
        private void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
    }
}
