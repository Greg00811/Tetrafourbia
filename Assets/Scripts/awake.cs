using UnityEngine;
using UnityEngine.Events;

public class OnAwakeEvent : MonoBehaviour
{
    public UnityEvent onAwake;

    private void Awake()
    {
        onAwake?.Invoke();
    }
}