using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;

public class FixPrefabs
{
    [MenuItem("Tools/Fix Prefab Duplicates")]
    public static void Fix()
    {
        string[] prefabPaths = new string[] {
            "Assets/_Tanks/Game/Prefabs/Tanks/Tank - Medium Variant.prefab",
            "Assets/_Tanks/Game/Prefabs/Tanks/Tank - Shark Variant.prefab",
            "Assets/_Tanks/Game/Prefabs/Tanks/Tank - ATV Variant.prefab",
            "Assets/_Tanks/Game/Prefabs/Tanks/Tank - Heavy Variant.prefab"
        };
        
        foreach(var path in prefabPaths) {
            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path)) {
                var prefabRoot = editingScope.prefabContentsRoot;
                bool changed = false;
                
                // Remove duplicated NetworkTransform
                var nts = prefabRoot.GetComponents<NetworkTransform>();
                if (nts.Length > 1) {
                    for(int i = 1; i < nts.Length; i++) {
                        GameObject.DestroyImmediate(nts[i], true);
                        changed = true;
                    }
                }
                
                // For ATV Variant, remove second NetworkObject
                if (path.Contains("ATV Variant")) {
                    var nos = prefabRoot.GetComponents<NetworkObject>();
                    if (nos.Length > 1) {
                        for(int i = 1; i < nos.Length; i++) {
                            GameObject.DestroyImmediate(nos[i], true);
                            changed = true;
                        }
                    }
                }
                
                if (changed) {
                    Debug.Log($"Fixed duplicates in {path}");
                }
            }
        }
        Debug.Log("Finished fixing prefabs.");
    }
}
