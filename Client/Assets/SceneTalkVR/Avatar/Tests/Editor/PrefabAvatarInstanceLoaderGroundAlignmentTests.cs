using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class PrefabAvatarInstanceLoaderGroundAlignmentTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [UnityTest]
        public IEnumerator LoadAvatar_NoGroundCollider_AlignsVisualBottomToWorldZero()
        {
            var parent = Track(new GameObject("RaisedAvatarRoot"));
            parent.transform.position = new Vector3(0f, 5f, 0f);

            GameObject loadedAvatar = null;
            yield return CreateLoader().LoadAvatar(
                CreateResolution(CreateAvatarSource()),
                parent.transform,
                avatar => loadedAvatar = Track(avatar),
                Assert.Fail);

            Assert.That(loadedAvatar, Is.Not.Null);
            Assert.That(GetVisualBounds(loadedAvatar).min.y, Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator LoadAvatar_GroundCollider_AlignsVisualBottomToColliderSurface()
        {
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0f, 1f, 0f);
            ground.transform.localScale = new Vector3(10f, 0.2f, 10f);

            var parent = Track(new GameObject("LoweredAvatarRoot"));
            parent.transform.position = new Vector3(0f, -5f, 0f);

            GameObject loadedAvatar = null;
            yield return CreateLoader().LoadAvatar(
                CreateResolution(CreateAvatarSource()),
                parent.transform,
                avatar => loadedAvatar = Track(avatar),
                Assert.Fail);

            Assert.That(loadedAvatar, Is.Not.Null);
            Assert.That(GetVisualBounds(loadedAvatar).min.y, Is.EqualTo(1.1f).Within(0.001f));
        }

        private PrefabAvatarInstanceLoader CreateLoader()
        {
            return Track(new GameObject("AvatarLoader")).AddComponent<PrefabAvatarInstanceLoader>();
        }

        private GameObject CreateAvatarSource()
        {
            var source = Track(new GameObject("AvatarSource"));
            source.transform.position = new Vector3(100f, 0f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(source.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            return source;
        }

        private static AvatarResolutionResult CreateResolution(GameObject prefab)
        {
            return new AvatarResolutionResult
            {
                avatarKey = "ground_alignment_test",
                preset = new AvatarPresetEntry
                {
                    key = "ground_alignment_test",
                    prefab = prefab
                }
            };
        }

        private GameObject Track(GameObject gameObject)
        {
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static Bounds GetVisualBounds(GameObject gameObject)
        {
            var renderer = gameObject.GetComponentInChildren<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            return renderer.bounds;
        }
    }
}
