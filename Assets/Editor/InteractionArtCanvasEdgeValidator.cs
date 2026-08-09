using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NotANap.Editor
{
    /// <summary>
    /// F-09 회귀 방지: 직접 돌봄 합성 PNG가 캔버스 경계에서 긴 직선으로 잘린 채
    /// 임포트되는 것을 즉시 알린다.
    /// </summary>
    public sealed class InteractionArtCanvasEdgeValidator : AssetPostprocessor
    {
        public const byte VisibleAlphaThreshold = 16;
        public const int RejectedBorderRunLength = 32;

        private static readonly HashSet<string> ValidatedAssets = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "Assets/Resources/Art/Baby/Interaction/diaper_check.png",
            "Assets/Resources/Art/Baby/Interaction/diaper_change.png",
            "Assets/Resources/Art/Baby/Interaction/limb_check.png",
            "Assets/Resources/Art/Baby/Interaction/temperature_check.png",
            "Assets/Resources/Art/Baby/Interaction/lying_sleep.png"
        };

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!ValidatedAssets.Contains(assetPath)) return;

            int longestRun = LongestVisibleRunOnCanvasEdge(
                texture.GetPixels32(), texture.width, texture.height, VisibleAlphaThreshold);
            if (longestRun < RejectedBorderRunLength) return;

            Debug.LogError(
                $"[InteractionArt] {assetPath} has a {longestRun}px alpha run touching the " +
                "canvas edge. Complete arms/sleeves inside the frame or fade alpha to zero " +
                $"before the edge (limit: {RejectedBorderRunLength - 1}px).",
                texture);
        }

        public static int LongestVisibleRunOnCanvasEdge(
            Color32[] pixels, int width, int height, byte alphaThreshold)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (width <= 0 || height <= 0 || pixels.Length != width * height)
                throw new ArgumentException("Pixel data must match width × height.", nameof(pixels));

            int longest = 0;
            longest = Math.Max(longest, LongestHorizontalRun(pixels, width, 0, alphaThreshold));
            longest = Math.Max(longest,
                LongestHorizontalRun(pixels, width, (height - 1) * width, alphaThreshold));
            longest = Math.Max(longest, LongestVerticalRun(pixels, width, height, 0, alphaThreshold));
            longest = Math.Max(longest,
                LongestVerticalRun(pixels, width, height, width - 1, alphaThreshold));
            return longest;
        }

        private static int LongestHorizontalRun(
            Color32[] pixels, int width, int offset, byte alphaThreshold)
        {
            int longest = 0;
            int current = 0;
            for (int x = 0; x < width; x++)
            {
                current = pixels[offset + x].a >= alphaThreshold ? current + 1 : 0;
                longest = Math.Max(longest, current);
            }
            return longest;
        }

        private static int LongestVerticalRun(
            Color32[] pixels, int width, int height, int x, byte alphaThreshold)
        {
            int longest = 0;
            int current = 0;
            for (int y = 0; y < height; y++)
            {
                current = pixels[y * width + x].a >= alphaThreshold ? current + 1 : 0;
                longest = Math.Max(longest, current);
            }
            return longest;
        }
    }
}
