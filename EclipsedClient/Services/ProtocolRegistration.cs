using Microsoft.Win32;

namespace EclipsedClient.Services;

internal static class ProtocolRegistration
{
    private const string ProtocolKey = @"Software\Classes\roblox-player";
    private const string CommandKey = ProtocolKey + @"\shell\open\command";
    private const string BackupKey = @"Software\Solarious\ProtocolBackup";
    private static string Command => $"\"{Environment.ProcessPath}\" \"%1\"";

    internal static bool IsRegistered => ReadCurrentCommand() == Command;

    private static string? ReadCurrentCommand()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(CommandKey);
        return key?.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    internal static void Register()
    {
        if (!string.Equals(System.IO.Path.GetFileName(Environment.ProcessPath), "Solarious.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Open Solarious.exe from its permanent location first.");

        if (IsRegistered)
            return;

        using RegistryKey? current = Registry.CurrentUser.OpenSubKey(CommandKey);
        using RegistryKey backup = Registry.CurrentUser.CreateSubKey(BackupKey);
        if (current?.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value)
            backup.SetValue("PreviousCommand", value, current.GetValueKind(null));
        else
            backup.DeleteValue("PreviousCommand", throwOnMissingValue: false);

        using RegistryKey protocol = Registry.CurrentUser.CreateSubKey(ProtocolKey);
        if (protocol.GetValue("URL Protocol") is null)
        {
            backup.SetValue("AddedUrlProtocol", 1);
            protocol.SetValue("URL Protocol", "");
        }
        else
        {
            backup.SetValue("AddedUrlProtocol", 0);
        }

        using RegistryKey command = Registry.CurrentUser.CreateSubKey(CommandKey);
        command.SetValue(null, Command, RegistryValueKind.String);
    }

    internal static void Restore()
    {
        if (!IsRegistered)
            throw new InvalidOperationException("Another app currently owns the per-user Roblox launch command.");

        using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(BackupKey);
        using RegistryKey command = Registry.CurrentUser.CreateSubKey(CommandKey);
        if (backup?.GetValue("PreviousCommand", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value)
            command.SetValue(null, value, backup.GetValueKind("PreviousCommand"));
        else
            command.DeleteValue(string.Empty, throwOnMissingValue: false);

        if (backup?.GetValue("AddedUrlProtocol") is 1)
        {
            using RegistryKey protocol = Registry.CurrentUser.CreateSubKey(ProtocolKey);
            protocol.DeleteValue("URL Protocol", throwOnMissingValue: false);
        }
        Registry.CurrentUser.DeleteSubKey(BackupKey, throwOnMissingSubKey: false);
    }
}
