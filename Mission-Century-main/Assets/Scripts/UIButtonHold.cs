// UIButtonHold.cs — fires continuously while held (joystick feel)
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class UIButtonHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public UnityEvent onPress   = new UnityEvent();
    public UnityEvent onHold    = new UnityEvent();
    public UnityEvent onRelease = new UnityEvent();

    bool held = false;

    public void OnPointerDown(PointerEventData e)
    {
        held = true;
        onPress.Invoke();
        onHold.Invoke();
    }

    public void OnPointerUp(PointerEventData e)
    {
        held = false;
        onRelease.Invoke();
    }

    void Update()
    {
        if (held) onHold.Invoke();
    }
}
