using System.Text.Json;
using Microsoft.Extensions.Logging;
using PortSentinel.Application;
using PortSentinel.Domain;

namespace PortSentinel.Infrastructure.Windows;

public sealed class DefenderAssessmentBackend(FixedPowerShell powershell, ILogger<DefenderAssessmentBackend> logger) : IPolicyBackend
{
    private BackendAssessment? cached;
    private DateTime expires;
    public async Task<BackendAssessment> AssessAsync(CancellationToken ct)
    {
        if (cached != null && DateTime.UtcNow < expires) return cached;
        IReadOnlyList<string> diagnostics;
        try { diagnostics = JsonSerializer.Deserialize<string[]>(await powershell.ExecuteResourceAsync("Diagnostics", ct)) ?? []; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Windows capability assessment failed"); diagnostics = ["Windows yetenek kontrolü başarısız; destek ve çatışma durumu doğrulanamadı."]; }
        cached = new("Microsoft Defender Device Control — değerlendirme", false,
            "Koruma doğrulanmadı. Bu sürüm Windows erişim politikalarını uygulamaz.", diagnostics);
        expires = DateTime.UtcNow.AddSeconds(30); return cached;
    }
    public async Task<PolicyApplyResult> ApplyAsync(PolicyRevision revision, CancellationToken ct)
    {
        var assessment = await AssessAsync(ct);
        return new(RevisionState.Unverified, $"Revizyon {revision.Id}: istenen politika kaydedildi. {assessment.Status} Lisanslı motor ve donanım doğrulaması gerekir.");
    }
}
