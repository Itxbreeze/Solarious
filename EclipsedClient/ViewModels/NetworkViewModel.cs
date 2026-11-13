using System.Windows.Input;
using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed class NetworkViewModel : ViewModel
{
    private ServerConnection? _connection;
    public string Address => _connection?.Address.ToString() ?? "—";
    public string Region => _connection?.Region ?? "Unavailable";
    public string Place => _connection?.PlaceId.ToString() ?? "—";
    public bool Connected => _connection is not null;
    public string Result { get; private set; } = "";
    public ICommand Ping { get; }

    internal NetworkViewModel(LauncherViewModel shell)
    {
        Ping = shell.Async(async _ =>
        {
            var connection = _connection ?? throw new InvalidOperationException("Join an experience first.");
            Result = "Measuring ICMP round-trip time…";
            Notify(nameof(Result));
            try
            {
                string result = await RobloxLogs.PingAsync(connection.Address);
                if (connection == _connection) Result = result;
            }
            catch
            {
                if (connection == _connection) Result = "";
                throw;
            }
            finally { Notify(nameof(Result)); }
        });
    }

    internal void Update(ServerConnection? connection)
    {
        if (_connection == connection) return;
        _connection = connection;
        Result = "";
        Notify(string.Empty);
    }
}
