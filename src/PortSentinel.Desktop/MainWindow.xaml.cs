using System.Diagnostics;
using System.Windows;

namespace PortSentinel.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel model) { InitializeComponent(); DataContext = model; Closed += (_, _) => model.Dispose(); }
    private void ElevateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            start.WorkingDirectory = Environment.CurrentDirectory;
            if (Environment.GetCommandLineArgs().Contains("--development")) start.ArgumentList.Add("--development");
            Process.Start(start); Close();
        }
        catch (System.ComponentModel.Win32Exception ex) { MessageBox.Show("Yönetici oturumu açılamadı: " + ex.Message, "PortSentinel"); }
    }
}
