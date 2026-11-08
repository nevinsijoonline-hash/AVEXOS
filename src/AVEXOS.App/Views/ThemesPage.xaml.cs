using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class ThemesPage : Page
{
    private ThemeDocument? _selectedTheme;

    public ThemesPage()
    {
        InitializeComponent();
        Loaded += (s, e) => LoadThemes();
    }

    private void LoadThemes()
    {
        var catalog = App.Services.GetRequiredService<IThemeCatalog>();
        ThemeList.ItemsSource = catalog.BuiltIn;
        if (catalog.BuiltIn.Count > 0) ThemeList.SelectedIndex = 0;
    }

    private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTheme = ThemeList.SelectedItem as ThemeDocument;
    }

    private void ApplyThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null) return;
        var themeService = App.Services.GetRequiredService<ThemeService>();
        themeService.Apply(_selectedTheme.Id, _selectedTheme.Mode);
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ProposalStatus.Text = "Enter a prompt first.";
            return;
        }

        try
        {
            var generator = App.Services.GetRequiredService<LocalProposalService>();
            var result = generator.Propose(prompt);
            if (result.IsValid)
            {
                ProposalStatus.Text = $"Generated: {result.Theme.Name} ({result.Theme.Mode})";
            }
            else
            {
                ProposalStatus.Text = $"Invalid: {string.Join("; ", result.Validation.Errors)}";
            }
        }
        catch (Exception ex)
        {
            ProposalStatus.Text = $"Error: {ex.Message}";
        }
    }
}
