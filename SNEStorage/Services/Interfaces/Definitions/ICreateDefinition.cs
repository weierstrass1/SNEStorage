using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;

public interface ICreateDefinition<TCreate, TModel>
{
    public Task<ServiceResultContext> ValidateCreate(
        TCreate create, CancellationToken ct = default);

    public Task<OneOf<TModel, ServiceResultContext>> CreateInternal(
        TCreate create, CancellationToken ct = default);
}
