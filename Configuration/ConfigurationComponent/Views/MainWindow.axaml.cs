using System;
using Avalonia.Controls;
using ConfigurationComponent.ViewModels;

namespace ConfigurationComponent.Views;

/// <summary>
/// Represents Main Window.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
