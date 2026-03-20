using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BatchSliceByCellCount
{
    // 对应你截图里的 Grid By Cell Count
    private const int ColumnCount = 2;
    private const int RowCount = 1;

    [MenuItem("Tools/Sprites/Batch Slice Selected (3x1 Cell Count)")]
    public static void BatchSliceSelectedTextures()
    {
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("没有选中任何资源。请先在 Project 面板里选中要切片的图片。");
            return;
        }

        int processedCount = 0;

        foreach (Object obj in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            if (!IsTextureFile(path))
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"无法读取纹理：{path}");
                continue;
            }

            SliceTextureByCellCount(importer, texture, path);
            processedCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"批量切片完成，共处理 {processedCount} 张图片。");
    }

    private static bool IsTextureFile(string path)
    {
        string lower = path.ToLower();
        return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg");
    }

    private static void SliceTextureByCellCount(TextureImporter importer, Texture2D texture, string path)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;

        int textureWidth = texture.width;
        int textureHeight = texture.height;

        int cellWidth = textureWidth / ColumnCount;
        int cellHeight = textureHeight / RowCount;

        if (cellWidth <= 0 || cellHeight <= 0)
        {
            Debug.LogWarning($"切片尺寸无效：{path}，贴图尺寸 {textureWidth}x{textureHeight}");
            return;
        }

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        string fileName = Path.GetFileNameWithoutExtension(path);

        // Method = Delete Existing
        // 这里直接重建整套 spritesheet 数据
        for (int row = 0; row < RowCount; row++)
        {
            for (int col = 0; col < ColumnCount; col++)
            {
                Rect rect = new Rect(
                    col * cellWidth,
                    textureHeight - ((row + 1) * cellHeight),
                    cellWidth,
                    cellHeight
                );

                SpriteMetaData meta = new SpriteMetaData
                {
                    name = $"{fileName}_{row}_{col}",
                    rect = rect,
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };

                metas.Add(meta);
            }
        }

#pragma warning disable CS0618
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618

        importer.SaveAndReimport();
        Debug.Log($"已切片：{path} -> {ColumnCount}列 x {RowCount}行，共 {metas.Count} 个 Sprite");
    }
}