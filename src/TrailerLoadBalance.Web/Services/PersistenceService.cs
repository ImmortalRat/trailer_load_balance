using System.Text.Json;
using Microsoft.JSInterop;
using TrailerLoadBalance.Web.Models;

namespace TrailerLoadBalance.Web.Services;

/// <summary>
/// Thin C# wrapper over the browser's localStorage (via JS interop) for saving/restoring the
/// user's profile selection and cargo layout, and for reacting to changes made in other tabs.
/// </summary>
public sealed class PersistenceService(IJSRuntime js)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(PersistedState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await js.InvokeVoidAsync("tlb.saveState", json);
    }

    public async Task<PersistedState?> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("tlb.loadState");
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<IDisposable> RegisterCrossTabSyncAsync(Func<Task> onExternalChange)
    {
        var listener = new StorageChangeListener(onExternalChange);
        var dotNetRef = DotNetObjectReference.Create(listener);
        await js.InvokeVoidAsync("tlb.registerSync", dotNetRef);
        listener.SelfRef = dotNetRef;
        return listener;
    }

    private sealed class StorageChangeListener(Func<Task> callback) : IDisposable
    {
        public DotNetObjectReference<StorageChangeListener>? SelfRef;

        [JSInvokable]
        public Task OnExternalStateChanged() => callback();

        public void Dispose() => SelfRef?.Dispose();
    }
}
