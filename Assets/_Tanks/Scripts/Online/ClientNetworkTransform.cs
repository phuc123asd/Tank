using Unity.Netcode.Components;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Owner-authoritative NetworkTransform: the owning client is the source of truth for its own
    /// transform, so a player can move their own object and everyone else sees it move. Used by the
    /// Phase 2 networking placeholder; the real tanks get their own networked movement in Phase 3.
    ///
    /// NOTE: a MonoBehaviour must live in a file named exactly after its class, otherwise Unity cannot
    /// resolve its script reference and the component shows up as "missing script".
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
