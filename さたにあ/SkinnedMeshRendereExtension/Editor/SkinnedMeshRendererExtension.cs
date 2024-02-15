using HarmonyLib;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace satania
{
    public static class DynamicSkinnedMeshRendererEditorPatch
    {
        public static void ApplyPatch()
        {
            var harmony = new Harmony("satania.harmony.patcher");

            Type skinnedMeshRendererEditorType = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor");

            if (skinnedMeshRendererEditorType != null)
            {
                MethodInfo originalMethod = skinnedMeshRendererEditorType.GetMethod("OnBlendShapeUI", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                var prefixMethodInfo = typeof(SkinnedMeshRendererEditorPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);

                // Harmonyでメソッドをパッチ
                if (originalMethod != null)
                {
                    harmony.Patch(originalMethod,
                        prefix: new HarmonyMethod(prefixMethodInfo));

                    Debug.Log("Patched SkinnedMeshRendererEditor.");
                }
            }


        }
        public static void Unpatch()
        {
            var harmony = new Harmony("satania.harmony.patcher");
            harmony.UnpatchAll();
        }
    }

    // エディタ用スクリプトの例
    public class InitializeHarmonyPatch
    {
        // Unityエディタがロードされたときに自動的に実行されるメソッド
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            DynamicSkinnedMeshRendererEditorPatch.Unpatch();
            DynamicSkinnedMeshRendererEditorPatch.ApplyPatch();
        }
    }

    public static class SkinnedMeshRendererEditorPatch
    {
        public static string searchText = "";

        public static void GetAndInvokeSlider(SerializedProperty property, float sliderLeftValue, float sliderRightValue, float textLeftValue, float textRightValue, GUIContent label, params GUILayoutOption[] options)
        {
            Type editorGUILayoutType = typeof(EditorGUILayout);

            MethodInfo sliderMethodInfo = editorGUILayoutType.GetMethod("Slider", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(SerializedProperty), typeof(float), typeof(float), typeof(float), typeof(float), typeof(GUIContent), typeof(GUILayoutOption[]) }, null);

            if (sliderMethodInfo != null)
            {
                sliderMethodInfo.Invoke(null, new object[] { property, sliderLeftValue, sliderRightValue, textLeftValue, textRightValue, label, options });
            }
            else
            {
                Debug.LogError("Method 'Slider' not found.");
            }
        }

        public static float InvokeCustomSlider(GUIContent label, float value, float sliderLeftValue, float sliderRightValue, float textLeftValue, float textRightValue, params GUILayoutOption[] options)
        {
            Type editorGUILayoutType = typeof(EditorGUILayout);

            MethodInfo sliderMethodInfo = editorGUILayoutType.GetMethod("Slider", BindingFlags.NonPublic | BindingFlags.Static, null,
                new Type[] { typeof(GUIContent), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(GUILayoutOption[]) }, null);

            if (sliderMethodInfo != null)
            {
                object result = sliderMethodInfo.Invoke(null, new object[] { label, value, sliderLeftValue, sliderRightValue, textLeftValue, textRightValue, options });
                return (float)result;
            }
            else
            {
                Debug.LogError("Method 'Slider' not found.");
                return value;
            }
        }

        public static SerializedProperty GetBlendShapeWeightsProperty(Editor editor)
        {
            SerializedObject serializedObject = new SerializedObject(editor.target);

            SerializedProperty blendShapeWeightsProperty = serializedObject.FindProperty("m_BlendShapeWeights");

            return blendShapeWeightsProperty;
        }

        public static bool Prefix(object __instance)
        {
            if (__instance == null)
                return false;

            Type skinnedMeshRendererEditorType = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor");

            Type stylesType = skinnedMeshRendererEditorType.GetNestedType("Styles", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            var legacyClampBlendShapeWeightsInfoProperty = stylesType.GetField("legacyClampBlendShapeWeightsInfo", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            GUIContent legacyClampBlendShapeWeightsInfo = (GUIContent)legacyClampBlendShapeWeightsInfoProperty.GetValue(null);

            GUILayout.Space(10);

            var type = __instance.GetType();

            var target = type.GetProperty("target", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (target != null)
            {
                var targetValue = target.GetValue(__instance);
                SkinnedMeshRenderer skinnedMeshRenderer = targetValue as SkinnedMeshRenderer;
                Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;

                int blendShapeCount = sharedMesh == null ? 0 : sharedMesh.blendShapeCount;
                if (blendShapeCount == 0)
                    return false;

                var m_BlendshapeWeights = GetBlendShapeWeightsProperty(__instance as Editor);
                GUIContent guicontent = new GUIContent();
                guicontent.text = "BlendShapes";
                EditorGUILayout.PropertyField(m_BlendshapeWeights, guicontent, false, new GUILayoutOption[0]);
                if (m_BlendshapeWeights.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    searchText = EditorGUILayout.TextField(searchText);

                    bool legacyClampBlendShapeWeights = PlayerSettings.legacyClampBlendShapeWeights;
                    if (legacyClampBlendShapeWeights)
                    {
                        EditorGUILayout.HelpBox(legacyClampBlendShapeWeightsInfo.text, MessageType.Info);
                    }
                    int blendshapeWeightsArraySize = m_BlendshapeWeights.arraySize;
                    for (int i = 0; i < blendShapeCount; i++)
                    {
                        string blendshapeName = sharedMesh.GetBlendShapeName(i);
                        if (!string.IsNullOrEmpty(searchText) && !Regex.IsMatch(blendshapeName, searchText, RegexOptions.IgnoreCase))
                            continue;

                        guicontent.text = sharedMesh.GetBlendShapeName(i);

                        float num3 = 0f;
                        float num4 = 0f;
                        int blendShapeFrameCount = sharedMesh.GetBlendShapeFrameCount(i);
                        for (int j = 0; j < blendShapeFrameCount; j++)
                        {
                            float blendShapeFrameWeight = sharedMesh.GetBlendShapeFrameWeight(i, j);
                            num3 = Mathf.Min(blendShapeFrameWeight, num3);
                            num4 = Mathf.Max(blendShapeFrameWeight, num4);
                        }

                        if (i < blendshapeWeightsArraySize)
                        {
                            EditorGUI.BeginChangeCheck();
                            float floatValue = InvokeCustomSlider(guicontent, m_BlendshapeWeights.GetArrayElementAtIndex(i).floatValue, 0.0f, 100.0f, float.MinValue, float.MaxValue, new GUILayoutOption[0]);

                            if (EditorGUI.EndChangeCheck())
                            {
                                m_BlendshapeWeights.arraySize = blendShapeCount;
                                blendshapeWeightsArraySize = blendShapeCount;
                                m_BlendshapeWeights.GetArrayElementAtIndex(i).floatValue = floatValue;

                                m_BlendshapeWeights.serializedObject.ApplyModifiedProperties();
                                m_BlendshapeWeights.serializedObject.Update();
                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                return true;
            }
    
            return false;
        }
    }
}