using FirebaseAdmin;
using FirebaseAdmin.Messaging;
namespace EquipManagementAPI.Services
{
    public class FcmService
    {
        public async Task SendAsync(string deviceToken, string title, string body,
        Dictionary<string, string>? data = null)
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                Console.WriteLine("[FCM] Firebase chưa được khởi tạo, bỏ qua notification.");
                return;
            }
            try
            {
                var message = new Message
                {
                    Token = deviceToken,
                    Notification = new Notification { Title = title, Body = body },
                    Data = data,
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            Sound = "default",
                            ChannelId = "default-channel-id",
                        }
                    },
                    Apns = new ApnsConfig
                    {
                        Aps = new Aps { Sound = "default" }
                    }
                };
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FCM] Lỗi gửi notification: {ex.Message}");
            }
        }
    }
}
