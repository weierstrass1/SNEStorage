using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Globalization;

namespace SNEStorage.Services;

public partial class ResourceService
{
    public Task<ServiceResultContext> ValidateCreate(
        ResourceCreateDTO dto, CancellationToken ct = default)
    {
        if (dto.ExternalAuthors.Any(author => author is null || author.Length > TextLimits.EXTERNAL_AUTHOR))
            return Task.FromResult(ServiceResult.InvalidExternalAuthor.Return(_logger));
        if (dto.Tags.Any(tag => tag is null || tag.Length > TextLimits.TAG))
            return Task.FromResult(ServiceResult.InvalidTag.Return(_logger));

        dto.Name = dto.Name.Trim();
        dto.Description = dto.Description.Trim();
        dto.RepositoryURL = dto.RepositoryURL?.Trim();
        dto.ChangeLog = dto.ChangeLog.Trim();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Task.FromResult(ServiceResult.EmptyName.Return(_logger));
        if (dto.Name.Length < MIN_NAME_SIZE || dto.Name.Length > MAX_NAME_SIZE ||
            !ResourceNameRegex().IsMatch(dto.Name))
            return Task.FromResult(ServiceResult.InvalidName.Return(_logger));
        if (string.IsNullOrWhiteSpace(dto.Description))
            return Task.FromResult(ServiceResult.EmptyDescription.Return(_logger));
        if (dto.Description.Length < MIN_DESCRIPTION_SIZE ||
            dto.Description.Length > MAX_DESCRIPTION_SIZE)
            return Task.FromResult(ServiceResult.InvalidDescription.Return(_logger));
        if (dto.ChangeLog.Length < MIN_DESCRIPTION_SIZE ||
            dto.ChangeLog.Length > MAX_DESCRIPTION_SIZE)
            return Task.FromResult(ServiceResult.InvalidReleaseNote.Return(_logger));
        if (dto.RepositoryURL != null && !IsValidRepositoryUrl(dto.RepositoryURL))
            return Task.FromResult(ServiceResult.InvalidRepositoryURL.Return(_logger));
        if (dto.File.Length > FILE_MAX_SIZE)
            return Task.FromResult(ServiceResult.FileSizeSurpassTheLimit.Return(_logger));
        if (dto.Previews.Any(preview => preview.Length > PREVIEW_MAX_SIZE))
            return Task.FromResult(ServiceResult.PreviewSizeSurpassTheLimit.Return(_logger));
        if (dto.Previews.Length > MAX_AMOUNT_OF_PREVIEWS)
            return Task.FromResult(ServiceResult.PreviewAmountSurpassTheLimit.Return(_logger));
        if (dto.Types is null || dto.Types.Length == 0)
            return Task.FromResult(ServiceResult.EmptyTypeList.Return(_logger));
        if (dto.AuthorsIDs.Length + dto.ExternalAuthors.Length > MAX_AMOUNT_OF_ELEMENTS ||
            dto.DependencyIDs.Length + dto.ExternalDependencies.Length > MAX_AMOUNT_OF_ELEMENTS ||
            dto.Tags.Length > MAX_AMOUNT_OF_ELEMENTS)
            return Task.FromResult(ServiceResult.AmountOfElementsSurpassTheLimit.Return(_logger));

        return Task.FromResult(ServiceResult.Success.Return(_logger));
    }

    public async Task<OneOf<Resource, ServiceResultContext>> CreateInternal(
        ResourceCreateDTO dto, CancellationToken ct = default)
    {
        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        var versionValidation = await _versionService.ValidateInitialVersion(dto, ct);
        if (versionValidation.Result != ServiceResult.Success)
            return versionValidation;

        string name = ResourcePathHelper.SanitizeFolderName(dto.Name);
        var mainFolder = Path.Combine("Storage", "Resources", $"{Guid.NewGuid():N}_{name}");
        var batchResult = await _fileService.BeginBatch(ct);
        if (!batchResult.TryPickT0(out var batchfile, out var batchError))
            return batchError;

        var versionResult = await _versionService.PrepareInitialVersion(
            dto, userId, mainFolder, batchfile, ct);
        if (!versionResult.TryPickT0(out var resourceVersion, out var versionError))
            return versionError;

        var previewResult = await GetPreviews(dto.Previews, mainFolder, batchfile, ct);
        if (!previewResult.TryPickT0(out var resourcePreviews, out var previewError))
            return previewError;

        var tagResult = await GetTags(dto.Tags, ct);
        if (!tagResult.TryPickT0(out var tags, out var tagError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return tagError;
        }

        var typesResult = await GetTypes(dto.Types, ct);
        if (!typesResult.TryPickT0(out var types, out var typeError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return typeError;
        }

        if (types.Any(type => type == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceTypeNotFound.Return(_logger);
        }

        var authorResult = await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor, userId, ct);
        if (!authorResult.TryPickT0(out var authors, out var authorError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return authorError;
        }

        if (authors.Any(author => author == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);
        var dependenciesResult = await GetDependencies(dto.DependencyIDs, ct);
        if (!dependenciesResult.TryPickT0(out var dependencies, out var dependenciesError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return dependenciesError;
        }

        if (dependencies.Any(dependency => dependency == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceNotFound.Return(_logger);
        }

        List<ResourceExternalDependency> externalDependencies =
            GetExternalDependencies(dto.ExternalDependencies);

        Resource resource = new()
        {
            Id = 0,
            SubmitterId = userId,
            Name = dto.Name,
            Description = dto.Description,
            VideogameId = dto.VideogameId,
            ScoreId = 0,
            Score = new Score(),
            VisibilityId = dto.VisibilityId,
            IncludesPorn = dto.IncludesPorn,
            IncludesGore = dto.IncludesGore,
            IncludesPolitics = dto.IncludesPolitics,
            IncludesSlurs = dto.IncludesSlurs,
            IncludesSensitiveContent = dto.IncludesSensitiveContent,
            CreatedAt = DateTime.UtcNow,
            ResourceVersions = [resourceVersion],
            ResourceTags = tags,
            ResourcesAuthors = authors!,
            ResourceExternalAuthors = externalAuthors,
            ResourceDependencies = dependencies!,
            ResourceExternalDependencies = externalDependencies,
            ResourcePreviews = resourcePreviews,
            ResourceResourceTypes = types!,
            RepositoryURL = dto.RepositoryURL
        };

        _context.Resources.Add(resource);
        var result = await SaveResourceChanges(resource, batchfile, ct);
        if (!result.TryPickT0(out var createdResource, out var saveError))
            return saveError;

        await _discordService.NotifyBotAsync(new DiscordBotEventDTO(Guid.NewGuid(),
            DiscordBotEventTypes.ResourceCreated,
            createdResource.Id.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow), ct);

        return createdResource;
    }
}
