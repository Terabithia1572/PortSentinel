using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using PortSentinel.Contracts;

namespace PortSentinel.Desktop;

public sealed class MainViewModel(ISentinelClient client) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private DispatcherTimer? timer;
    private bool busy, connected, admin;
    private SnapshotDto? snapshot;
    private bool disposed;
    private string serviceStatus = "Bağlantı bekleniyor", protectionStatus = "Koruma doğrulanmadı", message = "", name = "", preview = "";
    private DeviceRow? selectedDevice;
    private RegisteredRow? selectedRegistered;
    private int scanSeconds = 10, retentionDays = 30;
    public ObservableCollection<DeviceRow> Devices { get; } = [];
    public ObservableCollection<RegisteredRow> Registered { get; } = [];
    public ObservableCollection<LocalEvent> Events { get; } = [];
    public ObservableCollection<LocalAudit> Audit { get; } = [];
    public ObservableCollection<RevisionDto> Revisions { get; } = [];
    public bool Busy { get => busy; private set { Set(ref busy, value); UpdateCommands(); Raise(nameof(Activity)); } }
    public bool Connected { get => connected; private set { Set(ref connected, value); Raise(nameof(CanManage)); UpdateCommands(); } }
    public bool CanManage => Connected && admin && !Busy;
    public string Activity => Busy ? "İşlem sürüyor…" : "";
    public string ServiceStatus { get => serviceStatus; private set => Set(ref serviceStatus, value); }
    public string ProtectionStatus { get => protectionStatus; private set => Set(ref protectionStatus, value); }
    public string Message { get => message; private set => Set(ref message, value); }
    public string DeviceName { get => name; set => Set(ref name, value); }
    public string Preview { get => preview; private set => Set(ref preview, value); }
    public int ScanSeconds { get => scanSeconds; set => Set(ref scanSeconds, value); }
    public int RetentionDays { get => retentionDays; set => Set(ref retentionDays, value); }
    public string Role => admin ? "Yönetici token'ı doğrulandı" : "Salt görüntüleme · yönetmek için yönetici olarak açın";
    public string RevisionSummary => $"İstenen revizyon: {snapshot?.DesiredRevision ?? 0}  ·  Etkin revizyon: doğrulanmadı";
    public string InventorySummary => !Connected ? "Servis bağlantısı yok; cihaz listesi güncel kabul edilmez."
        : snapshot?.InventoryError ?? $"Son tarama: {snapshot?.InventoryUtc?.ToLocalTime():dd.MM.yyyy HH:mm:ss}";
    public string Diagnostics => snapshot == null ? "Servise bağlanınca tanılama bilgileri burada gösterilir." : string.Join(Environment.NewLine + Environment.NewLine, snapshot.Backend.Diagnostics);
    public string DeviceDetails => SelectedDevice == null ? "Kimlik ve volume bilgilerini görmek için cihaz seçin." :
        $"Fiziksel düğüm: {SelectedDevice.Data.Identity.InstanceId}\nSeri: {SelectedDevice.Data.Identity.Serial}\nVID/PID: {SelectedDevice.Data.Identity.Vid}/{SelectedDevice.Data.Identity.Pid}\n" +
        $"Kimlik: {SelectedDevice.Quality}\nDiskler: {string.Join("\n", SelectedDevice.Data.Disks)}\n" +
        $"Bölümler/volume: {string.Join("; ", SelectedDevice.Data.Volumes.Select(v => v.Partition + " → " + (v.DriveLetter ?? "harf yok")))}\nGerekçe: {SelectedDevice.Data.Reason}";
    public DeviceRow? SelectedDevice { get => selectedDevice; set { if (Set(ref selectedDevice, value)) { if (value != null) DeviceName = value.Name; Raise(nameof(DeviceDetails)); UpdateCommands(); } } }
    public RegisteredRow? SelectedRegistered { get => selectedRegistered; set { if (Set(ref selectedRegistered, value)) { if (value != null) DeviceName = value.Name; UpdateCommands(); } } }
    public AsyncCommand RefreshCommand { get; private set; } = null!;
    public AsyncCommand ReconcileCommand { get; private set; } = null!;
    public AsyncCommand GrantCommand { get; private set; } = null!;
    public AsyncCommand RevokeCommand { get; private set; } = null!;
    public AsyncCommand RenameCommand { get; private set; } = null!;
    public AsyncCommand SettingsCommand { get; private set; } = null!;
    public AsyncCommand PreviewCommand { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        RefreshCommand = new(() => RunAsync(new(1, Operation.Snapshot)), () => !Busy);
        ReconcileCommand = new(() => RunAsync(new(1, Operation.Reconcile)), () => CanManage);
        GrantCommand = new(() => RunAsync(new(1, Operation.Grant, SelectedDevice!.Data.Id, DeviceName, snapshot!.DesiredRevision)),
            () => CanManage && SelectedDevice?.Data.CanGrant == true);
        RevokeCommand = new(() => RunAsync(new(1, Operation.Revoke, SelectedRegistered!.Data.Id, ExpectedRevision: snapshot!.DesiredRevision)),
            () => CanManage && SelectedRegistered?.Data.Allowed == true);
        RenameCommand = new(() => RunAsync(new(1, Operation.Rename, SelectedRegistered!.Data.Id, DeviceName)), () => CanManage && SelectedRegistered != null);
        SettingsCommand = new(() => RunAsync(new(1, Operation.Settings, ScanIntervalSeconds: ScanSeconds, RetentionDays: RetentionDays)), () => CanManage);
        PreviewCommand = new(() => RunAsync(new(1, Operation.PreviewPolicy)), () => CanManage);
        foreach (var property in new[] { nameof(RefreshCommand), nameof(ReconcileCommand), nameof(GrantCommand), nameof(RevokeCommand), nameof(RenameCommand), nameof(SettingsCommand), nameof(PreviewCommand) }) Raise(property);
        await RunAsync(new(1, Operation.Snapshot));
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += async (_, _) => { if (!Busy) await RunAsync(new(1, Operation.Snapshot), silent: true); };
        timer.Start();
    }
    private async Task RunAsync(Request request, bool silent = false)
    {
        if (Busy) return;
        Busy = true;
        try
        {
            var response = await client.SendAsync(request, lifetime.Token);
            Connected = true; ServiceStatus = "Servis bağlı · IPC doğrulandı";
            if (!response.Success) { Message = $"{response.Error?.Message}  [{response.Error?.Code} · {response.CorrelationId}]"; return; }
            if (response.Snapshot != null) Apply(response.Snapshot);
            if (response.Preview != null) { Preview = response.Preview.Warning + "\n\n" + response.Preview.GroupsXml + "\n\n" + response.Preview.RulesXml; Message = "XML önizlemesi hazır; Windows'a uygulanmadı."; }
            if (response.Command != null)
            {
                Message = response.Command.Message;
                var refresh = await client.SendAsync(new(1, Operation.Snapshot), lifetime.Token);
                if (refresh.Success && refresh.Snapshot != null) Apply(refresh.Snapshot);
                else throw new IOException("İşlem sonrası güncel politika okunamadı.");
            }
            else if (!silent && response.Preview == null) Message = "Bilgiler güncellendi. Fiziksel erişim engeli doğrulanmış değil.";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Connected = false; admin = false; ServiceStatus = "Servise erişilemiyor"; ProtectionStatus = "Güncel koruma durumu bilinmiyor";
            Message = "Servis bağlantısı kesildi: " + ex.Message;
            Raise(nameof(Role)); Raise(nameof(InventorySummary));
        }
        finally { Busy = false; Raise(nameof(CanManage)); }
    }
    private void Apply(SnapshotDto value)
    {
        snapshot = value; admin = value.IsAdministrator;
        ProtectionStatus = value.Backend.Status;
        var deviceId = SelectedDevice?.Data.Id; var registeredId = SelectedRegistered?.Data.Id; var draftName = DeviceName;
        Replace(Devices, value.Devices.Select(d => new DeviceRow(d)));
        Replace(Registered, value.Registered.Select(d => new RegisteredRow(d)));
        SelectedDevice = Devices.FirstOrDefault(d => d.Data.Id == deviceId);
        SelectedRegistered = Registered.FirstOrDefault(d => d.Data.Id == registeredId);
        DeviceName = draftName;
        Replace(Events, value.Events.Select(e => new LocalEvent(e.OccurredUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), e.Kind, e.Device, e.Details)));
        Replace(Audit, value.Audit.Select(a => new LocalAudit(a.OccurredUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), a.ActorSid, a.Operation, a.Outcome, a.Details, a.CorrelationId)));
        Replace(Revisions, value.Revisions);
        ScanSeconds = value.Settings.ScanIntervalSeconds; RetentionDays = value.Settings.RetentionDays;
        foreach (var p in new[] { nameof(Role), nameof(CanManage), nameof(RevisionSummary), nameof(InventorySummary), nameof(Diagnostics), nameof(DeviceDetails) }) Raise(p);
    }
    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values) { collection.Clear(); foreach (var value in values) collection.Add(value); }
    private void UpdateCommands()
    { foreach (var command in new[] { RefreshCommand, ReconcileCommand, GrantCommand, RevokeCommand, RenameCommand, SettingsCommand, PreviewCommand }) command?.Refresh(); Raise(nameof(CanManage)); }
    public void Dispose() { if (disposed) return; disposed = true; timer?.Stop(); lifetime.Cancel(); lifetime.Dispose(); }
}
public sealed record DeviceRow(DeviceDto Data)
{
    public string Name => Data.Name;
    public string Manufacturer => Data.Manufacturer;
    public string Capacity => Data.SizeBytes > 0 ? $"{Data.SizeBytes / 1073741824.0:N1} GiB" : "Bilinmiyor";
    public string Transport => Data.Transport;
    public string Desired => State(Data.DesiredState);
    public string Effective => State(Data.EffectiveState);
    public string Quality => Data.Identity.Quality == "StableSerial" ? "Seri + VID/PID (taklit edilebilir)" : "Belirsiz / port bağımlı";
    public static string State(string value) => value switch { "Allowed" => "İzinli (istenen)", "Blocked" => "Engelli (istenen)", "AmbiguousIdentity" => "Kimlik belirsiz", "Applying" => "Politika uygulanıyor", "OutOfScope" => "Kapsam dışı", _ => "Doğrulanamadı" };
}
public sealed record RegisteredRow(RegisteredDto Data)
{
    public string Name => Data.Name;
    public string Serial => Data.Identity.Serial;
    public string VidPid => Data.Identity.Vid + "/" + Data.Identity.Pid;
    public string Allowed => Data.Allowed ? "Açık izin (istenen)" : "İzin iptal edildi";
    public string Connected => Data.Connected ? "Bağlı" : "Bağlı değil / doğrulanmadı";
}
public sealed record LocalEvent(string Time, string Kind, string Device, string Details);
public sealed record LocalAudit(string Time, string Actor, string Operation, string Outcome, string Details, string Correlation);
