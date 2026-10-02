using System.Windows;
using System.Windows.Input;
using DrekoAccSwitcher.Services;
using Localization = DrekoAccSwitcher.Services.Localization;

namespace DrekoAccSwitcher.Views;

public partial class NameDialog : Window
{
    public string ResultText => NameBox.Text.Trim();

    public NameDialog(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        FlowDirection = Localization.FlowDirection;
        CaptionHeading.Text = title;
        Heading.Text = title;
        LabelText.Text = label;
        CancelButton.Content = Localization.Text("cancel");
        SaveButton.Content = Localization.Text("save");
        NameBox.Text = initial;
        NameBox.SelectAll();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) DialogResult = false;
        if (e.Key == Key.Enter) Ok_Click(sender, e);
    }
}
