using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Applies a reversible transparent red tint to the selected room's glass. Selection has
    /// deliberately no outline geometry, colliders, lights, or material mutation.
    /// </summary>
    public sealed class RoomDeletionHighlight : IDisposable
    {
        private const float GlassTintStrength = 0.22f;
        private const string GlassShaderName = "What Light Remains/Light-Transmitting Glass";
        private static readonly int DeleteTintColorId = Shader.PropertyToID("_WLRDeleteTintColor");
        private static readonly int DeleteTintStrengthId = Shader.PropertyToID("_WLRDeleteTintStrength");
        private static readonly Color DeleteTintColor = new Color(1f, 0.02f, 0.015f, 1f);

        private readonly List<Renderer> tintedGlass = new List<Renderer>(32);
        private readonly HashSet<Renderer> uniqueTintedGlass = new HashSet<Renderer>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private RoomDeletionHighlight(GameObject root) => Root = root;

        public GameObject Root { get; private set; }
        public bool IsVisible => Root != null && Root.activeSelf;

        public static RoomDeletionHighlight Create()
        {
            GameObject root = new GameObject("Room Deletion Glass Tint");
            root.SetActive(false);
            return new RoomDeletionHighlight(root);
        }

        // Compatibility overload for an older generated prefab. The material is ignored.
        public static RoomDeletionHighlight Create(Material unused) => Create();

        public void Show(CubeRoom room)
        {
            if (Root == null || room == null) return;
            ClearGlassTint();
            foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>(true)) AddGlass(renderer);
            foreach (RoomPassage passage in UnityEngine.Object.FindObjectsByType<RoomPassage>(FindObjectsInactive.Include))
            {
                if (passage == null || (passage.RoomA != room && passage.RoomB != room)) continue;
                foreach (Renderer renderer in passage.GlassRenderers) AddGlass(renderer);
            }
            Root.SetActive(true);
        }

        public void Hide()
        {
            ClearGlassTint();
            if (Root != null) Root.SetActive(false);
        }

        public void Dispose()
        {
            ClearGlassTint();
            if (Root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
            Root = null;
        }

        private void AddGlass(Renderer renderer)
        {
            if (renderer == null || !UsesGlassShader(renderer) || !uniqueTintedGlass.Add(renderer)) return;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(DeleteTintColorId, DeleteTintColor);
            propertyBlock.SetFloat(DeleteTintStrengthId, GlassTintStrength);
            renderer.SetPropertyBlock(propertyBlock);
            tintedGlass.Add(renderer);
        }

        private void ClearGlassTint()
        {
            foreach (Renderer renderer in tintedGlass)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(DeleteTintStrengthId, 0f);
                renderer.SetPropertyBlock(propertyBlock);
            }
            tintedGlass.Clear();
            uniqueTintedGlass.Clear();
        }

        private static bool UsesGlassShader(Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.shader != null && material.shader.name == GlassShaderName)
                    return true;
            return false;
        }
    }
}
