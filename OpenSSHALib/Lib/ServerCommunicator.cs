﻿using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using OpenSSHALib.Models;
using Renci.SshNet;

namespace OpenSSHALib.Lib;

public static class ServerCommunicator
{
    public const string SshPathWithVariable = "$HOME/.ssh";

    public static bool TestConnection(string ipAddressOrHostname, string user, string userPassword,
        [NotNullWhen(false)] out string? message)
    {
        message = null;
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            message = "Machine is not connected to the Internet";
            return false;
        }

        try
        {
            using var sshClient = new SshClient(ipAddressOrHostname, user, userPassword);
            sshClient.Connect();
            var result = sshClient.IsConnected;
            sshClient.Disconnect();
            return result;
        }
        catch (Exception e)
        {
            message = e.Message;
            return false;
        }
    }

    public static bool TryOpenSshConnection(string ipAddressOrHostname, string user, string userPassword,
        [NotNullWhen(true)] out SshClient? connection, [NotNullWhen(false)] out string? message)
    {
        connection = null;
        message = null;
        try
        {
            var sshClient = new SshClient(ipAddressOrHostname, user, userPassword);
            sshClient.Connect();
            var connectionResult = sshClient.IsConnected;
            if (!connectionResult) return connectionResult;
            var checkOs = sshClient.RunCommand("uname -s");
            if (!checkOs.Result.Contains("linux", StringComparison.CurrentCultureIgnoreCase))
                throw new NotSupportedException("KeyToServer upload does not support any other OS than Linux!");
            connection = sshClient;
            return connectionResult;
        }
        catch (Exception e)
        {
            message = e.Message;
            return false;
        }
    }

    private static bool CreateAuthorizedKeysIfNotExist(this SshClient clientConnection,
        [NotNullWhen(false)] out string? message)
    {
        message = null;
        var listDirectory = clientConnection.RunCommand($"ls {SshPathWithVariable}");
        if (listDirectory.Result.Split("\n", StringSplitOptions.RemoveEmptyEntries)
            .Contains("authorized_keys")) return true;
        var createAuthorizedKeysFile = clientConnection.RunCommand(
            $"touch {SshPathWithVariable}/authorized_keys && chmod 700 {SshPathWithVariable}/authorized_keys");
        if (createAuthorizedKeysFile.ExitStatus == 0) return true;
        message = createAuthorizedKeysFile.Error + "\n" + createAuthorizedKeysFile.Result;
        return false;
    }

    private static bool KeyAlreadyExists(this SshClient client, string export)
    {
        var command = client.RunCommand("cat $HOME/.ssh/authorized_keys");
        if (command.ExitStatus != 0) throw new ApplicationException("Cat command was not executed successfully");
        return command.Result.Trim().Split(KnownHostsFile.LineEnding).Contains(export.Trim());
    }

    public static async Task<string> PutKeyToServer(this SshClient clientConnection, SshPublicKey publicKey)
    {
        if (!clientConnection.CreateAuthorizedKeysIfNotExist(out var errorMessage))
            throw new ApplicationException(errorMessage);
        var export = await publicKey.ExportKeyAsync();
        if (export is null) return "Key could not be exported!";
        if (clientConnection.KeyAlreadyExists(export))
            throw new ApplicationException("Key does already exist on host!");
        var command = clientConnection.RunCommand($"echo \"{export}\" >> {SshPathWithVariable}/authorized_keys");
        if (command.ExitStatus != 0) throw new Exception(command.Error + "\n" + command.Result);
        return "Key successfully uploaded";
    }
}