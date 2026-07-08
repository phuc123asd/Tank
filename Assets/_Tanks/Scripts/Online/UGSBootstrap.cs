using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Persistent bootstrap for Unity Gaming Services (UGS). Initializes the services SDK and signs
    /// the player in anonymously (Phase 1). Username/password and Google sign-in are added later.
    /// It survives scene loads (DontDestroyOnLoad) so the sign-in happens once per game session and
    /// the same player identity is reused by the Lobby/Relay layers.
    ///
    /// NOTE: UGS requires the project to be linked to a Unity Cloud project ID
    /// (Edit > Project Settings > Services). Without that link InitializeAsync throws and the status
    /// becomes Failed.
    /// </summary>
    public class UGSBootstrap : MonoBehaviour
    {
        public static UGSBootstrap Instance { get; private set; }

        public enum Status { NotStarted, Initializing, SigningIn, SignedIn, Failed }

        public Status CurrentStatus { get; private set; } = Status.NotStarted;
        public string PlayerId { get; private set; } = string.Empty;
        public string Profile { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        // Raised on the main thread whenever CurrentStatus changes.
        public event Action<Status> OnStatusChanged;

        public bool IsSignedIn => CurrentStatus == Status.SignedIn;

        /// <summary>
        /// Returns the singleton, creating a bootstrap GameObject if a scene needs UGS but none exists.
        /// Mirrors the "self-contained, create-what-you-need" style used by the menus in this project.
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
            // Enforce a single persistent instance.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Kick off sign-in without blocking. Fire-and-forget is fine: status is exposed for the UI.
            _ = InitializeAndSignInAsync();
        }

        /// <summary>
        /// Idempotent: safe to call multiple times. Initializes UGS and signs in anonymously.
        /// </summary>
        public async Task InitializeAndSignInAsync()
        {
            if (CurrentStatus == Status.Initializing
                || CurrentStatus == Status.SigningIn
                || CurrentStatus == Status.SignedIn)
            {
                return;
            }

            try
            {
                SetStatus(Status.Initializing);
                // Use a distinct profile per process so two copies of the game on ONE machine get
                // DIFFERENT player identities (they share persistentDataPath, so without this they'd
                // restore the same cached anonymous account and collide: "player is already a member").
                // A "-profile <name>" command-line arg pins it; otherwise a unique one is generated.
                Profile = ResolveProfile();

                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    options.SetProfile(Profile);
                    await UnityServices.InitializeAsync(options);
                }

                SetStatus(Status.SigningIn);
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                PlayerId = AuthenticationService.Instance.PlayerId;
                SetStatus(Status.SignedIn);
                Debug.Log($"[UGS] Signed in anonymously. Profile = {Profile}, PlayerId = {PlayerId}");
            }
            catch (Exception e)
            {
                LastError = e.Message;
                SetStatus(Status.Failed);
                Debug.LogError($"[UGS] Sign-in failed: {e}");
            }
        }

        private void SetStatus(Status status)
        {
            CurrentStatus = status;
            OnStatusChanged?.Invoke(status);
        }

        // Pick the UGS profile: a "-profile <name>" command-line arg if present, otherwise a unique
        // per-process name so each running copy is a distinct player. Profile must match [A-Za-z0-9_-].
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
                case Status.NotStarted:  return "Chua khoi dong";
                case Status.Initializing: return "Dang khoi tao dich vu...";
                case Status.SigningIn:   return "Dang dang nhap...";
                case Status.SignedIn:    return $"Da dang nhap ({Profile})\nID: {PlayerId}";
                case Status.Failed:      return $"Dang nhap that bai:\n{LastError}";
                default:                 return string.Empty;
            }
        }
    }
}
