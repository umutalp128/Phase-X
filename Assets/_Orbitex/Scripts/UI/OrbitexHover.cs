using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pointer hover callbacks for code-built UI.</summary>
public class OrbitexHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action Enter;
    public Action Exit;
    public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
}
