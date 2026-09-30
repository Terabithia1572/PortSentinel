using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace PortSentinel.Infrastructure.Windows;

public sealed class FixedPowerShell
{
    public async Task<string> ExecuteResourceAsync(string resource, CancellationToken ct)
    {
        if (resource is not ("Inventory" or "Diagnostics")) throw new ArgumentOutOfRangeException(nameof(resource));
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream($"PortSentinel.Infrastructure.Windows.{resource}.ps1")
            ?? throw new InvalidOperationException("Gömülü tanılama betiği bulunamadı.");
        using var reader = new StreamReader(input);
        var script = await reader.ReadToEndAsync(ct);
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows tanılama süreci başlatılamadı.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout; var error = await stderr;
            if (process.ExitCode != 0) throw new InvalidOperationException("Windows tanılaması başarısız: " + error[..Math.Min(error.Length, 1000)]);
            if (output.Length > 2_000_000) throw new InvalidDataException("Envanter yanıtı çok büyük.");
            return output;
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
    }
}
