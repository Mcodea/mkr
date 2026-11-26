using System.Windows.Markup;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>XAML access to the central texts: <c>Text="{local:Loc about.version}"</c> (read once, when the view is built).</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
