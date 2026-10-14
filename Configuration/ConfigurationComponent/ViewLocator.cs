using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ConfigurationComponent.ViewModels;

namespace ConfigurationComponent;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
/// <summary>
/// Represents View Locator.
/// </summary>
public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// Executes Build.
    /// </summary>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// Executes Match.
    /// </summary>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
