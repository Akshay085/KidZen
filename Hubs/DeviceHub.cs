using Microsoft.AspNetCore.SignalR;

namespace KidZen.API.Hubs;

public class DeviceHub : Hub
{
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(ILogger<DeviceHub> logger)
    {
        _logger = logger;
    }

    // Child device registers to its own group
    public async Task JoinDeviceGroup(int deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
        _logger.LogInformation("Connection {ConnectionId} joined device group device_{DeviceId}", Context.ConnectionId, deviceId);
    }

    // Parent subscribes to child updates
    public async Task JoinParentGroup(int childId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"parent_child_{childId}");
        _logger.LogInformation("Parent {ConnectionId} joined parent group parent_child_{ChildId}", Context.ConnectionId, childId);
    }

    // Child sends request for more screen time to parent
    public async Task RequestExtraTime(int deviceId, string packageName, int requestedMinutes)
    {
        _logger.LogInformation("Extra time requested for Device {DeviceId}, App: {PackageName}, Minutes: {Minutes}", 
            deviceId, packageName, requestedMinutes);
        
        await Clients.Group($"device_{deviceId}").SendAsync("OnExtraTimeRequested", new
        {
            deviceId,
            packageName,
            requestedMinutes,
            timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
