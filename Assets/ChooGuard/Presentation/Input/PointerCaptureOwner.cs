using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.Presentation.Input
{
    public enum InputContext { None, Modal, Text, Hud, World, Camera }

    public readonly struct InputOwner
    {
        public InputContext Context { get; }
        public int PointerId { get; }
        public GameObject Focus { get; }
        public GameObject Modal { get; }

        public InputOwner(InputContext context, int pointerId, GameObject focus, GameObject modal)
        {
            Context = context;
            PointerId = pointerId;
            Focus = focus;
            Modal = modal;
        }
    }

    // pointerId는 버튼별 스트림 식별자다. down 없는 up은 새로운 입력으로 해석하지 않는다.
    public sealed class PointerCaptureOwner
    {
        private readonly Dictionary<int, InputOwner> owners = new Dictionary<int, InputOwner>();
        public int Count => owners.Count;
        public bool HasUiCapture
        {
            get
            {
                foreach (var owner in owners.Values)
                    if (owner.Context == InputContext.Modal || owner.Context == InputContext.Text || owner.Context == InputContext.Hud) return true;
                return false;
            }
        }

        public bool TryBegin(InputOwner owner)
        {
            if (owner.Context == InputContext.None || owners.ContainsKey(owner.PointerId)) return false;
            owners.Add(owner.PointerId, owner);
            return true;
        }

        public bool TryGet(int pointerId, out InputOwner owner) => owners.TryGetValue(pointerId, out owner);

        public bool TryEnd(int pointerId, out InputOwner owner)
        {
            if (!owners.TryGetValue(pointerId, out owner)) return false;
            owners.Remove(pointerId);
            return true;
        }

        public bool Cancel(int pointerId) => owners.Remove(pointerId);
        public void CancelAll() => owners.Clear();
    }
}
