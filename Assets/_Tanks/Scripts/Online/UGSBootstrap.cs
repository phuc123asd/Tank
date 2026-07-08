using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Persistent bootstrap for Unity Gaming Services (UGS). Initializes the services SDK and provides the
    /// three sign-in paths the login screen uses: register (username/password), login (username/password)
    /// and guest (anonymous). It no longer auto-signs-in on Awake — that would consume the session so the
    /// login screen could not choose an account — it only initializes; the UI drives the actual sign-in.
    ///
    /// Survives scene loads (DontDestroyOnLoad) so the identity is reused by the Lobby/Relay layers.
    ///
    /// NOTE: UGS requires the project to be linked to a Unity Cloud project ID
    /// (Edit > Project Settings > Services). Username/Password also requires the "Username and Password"
    /// sign-in method to be enabled in the Unity Cloud Authentication dashboard.
    /// </summary>
    public class UGSBootstrap : MonoBehaviour
    {
        public static UGSBootstrap Instance { get; private set; }

        public enum Status { NotStarted, Initializing, SigningIn, SignedIn, Failed }

        public Status CurrentStatus { get; private set; } = Status.NotStarted;
        public string PlayerId { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public string Profile { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        // Raised on the main thread whenever CurrentStatus changes.
        public event Action<Status> OnStatusChanged;

        public bool IsSignedIn => CurrentStatus == Status.SignedIn;
        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;

        /// <summary>
        /// Returns the singleton, creating a bootstrap GameObject if a scene needs UGS but none exists.
        /// </summary>
        public static UGSBootstrap Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("UGSBootstrap");
                Instance = go.AddComponent<UGSBootstrap>();
            }

            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize only — the login screen decides how to sign in.
            _ = InitializeAsync();
        }

        /// <summary>Idempotent: initialize the UGS SDK (no sign-in).</summary>
        public async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            try
            {
                SetStatus(Status.Initializing);

                // Distinct profile per process so two copies on ONE machine keep separate session caches.
                Profile = ResolveProfile();

                var options = new InitializationOptions();
                options.SetProfile(Profile);
                await UnityServices.InitializeAsync(options);

                SetStatus(AuthenticationService.Instance.IsSignedIn ? Status.SignedIn : Status.NotStarted);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                SetStatus(Status.Failed);
                Debug.LogError($"[UGS] Initialize failed: {e}");
            }
        }

        /// <summary>Register a new account with username + password, then sign in.</summary>
        public Task<bool> RegisterAsync(string username, string password)
        {
            return RunAuthAsync(
                () => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password),
                username, $"Đăng ký thành công: {username}");
        }

        /// <summary>Sign in to an existing username + password account.</summary>
        public Task<bool> LoginAsync(string username, string password)
        {
            return RunAuthAsync(
                () => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password),
                username, $"Đăng nhập: {username}");
        }

        /// <summary>Anonymous "play as guest" sign-in.</summary>
        public Task<bool> SignInAsGuestAsync()
        {
            return RunAuthAsync(
                () => AuthenticationService.Instance.SignInAnonymouslyAsync(),
                "Khách", "Đăng nhập khách");
        }

        // Backwards-compatible fallback used by OnlineGameConnector: ensure we are signed in somehow
        // (as guest) if the player reached the online flow without logging in.
        public async Task InitializeAndSignInAsync()
        {
            if (IsSignedIn)
                return;

            await SignInAsGuestAsync();
        }

        private async Task<bool> RunAuthAsync(Func<Task> authCall, string displayName, string successLog)
        {
            try
            {
                await InitializeAsync();

                // Start from a clean slate so switching accounts / guest->account works.
                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut();

                SetStatus(Status.SigningIn);
                await authCall();

                PlayerId = AuthenticationService.Instance.PlayerId;
                Username = displayName;
                SetStatus(Status.SignedIn);
                Debug.Log($"[UGS] {successLog}. PlayerId = {PlayerId}");
                return true;
            }
            catch (Exception e)
            {
                LastError = FriendlyError(e);
                SetStatus(Status.Failed);
                Debug.LogError($"[UGS] Auth failed: {e}");
                return false;
            }
        }

        private void SetStatus(Status status)
        {
            CurrentStatus = status;
            OnStatusChanged?.Invoke(status);
        }

        // Turn common UGS auth exceptions into short Vietnamese hints for the login screen.
        private static string FriendlyError(Exception e)
        {
            string m = e.Message ?? string.Empty;
            if (m.Contains("already exists") || m.Contains("USERNAME_ALREADY_EXISTS"))
                return "Tên đăng nhập đã tồn tại.";
            if (m.Contains("Invalid username or password") || m.Contains("WRONG_USERNAME_PASSWORD"))
                return "Sai tên đăng nhập hoặc mật khẩu.";
            if (m.Contains("password"))
                return "Mật khẩu phải 8-30 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
            if (m.Contains("username"))
                return "Tên đăng nhập 3-20 ký tự (chữ, số, . _ -).";
            return m;
        }

        // Pick the UGS profile: a "-profile <name>" command-line arg if present, otherwise a unique
        // per-process name so each running copy keeps a distinct session cache. Profile must match [A-Za-z0-9_-].
        private static string ResolveProfile()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-profile" && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return Sanitize(args[i + 1]);
            }

            return "p" + System.Guid.NewGuid().ToString("N").Substring(0, 10);
        }

        private static string Sanitize(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                if (sb.Length >= 30)
                    break;
            }

            return sb.Length > 0 ? sb.ToString() : "player";
        }

        /// <summary>Human-readable status string for menus.</summary>
        public string StatusLabel()
        {
            switch (CurrentStatus)
            {
                case Status.NotStarted:   return "Chưa đăng nhập";
                case Status.Initializing: return "Đang khởi tạo dịch vụ...";
                case Status.SigningIn:    return "Đang đăng nhập...";
                case Status.SignedIn:     return $"Đã đăng nhập: {Username}";
                case Status.Failed:       return $"Lỗi: {LastError}";
                default:                  return string.Empty;
            }
        }
    }
}
