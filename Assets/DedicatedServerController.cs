using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class DedicatedServerController : MonoBehaviour
{
    private void Start()
    {
        // 1. Kiểm tra xem Game đang chạy ở chế độ "Không đồ họa" (Headless Mode)
        // Đây là dấu hiệu nhận biết game đang chạy trên máy chủ đám mây
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.Log("[Dedicated Server] Phát hiện chế độ Headless Mode. Đang khởi chạy Server...");
            
            // Khởi chạy tiến trình cấu hình máy chủ
            ConfigureAndStartServer();
        }
        else
        {
            Debug.Log("[Client] Phát hiện chế độ có đồ họa. Chờ người chơi bấm nút Start Client...");
            // Người chơi sẽ dùng giao diện UI bình thường để gọi StartClient()
        }
    }

    private void ConfigureAndStartServer()
    {
        // Khởi tạo các giá trị mặc định phòng trường hợp chạy thử nghiệm local
        ushort port = 7777; 

        // 2. Phân tích tham số dòng lệnh (Command Line Arguments) truyền từ Unity Cloud
        // Các đối số này do máy chủ đám mây tự động truyền vào khi mở ứng dụng game của bạn
        string[] args = Environment.GetCommandLineArgs();
        
        for (int i = 0; i < args.Length; i++)
        {
            // Tìm tham số "-port" để cấu hình cổng mạng cho người chơi kết nối vào
            if (args[i].ToLower() == "-port" && i + 1 < args.Length)
            {
                if (ushort.TryParse(args[i + 1], out ushort parsedPort))
                {
                    port = parsedPort;
                    Debug.Log($"[Dedicated Server] Tìm thấy tham số Port: {port}");
                }
            }
        }

        // 3. Cấu hình Port vừa đọc được vào hệ thống UnityTransport của Netcode
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Port = port;
            Debug.Log($"[Dedicated Server] Đã gán thành công Port {port} cho UnityTransport.");
        }
        else
        {
            Debug.LogError("[Dedicated Server] Không tìm thấy Component UnityTransport trên NetworkManager!");
            return;
        }

        // 4. Khởi chạy Server Netcode
        NetworkManager.Singleton.StartServer();
        Debug.Log("[Dedicated Server] Máy chủ Netcode đã khởi động thành công và đang lắng nghe kết nối.");
    }
}