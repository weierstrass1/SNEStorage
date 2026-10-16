using System.Text.Json;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

// UI transport only. No service, database or permission logic is moved here.
internal static class BrowserReadStartup
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ValueTask<BrowserModuleRead<T>> LoadAsync<T>(IJSRuntime runtime,
        string module, string method, object?[] args, string key, string? cancelMethod = null) =>
        ReceiveAsync(runtime.InvokeAsync<BrowserModuleRead<T>>(
            "snestorageReads.start", module, method, args, key, cancelMethod));

    public static ValueTask<BrowserModuleRead<T>> ReadAsync<T>(IJSRuntime runtime,
        IJSObjectReference module, string method, object?[] args, string key, string? cancelMethod = null) =>
        ReceiveAsync(runtime.InvokeAsync<BrowserModuleRead<T>>(
            "snestorageReads.read", module, method, args, key, cancelMethod));

    private static async ValueTask<BrowserModuleRead<T>> ReceiveAsync<T>(ValueTask<BrowserModuleRead<T>> request)
    {
        BrowserModuleRead<T> result;
        try { result = await request; }
        catch (OperationCanceledException) { throw new JSException("The initial read timed out. Please retry."); }
        if (result.ResponseStream is null) return result;
        await using var reference = result.ResponseStream;
        try
        {
            if (reference.Length > 8 * 1024 * 1024)
                throw new JSException("The initial response exceeds the browser read limit.");
            await using var stream = await reference.OpenReadStreamAsync(8 * 1024 * 1024);
            result.Response = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
            return result;
        }
        catch (Exception exception)
        {
            if (result.Module is not null) await result.Module.DisposeAsync();
            if (exception is IOException or OperationCanceledException)
                throw new JSException("The initial response could not be received. Please retry.");
            throw;
        }
    }

    public static ValueTask CancelAsync(IJSRuntime runtime, string key, CancellationToken ct) =>
        runtime.InvokeVoidAsync("snestorageReads.cancel", ct, key);
}

internal sealed class BrowserModuleRead<T>
{
    public IJSObjectReference? Module { get; set; }
    public IJSStreamReference? ResponseStream { get; set; }
    public T? Response { get; set; }
    public bool Cancelled { get; set; }
}
