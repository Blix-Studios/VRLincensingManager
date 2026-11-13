using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.18 - runtime copies of the materials that carry the client's logo.
    /// A branding applier must NEVER write into a shared material asset: in the Editor that
    /// change outlives Play mode and is saved to disk (pre.17 left LogoDecal_MAT without its
    /// texture), and in a build it leaks into every object that uses the material. The appliers
    /// therefore put a copy on their decal projector / renderer, and give it back here when they
    /// are destroyed.
    /// Appliers that need the same thing (same original material, property and texture) share
    /// one copy, so six decals with the client's logo are still one material.
    /// </summary>
    public static class BrandingMaterials
    {
        private sealed class Entry
        {
            public string key;
            public Material material;
            public int users;
        }

        private static readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>();
        private static readonly Dictionary<int, Entry> byInstance = new Dictionary<int, Entry>();

        /// <summary>Copies alive right now (tests, diagnostics).</summary>
        public static int LiveCount => byKey.Count;

        /// <summary>
        /// A copy of <paramref name="original"/> whose <paramref name="property"/> is
        /// <paramref name="texture"/> (null clears it). The original is only read.
        /// Every call must be paired with <see cref="Release"/>.
        /// </summary>
        public static Material Acquire(Material original, string property, Texture texture)
        {
            if (original == null || string.IsNullOrEmpty(property)) return null;

            string key = original.GetInstanceID() + "|" + property + "|" + (texture != null ? texture.GetInstanceID() : 0);
            if (byKey.TryGetValue(key, out Entry entry) && entry.material != null)
            {
                entry.users++;
                return entry.material;
            }

            var copy = new Material(original);
            copy.name = original.name + " (Branding)";
            // Never written into a scene or an asset, whatever is saved while it exists.
            copy.hideFlags = HideFlags.DontSave;
            copy.SetTexture(property, texture);

            entry = new Entry { key = key, material = copy, users = 1 };
            byKey[key] = entry;
            byInstance[copy.GetInstanceID()] = entry;
            return copy;
        }

        /// <summary>Gives a copy back; it is destroyed when its last user releases it.</summary>
        public static void Release(Material copy)
        {
            if (ReferenceEquals(copy, null)) return;
            int id = copy.GetInstanceID();
            if (!byInstance.TryGetValue(id, out Entry entry)) return;
            if (--entry.users > 0) return;

            byInstance.Remove(id);
            byKey.Remove(entry.key);
            if (entry.material == null) return;
            if (Application.isPlaying) Object.Destroy(entry.material);
            else Object.DestroyImmediate(entry.material);
        }

        /// <summary>True for a material created by <see cref="Acquire"/> (never a project asset).</summary>
        public static bool IsCopy(Material material)
        {
            return !ReferenceEquals(material, null) && byInstance.ContainsKey(material.GetInstanceID());
        }
    }
}
