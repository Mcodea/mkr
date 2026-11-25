namespace Mkr.Desktop;

/// <summary>A step of the study shown either inline in the study workspace or in a thin dialog host. The host calls <see cref="Cancel"/> when the view goes away.</summary>
internal interface IStepView
{
    void Cancel();
}
