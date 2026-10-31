using Microsoft.AspNetCore.SignalR;

namespace CineBook.Web.Hubs;

// Mỗi suất chiếu là một group. Ai đang mở sơ đồ ghế của suất đó sẽ nhận "SeatsChanged"
// ngay khi có người chọn / bỏ / giữ ghế, rồi tự tải lại sơ đồ.
public class SeatHub : Hub
{
    public const string Path = "/hubs/seats";

    public Task JoinShowtime(Guid showtimeId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(showtimeId));

    public static string GroupName(Guid showtimeId) => "showtime:" + showtimeId;
}

public static class SeatHubExtensions
{
    public static Task NotifySeatsChanged(this IHubContext<SeatHub> hub, Guid showtimeId) =>
        hub.Clients.Group(SeatHub.GroupName(showtimeId)).SendAsync("SeatsChanged", showtimeId);
}
