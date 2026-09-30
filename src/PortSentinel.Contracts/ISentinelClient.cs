namespace PortSentinel.Contracts;
public interface ISentinelClient { Task<Response> SendAsync(Request request, CancellationToken ct); }
