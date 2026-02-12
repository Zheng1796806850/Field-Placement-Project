using System;
using UnityEngine;

public class TimedActionRequest
{
    public string label;
    public float duration;
    public bool requireHold;
    public KeyCode holdKey;
    public KeyCode cancelKey;
    public bool lockPlayerMovement;
    public Transform target;
    public float maxDistance;
    public bool cancelIfPhaseNotDay;
    public Action onBegin;
    public Action<float> onProgress;
    public Action onComplete;
    public Action onCancel;

}