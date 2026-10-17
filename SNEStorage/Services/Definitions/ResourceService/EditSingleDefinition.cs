using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class ResourceService
{
    public Task<ServiceResultContext> ValidateEdit(ResourceEditDTO dto, CancellationToken ct = default)
    {
        if (dto.ExternalAuthors.Any(author => author is null || author.Length > TextLimits.EXTERNAL_AUTHOR))
            return Task.FromResult(ServiceResult.InvalidExternalAuthor.Return(_logger));
        if (dto.Tags.Any(tag => tag is null || tag.Length > TextLimits.TAG))
            return Task.FromResult(ServiceResult.InvalidTag.Return(_logger));

        if (dto.Name != null)
        {
            dto.Name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Task.FromResult(ServiceResult.EmptyName.Return(_logger));
            if (dto.Name.Length < MIN_NAME_SIZE || dto.Name.Length > MAX_NAME_SIZE ||
                !ResourceNameRegex().IsMatch(dto.Name))
                return Task.FromResult(ServiceResult.InvalidName.Return(_logger));
        }
        if (dto.Description != null)
        {
            dto.Description = dto.Description.Trim();
            if (string.IsNullOrWhiteSpace(dto.Description))
                return Task.FromResult(ServiceResult.EmptyDescription.Return(_logger));
            if (dto.Description.Length < MIN_DESCRIPTION_SIZE ||
                dto.Description.Length > MAX_DESCRIPTION_SIZE)
                return Task.FromResult(ServiceResult.InvalidDescription.Return(_logger));
        }
        if (dto.RepositoryURL != null)
        {
            dto.RepositoryURL = dto.RepositoryURL.Trim();
            if (dto.RepositoryURL.Length > 0 && !IsValidRepositoryUrl(dto.RepositoryURL))
                return Task.FromResult(ServiceResult.InvalidRepositoryURL.Return(_logger));
        }
        if (dto.Previews.Any(preview => preview.Length > PREVIEW_MAX_SIZE))
            return Task.FromResult(ServiceResult.PreviewSizeSurpassTheLimit.Return(_logger));

        return Task.FromResult(ServiceResult.Success.Return(_logger));
    }

    public async Task<OneOf<Resource, ServiceResultContext>> FindEdit(
        long resourceId, CancellationToken ct = default)
    {
        var accessResult = await (_accountService.IsModeratorOrAdmin() ?
            _accessService.GetViewAccess(resourceId, ct) :
            _accessService.GetEditAccess(resourceId, ct));
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var query = _context.Resources
            .AsTracking()
            .Where(resource => resource.Id == resourceId)
            .Include(resource => resource.ResourceVersions)
                .ThenInclude(version => version.File)
            .Include(resource => resource.ResourcePreviews)
            .Include(resource => resource.ResourcesAuthors)
            .Include(resource => resource.ResourceExternalAuthors)
            .Include(resource => resource.ResourceDependencies)
            .Include(resource => resource.ResourceExternalDependencies)
            .Include(resource => resource.ResourceResourceTypes)
            .Include(resource => resource.ResourceTags)
            .AsSplitQuery();

        return await _getResource.Execute(query, ServiceResult.ResourceNotFound, ct: ct);
    }

    public async Task<ServiceResultContext> EditInternal(
        Resource resource, ResourceEditDTO dto, CancellationToken ct = default)
    {
        long resourceId = resource.Id;
        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        bool hasSeal = resource.ResourceVersions.Any(version => version.SealOfQuality);
        bool isStaff = _accountService.IsModeratorOrAdmin();
        long visibilityId = dto.VisibilityId ?? resource.VisibilityId;

        if (hasSeal && (!isStaff || visibilityId != 1))
            return ServiceResult.ResourceHasSealOfQuality.Return(_logger);

        if (visibilityId == 3)
        {
            var dependentsResult = await _dbHelperService.TryFunction(
                cancel => _context.ResourceDependencies.AsNoTracking()
                    .AnyAsync(dependency => dependency.DependencyResourceId == resourceId &&
                        dependency.ResourceId != resourceId, cancel), ct: ct);

            if (!dependentsResult.TryPickT0(out bool hasDependents, out var dependentsError))
                return dependentsError;
            if (hasDependents)
                return ServiceResult.ResourceHasDependents.Return(_logger);
        }

        string mainFolder = ResourcePathHelper.GetMainResourceFolder(resource);
        var batchResult = await _fileService.BeginBatch(ct);
        if (!batchResult.TryPickT0(out var batchfile, out var batchError))
            return batchError;

        var prevResult = await GetPreviews(dto.Previews, mainFolder, batchfile, ct);
        if (!prevResult.TryPickT0(out var resourcePreviews, out var previewError))
            return previewError;

        var tagRes = await GetTags(dto.Tags, ct);
        if (!tagRes.TryPickT0(out var tags, out var tagError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return tagError;
        }

        var typesRes = await GetTypes(dto.Types, ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return typeError;
        }

        if (types.Any(type => type == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceTypeNotFound.Return(_logger);
        }

        var authorRes = await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor == true, userId, ct);
        if (!authorRes.TryPickT0(out var authors, out var authorError))
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
        var dependenciesRes = await GetDependencies(dto.DependencyIDs, ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
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

        resource.ResourceResourceTypes = [.. resource.ResourceResourceTypes
            .Where(type => !dto.RemovedTypeIDs.Contains(type.Id))
            .Concat(types)
            .DistinctBy(type => type!.ResourceTypeId)!];
        if (resource.ResourceResourceTypes.Count <= 0)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.EmptyTypeList.Return(_logger);
        }

        resource.ResourcePreviews = [.. resource.ResourcePreviews
            .Where(preview => !dto.RemovePreviewIDs.Contains(preview.Id))
            .Concat(resourcePreviews.Where(preview => preview.FileId != 0))
            .DistinctBy(preview => preview.FileId)
            .Concat(resourcePreviews.Where(preview => preview.FileId == 0))];

        if (resource.ResourcePreviews.Count > MAX_AMOUNT_OF_PREVIEWS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.PreviewAmountSurpassTheLimit.Return(_logger);
        }

        resource.Name = dto.Name?.Trim() ?? resource.Name;
        resource.Description = dto.Description?.Trim() ?? resource.Description;
        resource.UpdatedAt = DateTime.UtcNow;
        resource.AlreadyModerated = false;
        resource.ResourceDependencies = [.. resource.ResourceDependencies
            .Where(dependency => !dto.RemoveDependencyIDs.Contains(dependency.Id))
            .Concat(dependencies)
            .Where(dependency => dependency!.DependencyResourceId != resourceId)
            .DistinctBy(dependency => dependency!.DependencyResourceId)!];
        resource.ResourceExternalDependencies = [.. resource.ResourceExternalDependencies
            .Where(dependency => !dto.RemoveExternalDependencyIDs.Contains(dependency.Id))
            .Concat(externalDependencies.Where(dependency => dependency.Id != 0))
            .DistinctBy(dependency => dependency.Id)
            .Concat(externalDependencies.Where(dependency => dependency.Id == 0))];
        resource.ResourcesAuthors = [.. resource.ResourcesAuthors
            .Where(author => !dto.RemoveAuthorsIDs.Contains(author.Id))
            .Concat(authors)
            .DistinctBy(author => author!.UserId)!];
        resource.ResourceExternalAuthors = [.. resource.ResourceExternalAuthors
            .Where(author => !dto.RemoveExternalAuthorsIDs.Contains(author.Id))
            .Concat(externalAuthors.Where(author => author.Id != 0))
            .DistinctBy(author => author.Id)
            .Concat(externalAuthors.Where(author => author.Id == 0))];
        resource.ResourceTags = [.. resource.ResourceTags
            .Where(tag => !dto.RemoveTagIDs.Contains(tag.Id))
            .Concat(tags.Where(tag => tag.TagId != 0))
            .DistinctBy(tag => tag.TagId)
            .Concat(tags.Where(tag => tag.TagId == 0))];
        resource.RepositoryURL = _dbHelperService.GetEditString(
            dto.RepositoryURL, resource.RepositoryURL);
        resource.VideogameId = dto.VideogameId ?? resource.VideogameId;
        resource.VisibilityId = dto.VisibilityId ?? resource.VisibilityId;
        resource.IncludesPorn = dto.IncludesPorn ?? resource.IncludesPorn;
        resource.IncludesGore = dto.IncludesGore ?? resource.IncludesGore;
        resource.IncludesPolitics = dto.IncludesPolitics ?? resource.IncludesPolitics;
        resource.IncludesSlurs = dto.IncludesSlurs ?? resource.IncludesSlurs;
        resource.IncludesSensitiveContent =
            dto.IncludesSensitiveContent ?? resource.IncludesSensitiveContent;
        long[] previewIds = [.. dto.RemovePreviewIDs
            .Where(id => !resource.ResourcePreviews.Any(preview => preview.Id == id))];

        if (resource.ResourcePreviews.Count > MAX_AMOUNT_OF_PREVIEWS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.PreviewAmountSurpassTheLimit.Return(_logger);
        }

        if (resource.ResourcesAuthors.Count + resource.ResourceExternalAuthors.Count >
                MAX_AMOUNT_OF_ELEMENTS ||
            resource.ResourceDependencies.Count + resource.ResourceExternalDependencies.Count >
                MAX_AMOUNT_OF_ELEMENTS ||
            resource.ResourceTags.Count > MAX_AMOUNT_OF_ELEMENTS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.AmountOfElementsSurpassTheLimit.Return(_logger);
        }

        var removePreviewsQuery = _context.ResourcePreviews.AsNoTracking()
            .Where(preview => preview.ResourceId == resourceId &&
                previewIds.Contains(preview.Id));
        var removeResult = await _getPreviewList.Execute(removePreviewsQuery, ct: ct);
        if (!removeResult.TryPickT0(out var removePreviews, out var removeError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return removeError;
        }

        var filesResult = await _fileService.PrepareDeletion(
            removePreviews.Select(preview => preview.FileId), ct);
        if (!filesResult.TryPickT0(out var files, out var filesError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return filesError;
        }

        var saveResult = await SaveResourceChanges(resource, batchfile, ct);
        if (!saveResult.TryPickT0(out _, out var saveError))
            return saveError;

        _ = _fileService.FinishDeletion(files);
        return ServiceResult.Success.Return(_logger);
    }
}
