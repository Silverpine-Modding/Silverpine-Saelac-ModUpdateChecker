using System;
using System.Collections;
using UnityEngine;

namespace Silverpine.ModUpdateChecker;

/// <summary>Disposable Unity host, recreated after Silverpine cleans up runtime objects.</summary>
internal sealed class UpdateRunner : MonoBehaviour
{
    private static UpdateRunner? instance;
    private Action? interrupted;

    internal static void Run(IEnumerator routine, Action onInterrupted)
    {
        // Unity's overloaded null check detects a destroyed native object even when
        // its managed wrapper is still reachable. Do not replace with ?? or ??=.
        if (instance == null || !instance.isActiveAndEnabled)
        {
            var root = new GameObject("SilverpineModUpdateChecker");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<UpdateRunner>();
        }
        instance.interrupted = onInterrupted;
        instance.StartCoroutine(routine);
    }

    private void OnDisable() => Interrupt();
    private void OnDestroy()
    {
        Interrupt();
        if (ReferenceEquals(instance, this)) instance = null;
    }
    private void Interrupt()
    {
        var callback = interrupted;
        interrupted = null;
        // Unity does not guarantee disposal of a stopped coroutine's enumerator.
        // Explicitly abort/dispose its HTTP request and release the service's busy state.
        try { callback?.Invoke(); }
        finally { StopAllCoroutines(); }
    }
}
