using System.Net;

namespace XivVoices.Services;

public partial interface IDataService : IHostedService
{
  ServerStatus ServerStatus { get; }
  HttpClient HttpClient { get; }
  bool IsLoggingIn { get; }
  Task UpdateServerStatus(CancellationToken token);
  void Login();
}

public enum ServerStatus
{
  OFFLINE,
  UNAUTHORIZED,
  ONLINE
}

public partial class DataService
{
  public ServerStatus ServerStatus { get; private set; } = ServerStatus.OFFLINE;
  public HttpClient HttpClient { get; private set; } = new();
  public bool IsLoggingIn { get; private set; } = false;

  private CookieContainer _cookieContainer = new();

  private string? _cookiesPath
  {
    get
    {
      string? dataDirectory = DataDirectory;
      if (dataDirectory == null) return null;
      return Path.Join(dataDirectory, "cookies.json");
    }
  }

  private void AuthStart()
  {
    _cookieContainer = LoadCookies();
    HttpClientHandler handler = new()
    {
      CookieContainer = _cookieContainer
    };
    HttpClient = new(handler);
  }

  private void AuthStop()
  {
    SaveCookies();
  }

  public async Task UpdateServerStatus(CancellationToken token)
  {
    try
    {
      string statusEndpoint = $"{ServerUrl}/_status";
      HttpResponseMessage response = await HttpClient.GetAsync(statusEndpoint, token);

      if (response.IsSuccessStatusCode)
      {
        string content = await response.Content.ReadAsStringAsync(token);
        if (content.Trim().Equals("xivv", StringComparison.OrdinalIgnoreCase))
        {
          _logger.Debug("Server is online");
          ServerStatus = ServerStatus.ONLINE;
        }
        else
        {
          _logger.Debug($"'{statusEndpoint}' returned invalid response '{content}'");
          ServerStatus = ServerStatus.OFFLINE;
        }
      }
      else
      {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
          _logger.Debug("Server returned 401 Unauthorized");
          ServerStatus = ServerStatus.UNAUTHORIZED;
          OnOpenConfigWindow?.Invoke(this, ConfigWindowTab.Overview);
        }
        else
        {
          _logger.Debug($"Server returned status code: {response.StatusCode}");
          ServerStatus = ServerStatus.OFFLINE;
        }
      }
    }
    catch (OperationCanceledException)
    {
      _logger.Debug("UpdateServerStatus was cancelled");
    }
    catch
    {
      _logger.Debug($"UpdateServerStatus failed to reach server: {ServerUrl}");
      ServerStatus = ServerStatus.OFFLINE;
    }

    _ = SetLatestVersion();
  }

  public void Login()
  {
    if (ServerStatus == ServerStatus.OFFLINE) return;
    if (IsLoggingIn) return;
    IsLoggingIn = true;

    string state = new(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 10).Select(s => s[new Random().Next(s.Length)]).ToArray());
    string url = $"{ServerUrl}/auth/oauth2/discord?state={state}";
    Util.OpenLink(url);

    _ = CheckLoginStatus(state);
  }

  private CookieContainer LoadCookies()
  {
    CookieContainer cookieContainer = new();
    string? cookiesPath = _cookiesPath;

    if (cookiesPath != null && File.Exists(cookiesPath))
    {
      string json = File.ReadAllText(cookiesPath);
      List<SerializableCookie>? serializableCookies = JsonSerializer.Deserialize<List<SerializableCookie>>(json, JsonOptions.Read);

      if (serializableCookies != null)
      {
        foreach (SerializableCookie serializableCookie in serializableCookies)
        {
          Cookie cookie = new(serializableCookie.Name, serializableCookie.Value)
          {
            Domain = serializableCookie.Domain,
            Path = serializableCookie.Path,
            Expires = serializableCookie.Expires,
            Secure = serializableCookie.IsSecure,
            HttpOnly = serializableCookie.IsHttpOnly
          };
          cookieContainer.Add(cookie);
        }
      }
    }

    return cookieContainer;
  }

  private void SaveCookies()
  {
    string? cookiesPath = _cookiesPath;
    if (cookiesPath == null) return;

    List<SerializableCookie> serializableCookies = [];
    Uri uri = new(ServerUrl);

    foreach (Cookie cookie in _cookieContainer.GetCookies(uri))
    {
      serializableCookies.Add(new SerializableCookie
      {
        Name = cookie.Name,
        Value = cookie.Value,
        Domain = cookie.Domain,
        Path = cookie.Path,
        Expires = cookie.Expires,
        IsSecure = cookie.Secure,
        IsHttpOnly = cookie.HttpOnly,
      });
    }

    string json = JsonSerializer.Serialize(serializableCookies, JsonOptions.Write);
    File.WriteAllText(cookiesPath, json);
  }

  private async Task CheckLoginStatus(string state)
  {
    using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));

    while (!cts.Token.IsCancellationRequested)
    {
      await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

      try
      {
        string statusEndpoint = $"{ServerUrl}/auth/oauth2/discord/authorized?state={state}";
        HttpResponseMessage response = await HttpClient.GetAsync(statusEndpoint, cts.Token);

        if (response.IsSuccessStatusCode)
        {
          _logger.Debug("Logged in successfully!");
          IsLoggingIn = false;
          _ = UpdateServerStatus(default);
          SaveCookies();
          return;
        }
        else
        {
          _logger.Debug($"Awaiting login. Response status code: {response.StatusCode}");
        }
      }
      catch (OperationCanceledException)
      {
        _logger.Debug("CheckLoginStatus was cancelled.");
        return;
      }
      catch (Exception ex)
      {
        _logger.Error(ex);
        IsLoggingIn = false;
        return;
      }
    }

    _logger.Debug("Login check timed out after 5 minutes.");
    IsLoggingIn = false;
  }
}

[Serializable]
public class SerializableCookie
{
  public required string Name { get; set; }
  public required string Value { get; set; }
  public required string Domain { get; set; }
  public required string Path { get; set; }
  public required DateTime Expires { get; set; }
  public required bool IsSecure { get; set; }
  public required bool IsHttpOnly { get; set; }
}
