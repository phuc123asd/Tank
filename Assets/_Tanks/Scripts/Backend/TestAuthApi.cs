using Unity.Services.Authentication;
using UnityEngine;
public class TestAuthApi : MonoBehaviour {
    void Start() {
        AuthenticationService.Instance.ClearSessionToken();
    }
}
