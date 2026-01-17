using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.Models;
using MudBlazor;

namespace Boxty.ClientBase.Services
{
    public interface ILazyLookupService<TDto>
where TDto : IDto, ILazyLookup
    {
        string ItemToString(Guid e);
        Task<IEnumerable<Guid>> ItemSearch(string value, CancellationToken token, Guid? tenantConstraint = null, Guid? subjectConstraint = null);
        Task<TDto?> GetItemById(Guid id, CancellationToken token, Guid? tenantConstraint = null, Guid? subjectConstraint = null, bool forceUpdate = false);
        Task<IEnumerable<TDto>> GetItemsByIds(IEnumerable<Guid> ids, CancellationToken token, Guid? tenantConstraint = null, Guid? subjectConstraint = null, bool forceUpdate = false);
        Task<IEnumerable<TDto>> GetAllItems(CancellationToken token, Guid? tenantConstraint = null, Guid? subjectConstraint = null);
        Task<PagedResult<TDto>> GetItemsPaged(int page, int pageSize, CancellationToken token, FetchFilter? filter = null, Guid? tenantConstraint = null, Guid? subjectConstraint = null);
        Task<Guid> AddItem(TDto item, CancellationToken token);
        Task UpdateItem(TDto item, CancellationToken token);
        Task DeleteItem(Guid id, CancellationToken token);
        Task<IEnumerable<TDto>> GetAllByTenantId(Guid tenantId, CancellationToken token);
        Task<IEnumerable<TDto>> GetAllBySubjectId(Guid subjectId, CancellationToken token);
        Task<IEnumerable<TDto>> GetAllByCustom(string customRoute, CancellationToken token);
    }

    public class LazyLookupService<TDto> : ILazyLookupService<TDto>
    where TDto : IDto, ILazyLookup
    {
        private static string EndpointType => typeof(TDto).Name.Replace("Dto", string.Empty).ToLowerInvariant();
        private Dictionary<Guid, Tuple<TDto, DateTime>> _itemList { get; set; } = new();
        private readonly ConcurrentDictionary<string, Task> _ongoingRequests = new();
        private readonly HttpClient _httpClient;
        private readonly ISnackbar _snackbar;
        private readonly IDialogService _dialogService;
        private readonly GlobalStateService _globalStateService;
        public LazyLookupService(IHttpClientFactory httpClientFactory, ISnackbar snackbar, IDialogService dialogService, GlobalStateService globalStateService)
        {
            _httpClient = httpClientFactory.CreateClient("api");
            _snackbar = snackbar;
            _dialogService = dialogService;
            _globalStateService = globalStateService;
        }

        public string ItemToString(Guid e) => e != Guid.Empty && _itemList.TryGetValue(e, out var item) ? $"{item.Item1.DisplayName}" : string.Empty;
        public async Task<IEnumerable<Guid>> ItemSearch(string value, CancellationToken token, Guid? tenantConstraint = null, Guid? subjectConstraint = null)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length == 0)
            {
                return default!;
            }

            var key = $"search_{value}_{tenantConstraint}_{subjectConstraint}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<IEnumerable<Guid>> existingSearchTask)
            {
                return await existingSearchTask;
            }

            var task = PerformItemSearch(value, token, tenantConstraint, subjectConstraint);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<IEnumerable<Guid>> PerformItemSearch(string value, CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"api/{EndpointType}/search?term=" + value);
                if (tenantConstraint.HasValue)
                {
                    sb.Append($"&tenantId={tenantConstraint.Value}");
                }
                if (subjectConstraint.HasValue)
                {
                    sb.Append($"&subjectId={subjectConstraint.Value}");
                }

                var response = await _httpClient.GetAsync(sb.ToString(), token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return Enumerable.Empty<Guid>();

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(cancellationToken: token);
                if (result == null || _itemList == null)
                {
                    return Enumerable.Empty<Guid>();
                }
                foreach (var item in result)
                {
                    _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                }
                return result?.Select(x => x.Id) ?? Enumerable.Empty<Guid>();
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled (likely due to new search request), don't show error
                return Enumerable.Empty<Guid>();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to search items: {ex.Message}", Severity.Error);
                return Enumerable.Empty<Guid>();
            }
        }

        public async Task<TDto?> GetItemById(Guid id, CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint, bool forceUpdate = false)
        {
            if (_itemList.TryGetValue(id, out var item) && !forceUpdate)
            {
                if (item.Item2.AddMinutes(1) > DateTime.UtcNow)
                {
                    return item.Item1;
                }
            }

            var key = $"getbyid_{id}_{tenantConstraint}_{subjectConstraint}_{forceUpdate}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<TDto?> existingGetTask)
            {
                return await existingGetTask;
            }

            var task = PerformGetItemById(id, token, tenantConstraint, subjectConstraint);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<TDto?> PerformGetItemById(Guid id, CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"api/{EndpointType}/getbyid/{id}?1=1");
                if (tenantConstraint.HasValue)
                {
                    sb.Append($"&tenantId={tenantConstraint.Value}");
                }
                if (subjectConstraint.HasValue)
                {
                    sb.Append($"&subjectId={subjectConstraint.Value}");
                }
                var response = await _httpClient.GetAsync(sb.ToString(), token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return default;
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<TDto>(cancellationToken: token);
                if (result != null)
                {
                    _itemList.TryAdd(id, new Tuple<TDto, DateTime>(result, DateTime.UtcNow));
                }
                return result;
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get item by ID: {ex.Message}", Severity.Error);
                return default;
            }
        }

        public async Task<IEnumerable<TDto>> GetItemsByIds(IEnumerable<Guid> ids, CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint, bool forceUpdate = false)
        {
            if (ids == null || !ids.Any())
            {
                return Enumerable.Empty<TDto>();
            }

            var idsArray = ids.ToArray();
            var key = $"getbyids_{string.Join(",", idsArray.OrderBy(x => x))}_{tenantConstraint}_{subjectConstraint}_{forceUpdate}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<IEnumerable<TDto>> existingGetTask)
            {
                return await existingGetTask;
            }

            var task = PerformGetItemsByIds(idsArray, token, tenantConstraint, subjectConstraint, forceUpdate);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<IEnumerable<TDto>> PerformGetItemsByIds(IEnumerable<Guid> ids, CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint, bool forceUpdate)
        {
            var itemsToFetch = ids.Where(id => forceUpdate || !_itemList.ContainsKey(id)).ToList();
            if (!itemsToFetch.Any())
            {
                return ids.Select(id => _itemList[id].Item1).Where(item => item != null);
            }

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"api/{EndpointType}/getbyids?1=1");
                if (tenantConstraint.HasValue)
                {
                    sb.Append($"&tenantId={tenantConstraint.Value}");
                }
                if (subjectConstraint.HasValue)
                {
                    sb.Append($"&subjectId={subjectConstraint.Value}");
                }
                var response = await _httpClient.PostAsJsonAsync(sb.ToString(), itemsToFetch, token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return Enumerable.Empty<TDto>();
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(token);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                        }
                        return items;
                    }
                }
                return Enumerable.Empty<TDto>();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get items by IDs: {ex.Message}", Severity.Error);
                return Enumerable.Empty<TDto>();
            }
        }

        public async Task<IEnumerable<TDto>> GetAllItems(CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint)
        {
            var key = $"getall_{tenantConstraint}_{subjectConstraint}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<IEnumerable<TDto>> existingGetAllTask)
            {
                return await existingGetAllTask;
            }

            var task = PerformGetAllItems(token, tenantConstraint, subjectConstraint);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<IEnumerable<TDto>> PerformGetAllItems(CancellationToken token, Guid? tenantConstraint, Guid? subjectConstraint)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"api/{EndpointType}/getall");

                bool hasParams = false;
                if (tenantConstraint.HasValue && tenantConstraint != Guid.Empty)
                {
                    sb.Append(hasParams ? "&" : "?");
                    sb.Append($"tenantId={tenantConstraint.Value}");
                    hasParams = true;
                }
                if (subjectConstraint.HasValue && subjectConstraint != Guid.Empty)
                {
                    sb.Append(hasParams ? "&" : "?");
                    sb.Append($"subjectId={subjectConstraint.Value}");
                    hasParams = true;
                }
                var response = await _httpClient.GetAsync(sb.ToString(), token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return Enumerable.Empty<TDto>();
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(cancellationToken: token);
                if (result != null)
                {
                    foreach (var item in result)
                    {
                        _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                    }
                    return result;
                }
                return Enumerable.Empty<TDto>();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get all items: {ex.Message}", Severity.Error);
                return Enumerable.Empty<TDto>();
            }
        }

        public async Task<PagedResult<TDto>> GetItemsPaged(int page, int pageSize, CancellationToken token, FetchFilter? filter = null, Guid? tenantConstraint = null, Guid? subjectConstraint = null)
        {
            var key = $"getitems_paged_{page}_{pageSize}_{filter?.GetHashCode()}_{tenantConstraint}_{subjectConstraint}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<PagedResult<TDto>> existingGetItemsPagedTask)
            {
                return await existingGetItemsPagedTask;
            }

            var task = PerformGetItemsPaged(page, pageSize, token, filter, tenantConstraint, subjectConstraint);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<PagedResult<TDto>> PerformGetItemsPaged(int page, int pageSize, CancellationToken token, FetchFilter? filter, Guid? tenantConstraint, Guid? subjectConstraint)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"api/{EndpointType}/paged");

                sb.Append($"?page={page}&pageSize={pageSize}");
                if (tenantConstraint.HasValue && tenantConstraint != Guid.Empty)
                {
                    sb.Append($"&tenantId={tenantConstraint.Value}");
                }
                if (subjectConstraint.HasValue && subjectConstraint != Guid.Empty)
                {
                    sb.Append($"&subjectId={subjectConstraint.Value}");
                }

                // Add filter parameters
                if (filter != null)
                {
                    if (filter.IsActive.HasValue)
                    {
                        sb.Append($"&isActive={filter.IsActive.Value}");
                    }
                    if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                    {
                        sb.Append($"&searchTerm={Uri.EscapeDataString(filter.SearchTerm)}");
                    }
                    if (filter.StartTime.HasValue)
                    {
                        sb.Append($"&startTime={filter.StartTime.Value:yyyy-MM-ddTHH:mm:ss.fffZ}");
                    }
                    if (filter.EndTime.HasValue)
                    {
                        sb.Append($"&endTime={filter.EndTime.Value:yyyy-MM-ddTHH:mm:ss.fffZ}");
                    }
                }

                var response = await _httpClient.GetAsync(sb.ToString(), token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return new PagedResult<TDto> { Items = Enumerable.Empty<TDto>(), TotalCount = 0 };
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<PagedResult<TDto>>(cancellationToken: token);
                if (result?.Items != null)
                {
                    foreach (var item in result.Items)
                    {
                        _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                    }
                    return result;
                }
                return new PagedResult<TDto> { Items = Enumerable.Empty<TDto>(), TotalCount = 0 };
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get all items: {ex.Message}", Severity.Error);
                return new PagedResult<TDto> { Items = Enumerable.Empty<TDto>(), TotalCount = 0 };
            }
        }

        public async Task<Guid> AddItem(TDto item, CancellationToken token)
        {
            try
            {
                _globalStateService.StartLoading();
                var response = await _httpClient.PostAsJsonAsync($"api/{EndpointType}/create", item, token);
                if (response.IsSuccessStatusCode)
                {
                    var createdItemId = await response.Content.ReadFromJsonAsync<Guid>(token);
                    if (createdItemId != Guid.Empty)
                    {
                        item.Id = createdItemId;
                        _itemList[createdItemId] = new Tuple<TDto, DateTime>(item, DateTime.UtcNow);
                    }
                    _snackbar.Add($"Successfully added {EndpointType}", Severity.Success);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // Handle validation errors
                    var errorContent = await response.Content.ReadAsStringAsync(token);
                    var validationErrorMessage = ExtractValidationErrorMessage(errorContent);
                    _snackbar.Add(validationErrorMessage, Severity.Error);
                    throw new HttpRequestException(validationErrorMessage);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(token);
                    var errorMessage = $"Failed to add {EndpointType}: {response.StatusCode}";

                    if (!string.IsNullOrEmpty(errorContent))
                    {
                        errorMessage = $"Failed to add {EndpointType}: {errorContent}";
                    }

                    _snackbar.Add(errorMessage, Severity.Error);
                    throw new HttpRequestException(errorMessage);
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to add {EndpointType}: {ex.Message}";
                _snackbar.Add(errorMessage, Severity.Error);
                throw;
            }
            finally
            {
                _globalStateService.StopLoading();
            }
            return item.Id;
        }

        public async Task UpdateItem(TDto item, CancellationToken token)
        {
            try
            {
                _globalStateService.StartLoading();
                var response = await _httpClient.PutAsJsonAsync($"api/{EndpointType}/update", item, token);
                if (response.IsSuccessStatusCode)
                {
                    var updatedItemId = await response.Content.ReadFromJsonAsync<Guid>(token);
                    if (updatedItemId != Guid.Empty)
                    {
                        _itemList[updatedItemId] = new Tuple<TDto, DateTime>(item, DateTime.UtcNow);
                    }
                    _snackbar.Add($"Successfully updated {EndpointType}", Severity.Success);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // Handle validation errors
                    var errorContent = await response.Content.ReadAsStringAsync(token);
                    var validationErrorMessage = ExtractValidationErrorMessage(errorContent);
                    _snackbar.Add(validationErrorMessage, Severity.Error);
                    throw new HttpRequestException(validationErrorMessage);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(token);
                    var errorMessage = $"Failed to update {EndpointType}: {response.StatusCode}";
                    if (!string.IsNullOrEmpty(errorContent))
                    {
                        errorMessage = $"Failed to update {EndpointType}: {errorContent}";
                    }
                    _snackbar.Add(errorMessage, Severity.Error);
                    throw new HttpRequestException(errorMessage);
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to update {EndpointType}: {ex.Message}", Severity.Error);
                throw;
            }
            finally
            {
                _globalStateService.StopLoading();
            }
        }

        public async Task DeleteItem(Guid id, CancellationToken token)
        {
            var result = await _dialogService.ShowMessageBox(
                $"Delete {EndpointType}",
                $"Are you sure you want to delete {EndpointType}?",
                yesText: "Yes", cancelText: "Cancel");

            if (result != true)
            {
                return;
            }

            try
            {
                _globalStateService.StartLoading();
                var response = await _httpClient.DeleteAsync($"api/{EndpointType}/delete/{id}", token);
                if (response.IsSuccessStatusCode)
                {
                    _itemList.Remove(id);
                }
                _snackbar.Add($"Successfully deleted {EndpointType}", Severity.Success);
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to delete {EndpointType}: {ex.Message}", Severity.Error);
            }
            finally
            {
                _globalStateService.StopLoading();
            }
        }

        public async Task<IEnumerable<TDto>> GetAllByTenantId(Guid tenantId, CancellationToken token)
        {
            var key = $"getallbytenant_{tenantId}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<IEnumerable<TDto>> existingTenantTask)
            {
                return await existingTenantTask;
            }

            var task = PerformGetAllByTenantId(tenantId, token);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<IEnumerable<TDto>> PerformGetAllByTenantId(Guid tenantId, CancellationToken token)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/{EndpointType}/getall?tenantId={tenantId}", token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return Enumerable.Empty<TDto>();

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(cancellationToken: token);
                if (result != null)
                {
                    foreach (var item in result)
                    {
                        _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                    }
                    return result;
                }
                return Enumerable.Empty<TDto>();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get items by tenantId: {ex.Message}", Severity.Error);
                return Enumerable.Empty<TDto>();
            }
        }

        public async Task<IEnumerable<TDto>> GetAllBySubjectId(Guid subjectId, CancellationToken token)
        {
            var key = $"getallbysubject_{subjectId}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<IEnumerable<TDto>> existingSubjectTask)
            {
                return await existingSubjectTask;
            }

            var task = PerformGetAllBySubjectId(subjectId, token);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<IEnumerable<TDto>> PerformGetAllBySubjectId(Guid subjectId, CancellationToken token)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/{EndpointType}/getall?subjectId={subjectId}", token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return Enumerable.Empty<TDto>();

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(cancellationToken: token);
                if (result != null)
                {
                    foreach (var item in result)
                    {
                        _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                    }
                    return result;
                }
                return Enumerable.Empty<TDto>();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get items by subjectId: {ex.Message}", Severity.Error);
                return Enumerable.Empty<TDto>();
            }
        }

        public async Task<IEnumerable<TDto>> GetAllByCustom(string customRoute, CancellationToken token)
        {
            var key = $"getallbycustom_{customRoute}";

            if (_ongoingRequests.TryGetValue(key, out var existingTask) && existingTask is Task<IEnumerable<TDto>> existingCustomTask)
            {
                return await existingCustomTask;
            }

            var task = PerformGetAllByCustom(customRoute, token);
            _ongoingRequests[key] = task;

            try
            {
                return await task;
            }
            finally
            {
                _ongoingRequests.TryRemove(key, out _);
            }
        }

        private async Task<IEnumerable<TDto>> PerformGetAllByCustom(string customRoute, CancellationToken token)
        {
            try
            {
                var response = await _httpClient.GetAsync(customRoute, token);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return Enumerable.Empty<TDto>();

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(cancellationToken: token);
                if (result != null)
                {
                    foreach (var item in result)
                    {
                        _itemList.TryAdd(item.Id, new Tuple<TDto, DateTime>(item, DateTime.UtcNow));
                    }
                    return result;
                }
                return Enumerable.Empty<TDto>();
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to get all items: {ex.Message}", Severity.Error);
                return Enumerable.Empty<TDto>();
            }
        }

        private string ExtractValidationErrorMessage(string errorContent)
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<JsonElement>(errorContent);

                if (errorResponse.TryGetProperty("errors", out var errorsElement))
                {
                    var errorMessages = new List<string>();

                    foreach (var error in errorsElement.EnumerateArray())
                    {
                        if (error.TryGetProperty("message", out var messageElement))
                        {
                            errorMessages.Add(messageElement.GetString() ?? "Unknown validation error");
                        }
                    }

                    return errorMessages.Any()
                        ? string.Join("; ", errorMessages)
                        : "Validation failed";
                }

                if (errorResponse.TryGetProperty("message", out var messageProperty))
                {
                    return messageProperty.GetString() ?? "Validation failed";
                }

                return "Validation failed";
            }
            catch
            {
                return $"Validation failed: {errorContent}";
            }
        }
    }

}

