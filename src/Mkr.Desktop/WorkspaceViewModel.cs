using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Mkr.Core;

namespace Mkr.Desktop;

public sealed class WorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IProjectStore _store;
    private readonly IEngineDiagnostics _engine;
    private readonly CancellationTokenSource _lifetime = new();
    private string _name = "";
    private string _status = "";
    private bool _checking;

    public WorkspaceViewModel(IProjectStore store, IEngineDiagnostics engine)
    {
        _store = store;
        _engine = engine;
        Projects = new ObservableCollection<Project>(_store.List());
        Status = Projects.Count == 0 ? "Aucun projet enregistré." : $"{Projects.Count} projet(s) enregistré(s).";
        CreateProject = new Command(AddProject, () => !string.IsNullOrWhiteSpace(Name));
        CheckEngine = new Command(async () => await VerifyEngineAsync(), () => !_checking);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Project> Projects { get; }
    public ICommand CreateProject { get; }
    public ICommand CheckEngine { get; }

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            Notify();
            ((Command)CreateProject).Refresh();
        }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; Notify(); }
    }

    public void CancelDiagnostics()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void AddProject()
    {
        Project project;
        try { project = Project.Create(Name); }
        catch (ArgumentException error)
        {
            Status = error.Message;
            return;
        }

        try
        {
            _store.Add(project);
            Projects.Add(project);
            Name = "";
            Status = "Projet enregistré.";
        }
        catch (Exception error)
        {
            Trace.TraceError("project.create {0}", error.GetType().Name);
            Status = "Enregistrement impossible. Vérifiez l’accès au stockage local.";
        }
    }

    private async Task VerifyEngineAsync()
    {
        _checking = true;
        ((Command)CheckEngine).Refresh();
        Status = "Vérification du moteur Python…";
        try
        {
            Status = $"Moteur Python disponible · version {await _engine.GetVersionAsync(_lifetime.Token)}.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            Status = "Vérification annulée.";
        }
        catch (Exception error)
        {
            Trace.TraceError("engine.ping {0}", error.GetType().Name);
            Status = error is TimeoutException ? error.Message : "Moteur indisponible. Vérifiez l’installation de Python et MKR_PYTHON.";
        }
        finally
        {
            _checking = false;
            ((Command)CheckEngine).Refresh();
        }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class Command(Action execute, Func<bool> enabled) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => enabled();
        public void Execute(object? parameter) => execute();
        public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
