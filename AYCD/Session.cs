using System.Net;
using System.Text;
using System.Text.Json;
using AYCD.Dtos;
using AYCD.Util;
using RuriLib.Http;
using RuriLib.Proxies;
using RuriLib.Proxies.Clients;

namespace AYCD;
public class Session(string apiKey) {
    private string? _token;
    private long _tokenExpiresAt;
    private long _tasksFetchAt;
    private bool _debug;

    // per session but also shared across multiple threads
    private readonly SemaphoreSlim _authSemaphore = new(1, 1);
    private readonly HashSet<string> _pendingTasks = [];
    private readonly Dictionary<string, TaskResponseDto> _tasks = new();
    private readonly object _tasksLock = new();
    
    //shared across all sessions
    private static readonly Dictionary<string, Session> SessionMap = new();
    private static readonly object SessionMapLock = new();
    

    public static Session NewSession(string apiKey)  {
        return NewSessionWithHttpClient(apiKey);
    }
    
    public void EnableDebug() {
        _debug = true;
    }
    
    public void DisableDebug() {
        _debug = false;
    }
    
    public static Session NewSessionWithHttpClient(string apiKey) {
        lock (SessionMapLock)  {
            // compute if absent
            if (!SessionMap.TryGetValue(apiKey, out var session))  {
                session = new Session(apiKey);
                SessionMap[apiKey] = session;
            }
            return session;
        }
    }
    
     public async Task<TaskResponseDto?> Solve(CreateTaskDto taskRequest, TimeSpan timeout, CancellationToken cancellationToken) { 
        Log("Trying to solve task with id: " + taskRequest.TaskId);
        var errorMessage = await SendAsync(taskRequest, Constants.TasksCreateUrl, cancellationToken);
        if ( errorMessage != null )  {
            Log(errorMessage);
            return null;
        }
        
        _pendingTasks.Add(taskRequest.TaskId);
        Log($"New pending task: {taskRequest.TaskId} -> Size: {_pendingTasks.Count}");
        var taskResponse = await WaitForTaskAsync(taskRequest.TaskId, timeout, cancellationToken);
        if (taskResponse == null) {
            Log($"Error waiting for Task {taskRequest.TaskId} -> Canceling Task");
            var cancelErr = await CancelManyTasksAsync([taskRequest.TaskId], cancellationToken);
            if (cancelErr != null) {
                Log($"Error while cancelling Task {taskRequest.TaskId}: {cancelErr}");
            }
        }
        
        return taskResponse;
    }

    private async Task<string?> CancelManyTasksAsync(List<string> taskIds, CancellationToken cancellationToken) {
        var cancelTaskRequest = new CancelTaskRequest(taskIds, false);
        foreach (var taskId in taskIds) {
            _pendingTasks.Remove(taskId);
        }

        return await SendAsync(cancelTaskRequest, Constants.TasksCancelUrl, cancellationToken);
    }

    public async Task CancelAllTasksAsync() {
        var cancelTaskRequest = new CancelTaskRequest(_pendingTasks.ToList(), false);
        _pendingTasks.Clear();
        await SendAsync(cancelTaskRequest, Constants.TasksCancelUrl, CancellationToken.None);
    }

    private async Task<TaskResponseDto?> WaitForTaskAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken) {
        var createdAt = DateTime.Now;
        try {
            await Task.Delay(5000, cancellationToken);
            var startTime = DateTime.Now;

            while (_pendingTasks.Count > 0) {
                var (delay, err) = await FetchTasksAsync(cancellationToken);
                if (err != null) {
                    Log($"error while fetching tasks: {err}");
                } else {
                    lock (_tasksLock) {
                        if (_tasks.Remove(taskId, out var taskResponse)) {
                            //if it's in the map, it means the task is completed -> remove from pending tasks and return the response
                            _pendingTasks.Remove(taskId);
                            Log("Task completed: " + taskResponse);
                            return taskResponse;
                        }
                    }
                }
                
                if (DateTime.Now - startTime > timeout) {
                    Log($"Timeout for {taskId} reached -> Cancelling Task");
                    var cancelErr = await CancelManyTasksAsync([taskId], cancellationToken);
                    if (cancelErr != null) {
                        Log($"Error while cancelling Task {taskId}: {cancelErr}");
                    }

                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
            }
        }
        catch (OperationCanceledException) {
            Log("Cancellation requested -> Cancelling all tasks");
            await CancelAllTasksAsync();
        }
        
        return BuildCancelResponse(taskId, createdAt);
    }

    private static TaskResponseDto BuildCancelResponse(string taskId, DateTime createdAt) {
        return new TaskResponseDto(
            taskId,
            new DateTimeOffset(createdAt).ToUnixTimeSeconds(),
            null,
            "cancelled"
        );
    }
    private async Task<(int, string?)> FetchTasksAsync(CancellationToken cancellationToken) {
        var nextFetchDelay = 5;
        lock (_tasksLock) {
            if (_tasksFetchAt > DateTimeOffset.Now.ToUnixTimeSeconds()) {
                return (nextFetchDelay, null);
            }  
            _tasksFetchAt = DateTimeOffset.Now.ToUnixTimeSeconds() + nextFetchDelay;
        }

        using var request = new HttpRequestMessage();
        request.RequestUri = new Uri(Constants.TasksUrl);
        request.Method = HttpMethod.Get;

        var (responseMessage, statusCode) = await DoAsync(request, cancellationToken);
        if (responseMessage == null || !IsStatus2Xx(statusCode)) {
            return (nextFetchDelay, "response status code is not 2xx while fetching tasks");
        }

        var tasks = JsonSerializer.Deserialize<List<TaskResponseDto>>(responseMessage);
        if (tasks == null) {
            return (nextFetchDelay, "error decoding response");
        }

        lock (_tasksLock) {
            foreach (var task in tasks) {
                if (task.TaskId != null) {
                    _tasks[task.TaskId] = task;
                }
            }
        }

        if (tasks.Count >= 100) {
            nextFetchDelay = 1;
        }
        
        _tasksFetchAt = DateTimeOffset.Now.ToUnixTimeSeconds() + nextFetchDelay;
        Log($"fetched {tasks.Count} tasks with next delay: {nextFetchDelay}");

        return (nextFetchDelay, null);
    }

    private async Task<string?> SendAsync(object req, string url, CancellationToken cancellationToken)  {
        using var request = new HttpRequestMessage();
        request.RequestUri = new Uri(url);
        request.Method = HttpMethod.Post;
        request.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

        Log("Creating new task...");
        
        var (responseMessage, statusCode) = await DoAsync(request, cancellationToken);
        if (responseMessage == null || !IsStatus2Xx(statusCode)) {
            Log("response status code is not 2xx while sending request");
            return "response status code is not 2xx while sending request";
        }
        
        Log("Done sending request");
        return null;
    }

    private async Task<(string?, HttpStatusCode)> DoAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Log("Before auth token check");
        await _authSemaphore.WaitAsync(cancellationToken);
        try {
            if (_tokenExpiresAt <= DateTimeOffset.Now.ToUnixTimeSeconds()) {
                Log("new auth token is required.");
                var refreshErr = await RefreshAuthTokenAsync(cancellationToken);
                if (refreshErr == null) {
                    Log("Refreshed auth token");
                }
            }
        }
        finally {
            _authSemaphore.Release();
        }
        
        if (string.IsNullOrEmpty(_token)) {
            throw new Exception("auth token is not available");
        }

        lock (request.Headers) {
            request.Headers.TryAddWithoutValidation("Authorization", $"Token {_token}");
        }
        try {
            using var response = await SendAsyncClient(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return (content, response.StatusCode);
        }
        catch (OperationCanceledException) {
            Log("Operation was canceled.");
            return (null, HttpStatusCode.BadRequest); // Or handle the cancellation
        }
        catch (HttpRequestException ex) {
            Log($"HTTP request failed: {ex.Message}");
            throw; // Or handle the specific network error
        }
        catch (IOException ex) {
            Log($"IO error: {ex.Message}");
            throw; // Or handle IO-specific issues
        }
        catch (Exception ex) {
            Log($"Unexpected error: {ex.Message}");
            Console.WriteLine(Environment.StackTrace);
            throw; // Catch any other exceptions
        } 
    }

    private static async Task<HttpResponseMessage> SendAsyncClient(HttpRequestMessage request, CancellationToken cancellationToken) {
        var settings = new ProxySettings();
        var proxyClient = new NoProxyClient(settings);
        var handler = new ProxyClientHandler(proxyClient) {
            CookieContainer = new CookieContainer()
        };
            
        using var client = new HttpClient(handler);
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task<string?> RefreshAuthTokenAsync(CancellationToken cancellationToken) {
        var url = $"{Constants.AuthUrl}?apiKey={apiKey}";
        var request = new HttpRequestMessage {
            RequestUri = new Uri(url),
            Method = HttpMethod.Get
        };
        
        using var response = await SendAsyncClient(request, cancellationToken);
        if (!IsStatus2Xx(response.StatusCode)) {
            return "response status code is not 2xx while trying to refresh auth token";
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(content);
        if (authResponse == null) {
            return "error decoding auth response";
        }
        
        _token = authResponse.Token;
        _tokenExpiresAt = authResponse.ExpiresAt;
        
        Log($"auth token refreshed with new expiresAt: {_tokenExpiresAt}");
        return null;
    }
    private void Log(string msg) {
        if (_debug) {
            Console.WriteLine(msg);
        }
    }
    private static bool IsStatus2Xx(HttpStatusCode statusCode) {
        return statusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;
    }
}