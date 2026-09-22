using System;
using UnityEngine;
using UnityEngine.Events;
namespace ChooGuard.App.Fps
{
    // Generic staff action endpoint. Mission rules belong to the scene's gameplay owner.
    public class FpsInteractable : MonoBehaviour,IFpsInteraction
    {
        public string Prompt="조작하기";
        public string SuccessMessage="조작했습니다";
        public string UnavailableMessage="지금은 사용할 수 없습니다";
        public bool InteractionEnabled=true;
        public UnityEvent OnInteracted=new UnityEvent();
        public event Action<FirstPersonResponder> InteractionPerformed;
        public int InteractionCount { get; private set; }
        public string InteractionPrompt=>Prompt;
        public virtual bool CanInteract(FirstPersonResponder responder,out string reason)
        {
            bool available=isActiveAndEnabled&&InteractionEnabled&&responder!=null&&!responder.IsPaused;
            reason=available?null:UnavailableMessage;return available;
        }
        public virtual bool TryInteract(FirstPersonResponder responder,out string feedback)
        {
            if(!CanInteract(responder,out feedback))return false;
            OnInteracted.Invoke();InteractionPerformed?.Invoke(responder);InteractionCount++;feedback=SuccessMessage;return true;
        }
    }
}
