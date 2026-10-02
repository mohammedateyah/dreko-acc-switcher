using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using DrekoAccSwitcher.Services;
using Localization = DrekoAccSwitcher.Services.Localization;

namespace DrekoAccSwitcher.Views;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        InitializeComponent();
        FlowDirection = Localization.FlowDirection;
        Title = Localization.Text("welcomeTitle");
        CaptionTitle.Text = Localization.Text("welcomeTitle");
        Heading.Text = Localization.Text("welcomeHeading");
        RightsText.Inlines.Add(new Run(Localization.Text("welcomeRightsPrefix")));
        RightsText.Inlines.Add(new Run("DrekoStudio©")
        {
            FlowDirection = System.Windows.FlowDirection.LeftToRight
        });
        RightsText.Inlines.Add(new Run(Localization.Text("welcomeRightsSuffix")));
        VisitPrompt.Text = Localization.Text("welcomeVisitPrompt");
        WebsiteLink.Inlines.Add(new Run(Localization.Text("welcomeWebsiteLink")));
        ContinueButton.Content = Localization.Text("welcomeContinue");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void WebsiteLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Win32Exception ex)
        {
            ShowWebsiteError(ex);
        }
        catch (InvalidOperationException ex)
        {
            ShowWebsiteError(ex);
        }
    }

    private void ShowWebsiteError(Exception exception)
    {
        Log.Write($"Could not open DrekoStudio website from welcome window: {exception}");
        MessageBox.Show(
            this,
            Localization.Format("websiteOpenFailed", exception.Message),
            Localization.Text("appTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning,
            MessageBoxResult.OK,
            Localization.IsArabic
                ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                : MessageBoxOptions.None);
    }
}
