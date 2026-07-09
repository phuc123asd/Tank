using UnityEngine;
using UnityEditor;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class TestAuthEditor : MonoBehaviour
{
    [MenuItem("Tools/Test Login vphuc6899")]
    public static async void RunTest()
    {
        try {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            Debug.Log("Init success.");
            AuthenticationService.Instance.ClearSessionToken();
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync("vphuc6899@gmail.com", "123@phucXo");
            Debug.Log("LOGIN SUCCESS! PlayerID: " + AuthenticationService.Instance.PlayerId);
        } catch (System.Exception e) {
            Debug.LogError("LOGIN FAILED: " + e.Message);
        }
    }
}
