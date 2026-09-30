using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PortSentinel.Application;
using PortSentinel.Contracts;
using PortSentinel.Domain;
using Xunit;

namespace PortSentinel.UnitTests;

public sealed class ProtocolTests
{
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(65537)]
    public async Task Frame_rejects_invalid_sizes_before_allocating(int size)
    {
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, size);
        await Assert.ThrowsAsync<InvalidDataException>(() => PipeProtocol.ReadAsync<Request>(new MemoryStream(prefix), 65536, default));
    }
    [Fact] public async Task Truncated_frame_fails() => await Assert.ThrowsAsync<EndOfStreamException>(() =>
        PipeProtocol.ReadAsync<Request>(new MemoryStream([1, 0, 0, 0]), 65536, default));
    [Fact] public void Client_cannot_send_admin_flag_or_shell_command()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Request>("{\"version\":1,\"operation\":\"Grant\",\"admin\":true}", PipeProtocol.Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Request>("{\"version\":1,\"operation\":\"PowerShell\"}", PipeProtocol.Json));
    }
    [Fact] public async Task Wire_roundtrip_preserves_request()
    {
        var request = new Request(1, Operation.Grant, "ID", "Türkçe cihaz", 3);
        var stream = new MemoryStream(); await PipeProtocol.WriteAsync(stream, request, 65536, default); stream.Position = 0;
        Assert.Equal(request, await PipeProtocol.ReadAsync<Request>(stream, 65536, default));
    }
    [Fact] public void Service_validator_rejects_big_name_bad_version_and_range()
    {
        var validator = new RequestValidator();
        Assert.False(validator.Validate(new Request(2, Operation.Grant, "ID", new string('a', 81))).IsValid);
        Assert.False(validator.Validate(new Request(1, Operation.Settings, ScanIntervalSeconds: 0)).IsValid);
    }
    [Fact] public void Policy_preview_is_scoped_and_non_overlapping_with_file_mask()
    {
        var d = DomainTests.Device(); var record = new RegisteredDevice { Id = Guid.NewGuid(), Identity = d.Identity, IdentityKey = d.Identity.Key, DisplayName = "A&B <USB>" };
        var preview = PolicyPreviewCompiler.Compile([record], [new() { RegisteredDeviceId = record.Id, Allowed = true }]);
        var groups = XDocument.Parse(preview.GroupsXml); var rules = XDocument.Parse(preview.RulesXml);
        Assert.All(groups.Descendants("Group"), g => Assert.Equal("MatchAll", g.Element("MatchType")!.Value));
        Assert.All(rules.Descendants("AccessMask"), m => Assert.Equal("63", m.Value));
        Assert.Single(rules.Descendants("ExcludedIdList").Elements("GroupId"));
        Assert.Equal("A&B <USB>", groups.Descendants("Name").Last().Value);
        Assert.Contains("Windows'a uygulanmadı", preview.Warning);
    }
    [Fact] public void Policy_preview_rejects_ambiguous_allow()
    {
        var d = new RegisteredDevice { Id = Guid.NewGuid(), Identity = DomainTests.Device(unique: false).Identity };
        Assert.Throws<BusinessException>(() => PolicyPreviewCompiler.Compile([d], [new() { RegisteredDeviceId = d.Id, Allowed = true }]));
    }
}
