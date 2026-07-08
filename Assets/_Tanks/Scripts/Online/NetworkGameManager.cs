using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// The online equivalent of the offline <see cref="GameManager"/> round loop (Slice 4). Lives as a
    /// scene NetworkObject in Online_Arena, so NGO spawns one on every machine; only the server runs the
    /// authoritative round loop (revive → play → decide winner → score → repeat). Round/win messages are
    /// pushed to every client via a ClientRpc onto a small self-built HUD.
    ///
    /// Tanks are discovered with FindObjectsByType each round (rather than a registry) to stay robust to
    /// spawn/scene-load ordering. Respawn is owner-authoritative: the server tells each tank to reset, and
    /// each owner repositions itself (see NetworkTankHealth.ServerRoundReset / NetworkTankSetup).
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Tooltip("Rounds a player must win to take the game.")]
        public int m_NumRoundsToWin = 3;
        public float m_StartDelay = 2.5f;
        public float m_EndDelay = 3f;

        private readonly Dictionary<ulong, int> m_Scores = new Dictionary<ulong, int>();
        private List<NetworkTankHealth> m_RoundTanks = new List<NetworkTankHealth>();
        private TextMeshProUGUI m_MessageText;
        private int m_RoundNumber;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            BuildHud();

            if (IsServer)
                StartCoroutine(ServerWaitThenLoop());
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------------- Server round loop ----------------

        private IEnumerator ServerWaitThenLoop()
        {
            // Wait until at least two tanks are present (two players joined).
            while (CountTanks() < 2)
                yield return new WaitForSeconds(0.5f);

            yield return ServerGameLoop();
        }

        private IEnumerator ServerGameLoop()
        {
            while (true)
            {
                yield return ServerRoundStarting();
                yield return ServerRoundPlaying();
                yield return ServerRoundEnding();

                // Endless match: once someone takes the game, wipe the board and start a fresh game.
                if (GameWinner(out _))
                {
                    m_Scores.Clear();
                    m_RoundNumber = 0;
                }
            }
        }

        private IEnumerator ServerRoundStarting()
        {
            m_RoundNumber++;
            m_RoundTanks = GatherTanks();

            // Revive everyone to full health at their spawn point, frozen.
            foreach (var t in m_RoundTanks)
                if (t != null)
                    t.ServerRoundReset();

            MessageClientRpc($"VÒNG {m_RoundNumber}");
            yield return new WaitForSeconds(m_StartDelay);
        }

        private IEnumerator ServerRoundPlaying()
        {
            MessageClientRpc(string.Empty);

            // Unfreeze — let players drive.
            foreach (var t in m_RoundTanks)
                if (t != null)
                    t.ServerSetControl(true);

            // Round runs until one (or zero) tanks remain.
            while (AliveCount() > 1)
                yield return null;
        }

        private IEnumerator ServerRoundEnding()
        {
            // Freeze survivors.
            foreach (var t in m_RoundTanks)
                if (t != null)
                    t.ServerSetControl(false);

            var winner = LastAlive();
            string message;
            if (winner != null)
            {
                ulong id = winner.OwnerClientId;
                m_Scores.TryGetValue(id, out int s);
                m_Scores[id] = s + 1;
                message = $"{PlayerName(id)} THẮNG VÒNG!";
            }
            else
            {
                message = "HÒA!";
            }

            if (GameWinner(out ulong gameWinnerId))
                message = $"{PlayerName(gameWinnerId)} THẮNG CHUNG CUỘC!";

            MessageClientRpc(message + "\n\n" + ScoreLine());
            yield return new WaitForSeconds(m_EndDelay);
        }

        // ---------------- Helpers ----------------

        private List<NetworkTankHealth> GatherTanks()
        {
            var list = new List<NetworkTankHealth>();
            foreach (var h in FindObjectsByType<NetworkTankHealth>(FindObjectsSortMode.None))
                list.Add(h);
            return list;
        }

        private int CountTanks() => GatherTanks().Count;

        private int AliveCount()
        {
            int c = 0;
            foreach (var t in m_RoundTanks)
                if (t != null && !t.IsDead)
                    c++;
            return c;
        }

        private NetworkTankHealth LastAlive()
        {
            foreach (var t in m_RoundTanks)
                if (t != null && !t.IsDead)
                    return t;
            return null;
        }

        private bool GameWinner(out ulong id)
        {
            id = 0;
            foreach (var kv in m_Scores)
            {
                if (kv.Value >= m_NumRoundsToWin)
                {
                    id = kv.Key;
                    return true;
                }
            }
            return false;
        }

        private static string PlayerName(ulong ownerId) => $"NGƯỜI CHƠI {ownerId + 1}";

        private string ScoreLine()
        {
            var sb = new StringBuilder();
            foreach (var kv in m_Scores)
                sb.Append($"{PlayerName(kv.Key)}: {kv.Value}   ");
            return sb.ToString().TrimEnd();
        }

        [ClientRpc]
        private void MessageClientRpc(string message)
        {
            if (m_MessageText != null)
                m_MessageText.text = message;
        }

        // Build a simple centred message HUD locally on each client.
        private void BuildHud()
        {
            var canvasGo = new GameObject("OnlineHUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("Message");
            textGo.transform.SetParent(canvasGo.transform, false);

            m_MessageText = textGo.AddComponent<TextMeshProUGUI>();
            m_MessageText.alignment = TextAlignmentOptions.Center;
            m_MessageText.fontSize = 72;
            m_MessageText.fontStyle = FontStyles.Bold;
            m_MessageText.color = Color.white;
            m_MessageText.enableWordWrapping = true;
            m_MessageText.text = string.Empty;

            var rt = m_MessageText.rectTransform;
            rt.anchorMin = new Vector2(0.1f, 0.55f);
            rt.anchorMax = new Vector2(0.9f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
