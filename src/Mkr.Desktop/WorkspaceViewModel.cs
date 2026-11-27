using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
        Status = Projects.Count == 0 ? Loc.T("cs.aucun_projet_enregistre") : Loc.T("cs.projet_s_enregistre_s", Projects.Count);
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
            Status = Loc.T("cs.projet_enregistre");
        }
        catch (Exception error)
        {
            Trace.TraceError("project.create {0}", error.GetType().Name);
            Status = Loc.T("cs.enregistrement_impossible_verifiez_lacces_au");
        }
    }

    private async Task VerifyEngineAsync()
    {
        _checking = true;
        ((Command)CheckEngine).Refresh();
        Status = Loc.T("cs.verification_du_moteur_python");
        try
        {
            var version = await _engine.GetVersionAsync(_lifetime.Token);
            await _engine.VerifyContractAsync(_lifetime.Token);
            Status = Loc.T("cs.moteur_python_disponible_version", version);
        }
        catch (InvalidDataException error)
        {
            Trace.TraceError("engine.contract {0}", error.Message);
            Status = Loc.T("cs.moteur_python_incompatible_avec_le_contrat", error.Message);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            Status = Loc.T("cs.verification_annulee");
        }
        catch (Exception error)
        {
            Trace.TraceError("engine.ping {0}", error.GetType().Name);
            Status = error is TimeoutException ? error.Message : Loc.T("cs.moteur_indisponible_verifiez_linstallation_de");
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
