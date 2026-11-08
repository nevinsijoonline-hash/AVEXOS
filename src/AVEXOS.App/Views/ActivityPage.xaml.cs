using System;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
    {
        InitializeComponent();
        Loaded += (s, e) => LoadActivity();
    }

    private void LoadActivity()
    {
        try
        {
            var audit = App.Services.GetRequiredService<IAuditLog>();
            ActivityList.ItemsSource = audit.Recent(100);
        }
        catch (Exception)
        {
            // Log but don't crash
        }
    }
}
