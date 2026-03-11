using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;

namespace VideoAnalysis.App.Views;

public partial class MainWindow : Window
{
    private bool _isSynchronizingMenus;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnFileMenuActionClick(object? sender, RoutedEventArgs e)
    {
        FileMenuButton.IsChecked = false;
    }

    private void OnViewMenuActionClick(object? sender, RoutedEventArgs e)
    {
        ViewMenuButton.IsChecked = false;
    }

    private void OnHelpMenuActionClick(object? sender, RoutedEventArgs e)
    {
        HelpMenuButton.IsChecked = false;
    }

    private void OnFileMenuChecked(object? sender, RoutedEventArgs e)
    {
        CloseOtherMenus(FileMenuButton);
    }

    private void OnViewMenuChecked(object? sender, RoutedEventArgs e)
    {
        CloseOtherMenus(ViewMenuButton);
    }

    private void OnHelpMenuChecked(object? sender, RoutedEventArgs e)
    {
        CloseOtherMenus(HelpMenuButton);
    }

    private void CloseOtherMenus(ToggleButton activeButton)
    {
        if (_isSynchronizingMenus)
        {
            return;
        }

        _isSynchronizingMenus = true;
        try
        {
            if (!ReferenceEquals(activeButton, FileMenuButton))
            {
                FileMenuButton.IsChecked = false;
            }

            if (!ReferenceEquals(activeButton, ViewMenuButton))
            {
                ViewMenuButton.IsChecked = false;
            }

            if (!ReferenceEquals(activeButton, HelpMenuButton))
            {
                HelpMenuButton.IsChecked = false;
            }
        }
        finally
        {
            _isSynchronizingMenus = false;
        }
    }
}
