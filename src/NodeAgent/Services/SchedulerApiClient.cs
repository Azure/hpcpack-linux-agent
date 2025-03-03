using NodeAgent.Models;
using System.Net;

namespace NodeAgent.Services;

public class HostsUpdate : DiagBase
{
    public string? UpdateId { get; set; }

    public IEnumerable<HostEntry>? Hosts { get; set; }
}

public interface ISchedulerApiClient
{
    Task<int> RegisterAsync(RegisterInfo info, CancellationToken cancelToken = default);

    Task<int> ReportHeartbeatAsync(NodeInfo nodeInfo, CancellationToken cancelToken = default);

    Task<HostsUpdate?> GetHostsAsync(string? updateId, CancellationToken cancelToken = default);

    Task ReportTaskCompletionAsync(string uri, TaskCompletionEventArgs args, CancellationToken cancelToken = default);
}

public class SchedulerApiClient : ISchedulerApiClient
{
    public const string UpdateIdHeaderName = "UpdateId";

    private ILogger _logger;
    private IHttpClientFactory _httpClientFactory;
    private IConfigManager _configManager;
    private INamingClient _namingClient;

    public SchedulerApiClient(
        ILogger<SchedulerApiClient> logger,
        IHttpClientFactory httpClientFactory,
        IConfigManager configManager,
        INamingClient namingClient)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configManager = configManager;
        _namingClient = namingClient;
    }

    public async Task<int> RegisterAsync(RegisterInfo info, CancellationToken cancelToken = default)
    {
        string? uri = null;
        try
        {
            uri = await _namingClient.ResolveUriAsync(
                _configManager.Config.RegisterUri, _configManager.Config.DefaultServiceName, cancelToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Register to {uri} with {value}", uri, info);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(uri, info, cancelToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            //Return a new interval value in millisecond, which the next call should wait for.
            return await response.Content.ReadFromJsonAsync<int>(cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when registering to {uri}.", uri);
            _namingClient.InvalidateCache();
            throw;
        }
    }

    public async Task<int> ReportHeartbeatAsync(NodeInfo nodeInfo, CancellationToken cancelToken = default)
    {
        string? uri = null;
        try
        {
            uri = await _namingClient.ResolveUriAsync(
                _configManager.Config.HeartbeatUri, _configManager.Config.DefaultServiceName, cancelToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Report heartbeat to {uri} with {value}", uri, nodeInfo);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(uri, nodeInfo, cancelToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            //Return a new interval value in millisecond, which the next call should wait for.
            return await response.Content.ReadFromJsonAsync<int>(cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when reporting heartbeat to {uri}.", uri);
            _namingClient.InvalidateCache();
            throw;
        }
    }

    public async Task<HostsUpdate?> GetHostsAsync(string? updateId, CancellationToken cancelToken = default)
    {
        string? uri = null;
        try
        {
            uri = await _namingClient.ResolveUriAsync(
                _configManager.Config.HostsFileUri, _configManager.Config.DefaultServiceName, cancelToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Get hosts info from {uri} with update id '{id}'", uri, updateId);

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(updateId))
            {
                request.Headers.Add(UpdateIdHeaderName, updateId);
            }

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                _logger.LogDebug("No hosts update from server.");
                return null;
            }

            var values = response.Headers.GetValues(UpdateIdHeaderName);
            var newUpdateId = values.First();
            var hostEntries = await response.Content.ReadFromJsonAsync<IEnumerable<HostEntry>>(cancelToken).ConfigureAwait(false);
            var update = new HostsUpdate() { UpdateId = newUpdateId, Hosts = hostEntries };

            _logger.LogDebug("Received hosts update {update}", update);

            return update;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting hosts info from {uri}.", uri);
            _namingClient.InvalidateCache();
            throw;
        }
    }

    public async Task ReportTaskCompletionAsync(string uri, TaskCompletionEventArgs args, CancellationToken cancelToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_configManager.Config.TaskCompletionUri))
            {
                uri = _configManager.Config.TaskCompletionUri;
            }
            uri = await _namingClient.ResolveUriAsync(uri, _configManager.Config.DefaultServiceName, cancelToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Report task completion to {uri} with {args}", uri, args);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(uri, args, cancelToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when reporting task completion to {uri}.", uri);
            _namingClient.InvalidateCache();
            throw;
        }
    }
}
