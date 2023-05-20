﻿using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using YoutubeDl.Wpf.Models;

namespace YoutubeDl.Wpf.ViewModels;

public class PresetDialogViewModel : ReactiveValidationObject
{
    private readonly List<BackendArgument> _backendArguments = new();
    private readonly Action<bool> _controlDialogAction;
    private Preset? _preset;
    private Action<Preset>? _saveAction;

    [Reactive]
    public string Name { get; set; } = "";

    [Reactive]
    public string FormatArg { get; set; } = "";

    [Reactive]
    public string ContainerArg { get; set; } = "";

    [Reactive]
    public bool IsYtdlSupported { get; set; } = true;

    [Reactive]
    public bool IsYtdlpSupported { get; set; } = true;

    public ObservableCollection<object> ArgumentChips { get; set; } = new();

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    public PresetDialogViewModel(Action<bool> controlDialogAction)
    {
        _controlDialogAction = controlDialogAction;

        ArgumentChips = new()
        {
            new AddArgumentViewModel(AddArgument),
        };

        var canSave = this.WhenAnyValue(
            x => x.Name,
            x => x.IsYtdlSupported,
            x => x.IsYtdlpSupported,
            (name, ytdl, ytdlp) => !string.IsNullOrEmpty(name) && (ytdl || ytdlp));

        SaveCommand = ReactiveCommand.Create(Save, canSave);
        DiscardCommand = ReactiveCommand.Create(CloseDialog);

        this.WhenAnyValue(
            x => x.FormatArg,
            x => x.ContainerArg,
            x => x.IsYtdlSupported,
            x => x.IsYtdlpSupported)
            .Throttle(TimeSpan.FromSeconds(0.1))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(((string formatArg, string containerArg, bool isYtdlSupported, bool isYtdlpSupported) x) => UpdatePreset(x.formatArg, x.containerArg, x.isYtdlSupported, x.isYtdlpSupported));
    }

    public void AddOrEditPreset(Preset preset, Action<Preset> saveAction)
    {
        _preset = preset;
        _saveAction = saveAction;
        LoadPreset(preset);
        OpenDialog();
    }

    private void OpenDialog() => _controlDialogAction(true);

    private void CloseDialog() => _controlDialogAction(false);

    private void Save()
    {
        if (_preset is null)
        {
            throw new InvalidOperationException("Preset is not loaded.");
        }

        if (_saveAction is null)
        {
            throw new InvalidOperationException("Missing save action for preset.");
        }

        CloseDialog();
        UpdatePresetNameAndExtraArgs(_preset);
        _saveAction(_preset);
    }

    private void LoadPresetExtraArgs(Preset preset)
    {
        // ArgumentChips is guaranteed to have at least one element.
        var index = ArgumentChips.Count - 1;

        // Remove extra argument chips.
        for (var i = 0; i < _backendArguments.Count; i++)
        {
            index--;
            ArgumentChips.RemoveAt(index);
        }

        // Clear extra arguments.
        _backendArguments.Clear();

        // Add new extra arguments.
        foreach (var extraArg in preset.ExtraArgs)
        {
            AddArgument(extraArg);
        }
    }

    private void UpdatePresetNameAndExtraArgs(Preset preset) => _preset = preset with
    {
        Name = Name,
        ExtraArgs = _backendArguments.Select(x => x.Argument).ToArray(),
    };

    private void LoadPreset(Preset preset)
    {
        LoadPresetExtraArgs(preset);
        Name = preset.Name ?? "";
        FormatArg = preset.FormatArg ?? "";
        ContainerArg = preset.ContainerArg ?? "";
        IsYtdlSupported = (preset.SupportedBackends & BackendTypes.Ytdl) == BackendTypes.Ytdl;
        IsYtdlpSupported = (preset.SupportedBackends & BackendTypes.Ytdlp) == BackendTypes.Ytdlp;
    }

    private void UpdatePreset(string formatArg, string containerArg, bool isYtdlSupported, bool isYtdlpSupported)
    {
        if (_preset is null)
        {
            return;
        }

        var supportedBackends = BackendTypes.None;
        if (isYtdlSupported)
        {
            supportedBackends |= BackendTypes.Ytdl;
        }
        if (isYtdlpSupported)
        {
            supportedBackends |= BackendTypes.Ytdlp;
        }

        _preset = _preset with
        {
            FormatArg = string.IsNullOrEmpty(formatArg) ? null : formatArg,
            ContainerArg = string.IsNullOrEmpty(containerArg) ? null : containerArg,
            SupportedBackends = supportedBackends,
        };

        GenerateArgumentChips(_preset);
    }

    private void DeleteArgumentChip(ArgumentChipViewModel item)
    {
        if (item.IsRemovable)
        {
            _backendArguments.Remove(item.Argument);
            ArgumentChips.Remove(item);
        }
    }

    private void AddArgument(string argument)
    {
        var backendArgument = new BackendArgument(argument);
        _backendArguments.Add(backendArgument);

        // Insert right before AddArgumentViewModel.
        ArgumentChips.Insert(ArgumentChips.Count - 1, new ArgumentChipViewModel(backendArgument, true, DeleteArgumentChip));
    }

    /// <summary>
    /// Generates and updates non-extra argument chips.
    /// </summary>
    private void GenerateArgumentChips(Preset preset)
    {
        for (var i = 0; i < ArgumentChips.Count - _backendArguments.Count - 1; i++)
        {
            ArgumentChips.RemoveAt(i);
        }

        var index = 0;

        foreach (var arg in preset.GetNonExtraArgs())
        {
            ArgumentChips.Insert(index, new ArgumentChipViewModel(new(arg), false, DeleteArgumentChip));
            index++;
        }
    }
}
