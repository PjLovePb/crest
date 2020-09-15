﻿using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Crest
{
    class BuildProcessor : IPreprocessShaders, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        int shaderVariantCount = 0;
        int shaderVarientStrippedCount = 0;
        string UnderwaterShaderName => "Crest/Underwater Curtain";
        readonly List<Material> _oceanMaterials = new List<Material>();

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // OnProcessScene is called on scene start too. Limit to building.
            if (!BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            // Resources.FindObjectsOfTypeAll will get all materials that are used for this scene.
            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material.shader.name != "Crest/Ocean")
                {
                    continue;
                }

                _oceanMaterials.Add(material);
            }
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader.name.StartsWith("Crest"))
            {
                shaderVariantCount += data.Count;
            }

            if (shader.name == UnderwaterShaderName)
            {
                ProcessUnderwaterShader(shader, snippet, data);
            }
        }

        public void ProcessUnderwaterShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // This should not happen. There should always be at least one variant.
            if (data.Count == 0)
            {
                return;
            }

            var shaderVariantCount = data.Count;
            var shaderVarientStrippedCount = 0;

            // Collect all shader keywords.
            var unusedShaderKeywords = new HashSet<ShaderKeyword>();
            for (int i = 0; i < data.Count; i++)
            {
                // Each ShaderCompilerData is a variant which is a combination of keywords. Since each list will be
                // different, simply getting a list of all keywords is not possible. This also appears to be the only
                // way to get a list of keywords without trying to extract them from shader property names.
                unusedShaderKeywords.UnionWith(data[i].shaderKeywordSet.GetShaderKeywords());
            }

            // Get used shader keywords so we can exclude them.
            var usedShaderKeywords = new List<ShaderKeyword>();
            foreach (var shaderKeyword in unusedShaderKeywords)
            {
                // GetKeywordName will work for both global and local keywords.
                var shaderKeywordName = ShaderKeyword.GetKeywordName(shader, shaderKeyword);

                // Meniscus shader keyword will not be on ocean material.
                if (shaderKeywordName.Contains("MENISCUS"))
                {
                    usedShaderKeywords.Add(shaderKeyword);
                    continue;
                }

                // Instancing should not be determined by the ocean material.
                if (shaderKeywordName.Contains("INSTANCING"))
                {
                    usedShaderKeywords.Add(shaderKeyword);
                    continue;
                }

                foreach (var oceanMaterial in _oceanMaterials)
                {
                    if (oceanMaterial.IsKeywordEnabled(shaderKeywordName))
                    {
                        usedShaderKeywords.Add(shaderKeyword);
                        break;
                    }
                }
            }

            // Exclude used keywords to obtain list of unused keywords.
            unusedShaderKeywords.ExceptWith(usedShaderKeywords);

            for (int index = 0; index < data.Count; index++)
            {
                foreach (var unusedShaderKeyword in unusedShaderKeywords)
                {
                    // IsEnabled means this variant uses this keyword and we can strip it.
                    if (data[index].shaderKeywordSet.IsEnabled(unusedShaderKeyword))
                    {
                        data.RemoveAt(index--);
                        shaderVarientStrippedCount++;
                    }
                }
            }

            this.shaderVarientStrippedCount += shaderVarientStrippedCount;

            Debug.Log($"Crest: {shaderVarientStrippedCount} shader variants stripped of {shaderVariantCount} from {shader.name}.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log($"Crest: Stripped {shaderVarientStrippedCount} shader variants of {shaderVariantCount} from Crest.");
        }
    }
}