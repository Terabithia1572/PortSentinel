using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PortSentinel.Contracts;
using PortSentinel.Infrastructure.Ipc;

namespace PortSentinel.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var development = e.Args.Contains("--development");
        var collection = new ServiceCollection();
        collection.AddSingleton<ISentinelClient>(new PipeSentinelClient(development ? "PortSentinel.Development.v1" : PipeProtocol.ProductionPipe, development));
        collection.AddSingleton<MainViewModel>(); collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider();
        TextWriterTraceListener? bindingListener = null;
        if (e.Args.Contains("--smoke-test"))
        {
            Directory.CreateDirectory("artifacts");
            bindingListener = new TextWriterTraceListener(Path.GetFullPath("artifacts/desktop-bindings.log"));
            PresentationTraceSources.DataBindingSource.Listeners.Add(bindingListener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        }
        var window = services.GetRequiredService<MainWindow>();
        if (development) window.Title += " — Geliştirme (erişim engeli yok)";
        window.Loaded += async (_, _) =>
        {
            var model = services.GetRequiredService<MainViewModel>();
            await model.InitializeAsync();
            if (e.Args.Contains("--smoke-test"))
            {
                var screens = (TabControl)window.FindName("Screens");
                for (var index = 0; index < screens.Items.Count; index++)
                {
                    screens.SelectedIndex = index;
                    await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ContextIdle);
                    var root = (FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.GetFullPath($"artifacts/desktop-screen-{index}.png")); encoder.Save(file);
                }
                bindingListener?.Flush();
                File.WriteAllText(Path.GetFullPath("artifacts/desktop-smoke.txt"), $"Connected={model.Connected}; Service={model.ServiceStatus}; Protection={model.ProtectionStatus}\nDevices={model.Devices.Count}\n{model.Diagnostics}");
                Shutdown(model.Connected ? 0 : 1);
            }
        };
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { services?.Dispose(); base.OnExit(e); }
}
