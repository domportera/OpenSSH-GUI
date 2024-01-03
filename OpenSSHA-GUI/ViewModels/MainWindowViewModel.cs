﻿using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using OpenSSHA_GUI.Views;
using OpenSSHALib.Lib;
using OpenSSHALib.Models;
using ReactiveUI;

namespace OpenSSHA_GUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ObservableCollection<SshKey> _sshKeys = new(DirectoryCrawler.GetAllKeys());

    public readonly Interaction<ConfirmDialogViewModel, ConfirmDialogViewModel?> ShowConfirm = new();
    public readonly Interaction<AddKeyWindowViewModel, AddKeyWindowViewModel?> ShowCreate = new();
    public readonly Interaction<EditKnownHostsViewModel, EditKnownHostsViewModel?> ShowEditKnownHosts = new();

    public ReactiveCommand<Unit, EditKnownHostsViewModel?> OpenEditKnownHostsWindow =>
        ReactiveCommand.CreateFromTask<Unit, EditKnownHostsViewModel?>(async e =>
        {
            var editKnownHosts = new EditKnownHostsViewModel();
            return await ShowEditKnownHosts.Handle(editKnownHosts);
        });

    public ReactiveCommand<Unit, AddKeyWindowViewModel?> OpenCreateKeyWindow =>
        ReactiveCommand.CreateFromTask<Unit, AddKeyWindowViewModel?>(async e =>
        {
            var create = new AddKeyWindowViewModel();
            var result = await ShowCreate.Handle(create);
            if (result == null) return result;
            var newKey = await result.RunKeyGen();
            if (newKey != null) SshKeys.Add(newKey);
            return result;
        });

    public ReactiveCommand<SshKey, ConfirmDialogViewModel?> DeleteKey =>
        ReactiveCommand.CreateFromTask<SshKey, ConfirmDialogViewModel?>(async u =>
        {
            var confirm = new ConfirmDialogViewModel(StringsAndTexts.MainWindowViewModelDeleteKeyQuestionText, 
                StringsAndTexts.MainWindowViewModelDeleteKeyOkText, StringsAndTexts.MainWindowViewModelDeleteKeyNoText);
            var result = await ShowConfirm.Handle(confirm);
            if (result is { Consent: false }) return result;
            u.DeleteKeys();
            SshKeys.Remove(u);
            return result;
        });

    public ObservableCollection<SshKey> SshKeys
    {
        get => _sshKeys;
        set => this.RaiseAndSetIfChanged(ref _sshKeys, value);
    }


    public async Task OpenExportWindow(SshKey key)
    {
        var export = await key.ExportKey();
        if (export is null) return;
        var win = new ExportWindow
        {
            DataContext = new ExportWindowViewModel
            {
                Export = export
            },
            Title = string.Format(StringsAndTexts.MainWindowViewModelDynamicExportWindowTitle, key.KeyTypeString, key.Fingerprint),
            ShowActivated = true,
            ShowInTaskbar = true,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.Show();
    }
}