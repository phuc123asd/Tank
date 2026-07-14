using System;
using System.Collections.Generic;
using System.Linq;
using Tanks.Complete;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Tanks.EditorTools
{
    /// <summary>
    /// Prevents a playable map from reaching a build with an incomplete online,
    /// spawn, navigation, or physics contract.
    /// </summary>
    public static class GameplayMapContractValidator
    {
        private static readonly string[] k_MapScenePaths =
        {
            "Assets/_Tanks/Scenes/Desert.unity",
            "Assets/_Tanks/Scenes/Jungle.unity",
            "Assets/_Tanks/Scenes/Moon.unity"
        };

        private static readonly Vector3 k_DefaultGravity = new Vector3(0f, -9.81f, 0f);
        private static readonly Vector3 k_MoonGravity = new Vector3(0f, -4f, 0f);

        [MenuItem("Tanks/Validate Gameplay Maps")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("[MapValidator] Desert, Jungle và Moon đều hợp lệ.");
        }

        public static void ValidateOrThrow()
        {
            EnsureOpenScenesAreSaved();

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var errors = new List<string>();
            var hashes = new HashSet<uint>();

            try
            {
                ValidateBuildSettings(errors);
                foreach (string scenePath in k_MapScenePaths)
                    ValidateScene(scenePath, hashes, errors);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            if (errors.Count > 0)
                throw new BuildFailedException("Gameplay map validation failed:\n- " + string.Join("\n- ", errors));
        }

        private static void EnsureOpenScenesAreSaved()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    throw new BuildFailedException(
                        $"Hãy lưu scene '{scene.name}' trước khi kiểm tra gameplay maps.");
                }
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            var enabledScenes = new HashSet<string>(
                EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));

            foreach (string scenePath in k_MapScenePaths)
            {
                if (!enabledScenes.Contains(scenePath))
                    errors.Add($"{scenePath} chưa được bật trong Build Settings.");
            }
        }

        private static void ValidateScene(string scenePath, HashSet<uint> hashes, List<string> errors)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string prefix = scene.name + ": ";

            GameManager[] managers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameManager>(true))
                .ToArray();

            if (managers.Length != 1)
            {
                errors.Add(prefix + $"cần đúng 1 GameManager, hiện có {managers.Length}.");
                return;
            }

            GameManager manager = managers[0];
            ValidateNetworkObject(manager, hashes, prefix, errors);
            ValidatePhysicsProfile(manager, scene.name, prefix, errors);
            ValidateManagerReferences(manager, prefix, errors);
        }

        private static void ValidateNetworkObject(
            GameManager manager,
            HashSet<uint> hashes,
            string prefix,
            List<string> errors)
        {
            NetworkObject[] networkObjects = manager.GetComponents<NetworkObject>();
            if (networkObjects.Length != 1)
            {
                errors.Add(prefix + $"GameManager cần đúng 1 NetworkObject, hiện có {networkObjects.Length}.");
                return;
            }

            var serialized = new SerializedObject(networkObjects[0]);
            SerializedProperty hashProperty = serialized.FindProperty("GlobalObjectIdHash");
            uint hash = hashProperty != null ? hashProperty.uintValue : 0u;
            if (hash == 0u)
                errors.Add(prefix + "NetworkObject của GameManager chưa có GlobalObjectIdHash hợp lệ.");
            else if (!hashes.Add(hash))
                errors.Add(prefix + $"GlobalObjectIdHash {hash} bị trùng với map khác.");
        }

        private static void ValidatePhysicsProfile(
            GameManager manager,
            string sceneName,
            string prefix,
            List<string> errors)
        {
            ScenePhysicsProfile[] profiles = manager.GetComponents<ScenePhysicsProfile>();
            if (profiles.Length != 1)
            {
                errors.Add(prefix + $"GameManager cần đúng 1 ScenePhysicsProfile, hiện có {profiles.Length}.");
                return;
            }

            Vector3 expected = sceneName == "Moon" ? k_MoonGravity : k_DefaultGravity;
            if ((profiles[0].Gravity - expected).sqrMagnitude > 0.0001f)
                errors.Add(prefix + $"gravity phải là {expected}, hiện là {profiles[0].Gravity}.");
        }

        private static void ValidateManagerReferences(GameManager manager, string prefix, List<string> errors)
        {
            if (manager.m_CameraControl == null)
                errors.Add(prefix + "thiếu CameraControl.");

            if (manager.m_Tank1Prefab == null || manager.m_Tank2Prefab == null ||
                manager.m_Tank3Prefab == null || manager.m_Tank4Prefab == null)
            {
                errors.Add(prefix + "thiếu một hoặc nhiều tank prefab.");
            }

            var serialized = new SerializedObject(manager);
            SerializedProperty runtimeSlots = serialized.FindProperty("m_SpawnPoints");
            if (runtimeSlots == null || runtimeSlots.arraySize < 4)
                errors.Add(prefix + "m_SpawnPoints cần ít nhất 4 slot runtime.");

            Transform[] duel = ReadTransformArray(serialized.FindProperty("m_DuelSpawnPoints"), 2, "1v1", prefix, errors);
            Transform[] team = ReadTransformArray(serialized.FindProperty("m_TeamSpawnPoints"), 4, "2v2", prefix, errors);

            ValidateLayout(duel, "1v1", 15f, prefix, errors);
            ValidateLayout(team, "2v2", 7f, prefix, errors);

            if (team.Length == 4 && team.All(point => point != null))
            {
                float blueZ = (team[0].position.z + team[1].position.z) * 0.5f;
                float redZ = (team[2].position.z + team[3].position.z) * 0.5f;
                if (Mathf.Abs(blueZ - redZ) < 15f)
                    errors.Add(prefix + "hai đội 2v2 chưa nằm ở hai phía đủ xa nhau.");
            }
        }

        private static Transform[] ReadTransformArray(
            SerializedProperty property,
            int requiredSize,
            string layoutName,
            string prefix,
            List<string> errors)
        {
            if (property == null || property.arraySize != requiredSize)
            {
                int actualSize = property != null ? property.arraySize : 0;
                errors.Add(prefix + $"layout {layoutName} cần {requiredSize} điểm, hiện có {actualSize}.");
                return Array.Empty<Transform>();
            }

            var result = new Transform[requiredSize];
            for (int i = 0; i < requiredSize; i++)
            {
                result[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (result[i] == null)
                    errors.Add(prefix + $"layout {layoutName} thiếu reference tại slot {i}.");
            }
            return result;
        }

        private static void ValidateLayout(
            Transform[] points,
            string layoutName,
            float minimumSeparation,
            string prefix,
            List<string> errors)
        {
            var uniquePoints = new HashSet<Transform>();
            Physics.SyncTransforms();

            for (int i = 0; i < points.Length; i++)
            {
                Transform point = points[i];
                if (point == null)
                    continue;

                if (!uniquePoints.Add(point))
                    errors.Add(prefix + $"layout {layoutName} dùng trùng Transform tại slot {i}.");

                if (!NavMesh.SamplePosition(point.position, out NavMeshHit navHit, 0.75f, NavMesh.AllAreas))
                {
                    errors.Add(prefix + $"{layoutName} slot {i} không nằm trên NavMesh.");
                }
                else if (Vector3.Distance(point.position, navHit.position) > 0.75f)
                {
                    errors.Add(prefix + $"{layoutName} slot {i} lệch NavMesh quá xa.");
                }

                Collider[] blockers = Physics.OverlapBox(
                    point.position + Vector3.up * 1.5f,
                    new Vector3(2.1f, 1.2f, 2.1f),
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                if (blockers.Length > 0)
                {
                    string blockerNames = string.Join(", ", blockers.Select(collider => collider.name).Distinct());
                    errors.Add(prefix + $"{layoutName} slot {i} đè vật cản: {blockerNames}.");
                }

                for (int other = 0; other < i; other++)
                {
                    if (points[other] != null &&
                        Vector3.Distance(points[other].position, point.position) < minimumSeparation)
                    {
                        errors.Add(prefix + $"{layoutName} slot {other} và {i} ở quá gần nhau.");
                    }
                }
            }
        }
    }

    public sealed class GameplayMapBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            GameplayMapContractValidator.ValidateOrThrow();
        }
    }
}
