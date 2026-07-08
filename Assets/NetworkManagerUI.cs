using Unity.Netcode;
using UnityEngine;

public class NetworkManagerUI : MonoBehaviour
{
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        
        // Nếu game chưa bắt đầu kết nối mạng nào
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            // Bấm nút này để làm Host (Vừa là máy chủ vừa là người chơi)
            if (GUILayout.Button("Start Host (Chủ phòng)"))
            {
                NetworkManager.Singleton.StartHost();
            }

            // Bấm nút này để làm Client (Người tham gia)
            if (GUILayout.Button("Start Client (Khách)"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            // Hiển thị trạng thái hiện tại
            string mode = NetworkManager.Singleton.IsHost ? "Host" : "Client";
            GUILayout.Label($"Đang chạy dưới quyền: {mode}");

            // Nút ngắt kết nối
            if (GUILayout.Button("Disconnect (Thoát)"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}