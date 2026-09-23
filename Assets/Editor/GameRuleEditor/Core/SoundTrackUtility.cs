using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameRuleEditor.Core
{
    /// <summary>
    /// Converts soundtrack selections to portable JSON references and applies them
    /// to the generated GameManager. Full asset paths are used for new selections;
    /// legacy JSON values containing only a clip name remain supported.
    /// </summary>
    internal static class SoundTrackUtility
    {
        public static string GetReference(AudioClip clip)
        {
            return clip == null ? "" : AssetDatabase.GetAssetPath(clip);
        }

        public static AudioClip Resolve(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;

            string normalized = reference.Trim().Replace('\\', '/');
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(normalized);
            if (clip != null) return clip;

            string resourcePath = ToResourcePath(normalized);
            if (!string.IsNullOrEmpty(resourcePath))
            {
                clip = Resources.Load<AudioClip>(resourcePath);
                if (clip != null) return clip;
            }

            string clipName = Path.GetFileNameWithoutExtension(normalized);
            string[] guids = AssetDatabase.FindAssets("t:AudioClip");
            Array.Sort(guids, StringComparer.Ordinal);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip candidate = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (candidate != null &&
                    string.Equals(candidate.name, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static void ApplyToGameManager(GameObject gameManager, string reference, bool recordUndo)
        {
            if (gameManager == null) return;

            foreach (AudioSource existing in gameManager.GetComponents<AudioSource>())
            {
                if (recordUndo)
                    Undo.DestroyObjectImmediate(existing);
                else
                    UnityEngine.Object.DestroyImmediate(existing);
            }

            if (string.IsNullOrWhiteSpace(reference)) return;

            AudioClip clip = Resolve(reference);
            if (clip == null)
            {
                Debug.LogWarning($"Soundtrack not found: {reference}");
                return;
            }

            AudioSource source = recordUndo
                ? Undo.AddComponent<AudioSource>(gameManager)
                : gameManager.AddComponent<AudioSource>();

            source.clip = clip;
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
        }

        private static string ToResourcePath(string reference)
        {
            const string resourcesMarker = "/Resources/";
            int resourcesIndex = reference.IndexOf(resourcesMarker, StringComparison.OrdinalIgnoreCase);
            string resourcePath;

            if (resourcesIndex >= 0)
                resourcePath = reference.Substring(resourcesIndex + resourcesMarker.Length);
            else if (!reference.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                resourcePath = reference;
            else
                return null;

            string extension = Path.GetExtension(resourcePath);
            if (!string.IsNullOrEmpty(extension))
                resourcePath = resourcePath.Substring(0, resourcePath.Length - extension.Length);

            return resourcePath;
        }
    }
}
