using System.Collections;

// Exercise the production coroutine without Unity native libraries or live HTTP.
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public IEnumerator? Pending;
        protected void StartCoroutine(IEnumerator routine) => Pending = routine;
        public void Finish() { int ticks = 0; while (Pending!.MoveNext()) if (++ticks > 100) throw new Exception("Coroutine did not finish"); }
    }
    public static class Time { public static float realtimeSinceStartup; }
}
namespace UnityEngine.Networking
{
    public sealed class UnityWebRequestAsyncOperation { public bool isDone = true; }
    public sealed class DownloadHandler { public string text = ""; }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ConnectionError }
        public static readonly List<UnityWebRequest> Sent = new();
        public static UnityWebRequest Next = new();
        public string Url = "";
        public bool Disposed, ThrowOnSend;
        public Result result;
        public long responseCode = 200;
        public string? error;
        public ulong downloadedBytes;
        public int timeout, redirectLimit;
        public readonly DownloadHandler downloadHandler = new();
        public readonly UnityWebRequestAsyncOperation Operation = new();
        public readonly Dictionary<string,string> Headers = new();
        public static UnityWebRequest Get(string url) { Next.Url=url; Sent.Add(Next); return Next; }
        public UnityWebRequestAsyncOperation SendWebRequest() { if (ThrowOnSend) throw new Exception("offline"); return Operation; }
        public void SetRequestHeader(string key,string value) => Headers[key]=value;
        public void Abort() { result=Result.ConnectionError; error="aborted"; Operation.isDone=true; }
        public void Dispose() => Disposed=true;
    }
}
namespace Silverpine.ModdingTools
{
    public sealed class InstalledModInfo
    {
        public string Id="", Name="", Kind="";
        public Version Version=new(1,0);
    }
    public sealed class ModInventorySnapshot
    {
        public List<InstalledModInfo> Mods=new();
        public List<string> Warnings=new();
    }
    public static class ModInventory
    {
        public static ModInventorySnapshot Capture() => new();
    }
}
