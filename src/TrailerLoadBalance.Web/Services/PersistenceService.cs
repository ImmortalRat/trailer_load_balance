using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using TrailerLoadBalance.Web.Models;

namespace TrailerLoadBalance.Web.Services;

/// <summary>
/// Thin C# wrapper over the browser's localStorage (via JS interop) for saving/restoring the
/// user's profile selection and cargo layout, and for reacting to changes made in other tabs.
///
/// Persistence is a nice-to-have, not core functionality - every call here is wrapped so a JS
/// interop failure (blocked storage in a private/incognito window, a disconnected circuit, a
/// browser privacy setting, etc.) degrades to "this session isn't persisted" instead of
/// propagating an unhandled exception, which for Blazor Server would tear down the whole
/// circuit and leave the page permanently unresponsive to clicks/drags.
/// </summary>
public sealed class PersistenceService(IJSRuntime js, ILogger<PersistenceService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(PersistedState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await js.InvokeVoidAsync("tlb.saveState", json);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not save trailer state to browser storage");
        }
    }

    public async Task<PersistedState?> LoadAsync()
    {
        string? json;
        try
        {
            json = await js.InvokeAsync<string?>("tlb.loadState");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not load trailer state from browser storage");
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse saved trailer state - ignoring it");
            return null;
        }
    }

    public async Task<IDisposable> RegisterCrossTabSyncAsync(Func<Task> onExternalChange)
    {
        var listener = new StorageChangeListener(onExternalChange);
        var dotNetRef = DotNetObjectReference.Create(listener);

        try
        {
            await js.InvokeVoidAsync("tlb.registerSync", dotNetRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not register cross-tab sync listener");
        }

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
