using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text.RegularExpressions;

//http://www.martinpalko.com/muli-compile-unity/
public abstract class CustomMaterialEditor : MaterialEditor
{
    public enum BlendMode
    {
        Opaque,
        Cutout,
        Fade,
        Transparent
    }

    public class FeatureToggle
    {
        // The name the toggle will have in the inspector.
        public string InspectorName;
        // We will look for properties that contain this word, and hide them if we're not enabled.
        public string InspectorPropertyHideTag;
        // The keyword that the shader uses when this feature is enabled or disabled.
        public string ShaderKeyword;
        // The current state of this feature.
        public bool Enabled;
        public bool Foldout = true;
        public bool ShowEnabled = true;

        public FeatureToggle(string InspectorName, string InspectorPropertyHideTag, string ShaderKeywordEnabled, bool enabled = false, bool showEnabled = true)
        {
            this.InspectorName = InspectorName;
            this.InspectorPropertyHideTag = InspectorPropertyHideTag;
            this.ShaderKeyword = ShaderKeywordEnabled;
            this.Enabled = enabled;
            this.ShowEnabled = showEnabled;
        }
    }

    // A list of all the toggles that we have in this material editor.
    protected List<FeatureToggle> Toggles = new List<FeatureToggle>();
    
    private float previousFieldWidth;
    const float MaterialFieldWidth = 64;

    // This function will be implemented in derived classes, and used to populate the list of toggles.
    protected abstract void CreateToggleList();

    const string RENDER_OPS = "Render Ops";
    const string RENDER_OPS_ENABLED_EDITOR_KEY = "RENDER_OPS_ENABLED_EDITOR_KEY";

    protected void AddRenderOpsToToggleList()
    {
        Toggles.Add(new FeatureToggle(RENDER_OPS, "__", "RENDER_OPS_ENABLED", false, false));
    }

    protected bool showRenderOpProperties = false;

    public override void OnEnable()
    {
        base.OnEnable();

        Material targetMat = target as Material;
        string[] oldKeyWords = targetMat.shaderKeywords;

        // Populate our list of toggles
        //Toggles.Clear();
        Toggles = new List<FeatureToggle>();
        CreateToggleList();

        // Update each toggle to enabled if it's enabled keyword is present. If it's enabled keyword is missing, we assume it's disabled.
        for (int i = 0; i < Toggles.Count; i++)
        {
            Toggles[i].Enabled = oldKeyWords.Contains(Toggles[i].ShaderKeyword);
            Toggles[i].Foldout = Toggles[i].Enabled;
        }

        var renderOpsToggle = Toggles.Find(x => x.InspectorName == RENDER_OPS);

        if (renderOpsToggle != null)
        {
            renderOpsToggle.Foldout = EditorPrefs.GetBool(RENDER_OPS_ENABLED_EDITOR_KEY, renderOpsToggle.Foldout);
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();

        var renderOpsToggle = Toggles.Find(x => x.InspectorName == RENDER_OPS);

        if(renderOpsToggle != null)
        {
            EditorPrefs.SetBool(RENDER_OPS_ENABLED_EDITOR_KEY, renderOpsToggle.Foldout);
        }
    }

    public override void OnInspectorGUI()
    {
        // if we are not visible... return
        if (!isVisible)
            return;

        // Get the current keywords from the material
        Material targetMat = target as Material;

        // Begin listening for changes in GUI, so we don't waste time re-applying settings that haven't changed.
        EditorGUI.BeginChangeCheck();

        serializedObject.Update();
        var theShader = serializedObject.FindProperty("m_Shader");
        if (isVisible && !theShader.hasMultipleDifferentValues && theShader.objectReferenceValue != null)
        {
            EditorGUI.BeginChangeCheck();

            previousFieldWidth = EditorGUIUtility.fieldWidth;
            
            //force texture previews to be square
            EditorGUIUtility.fieldWidth = MaterialFieldWidth;
            //until such time as we can actually get the avail width of the inspector we probably don't want this
            //EditorGUIUtility.labelWidth = Mathf.Min(128,EditorGUIUtility.currentViewWidth - EditorGUIUtility.fieldWidth - 19);

            var props = GetMaterialProperties(new Object[] { targetMat });

            var modeProp = System.Array.Find(props, x => x.displayName == "__Mode");
            BlendModePopup(modeProp);

            // Draw Non-toggleable values
            for (int i = 0; i < props.Length; i++)
            {
                ShaderPropertyImpl(props[i], null);
            }

            // Draw toggles, then their values.
            for (int s = 0; s < Toggles.Count; s++)
            {
                EditorGUILayout.Separator();
                if (Toggles[s].ShowEnabled)
                {
                    Toggles[s].Foldout = EditorGUILayout.Foldout(Toggles[s].Foldout, Toggles[s].InspectorName, true);
                    Toggles[s].Enabled = EditorGUILayout.BeginToggleGroup("Enabled", Toggles[s].Enabled);
                }
                else
                    Toggles[s].Foldout = EditorGUILayout.Foldout(Toggles[s].Foldout, Toggles[s].InspectorName, true);

                if (Toggles[s].Foldout)
                {
                    for (int i = 0; i < props.Length; i++)
                    {
                        ShaderPropertyImpl(props[i], Toggles[s]);
                    }
                }

                if (Toggles[s].ShowEnabled)
                    EditorGUILayout.EndToggleGroup();
            }

            EditorGUILayout.Space();
            this.RenderQueueField();
            this.EnableInstancingField();
            this.DoubleSidedGIField();

            if (EditorGUI.EndChangeCheck())
                PropertiesChanged();
        }

        // If changes have been made, then apply them.
        if (EditorGUI.EndChangeCheck())
        {
            //TODO couldn't we just use enable and disable keyword?
            for (int i = 0; i < Toggles.Count; i++)
            {
                if (Toggles[i].Enabled)
                    targetMat.EnableKeyword(Toggles[i].ShaderKeyword);
                else
                    targetMat.DisableKeyword(Toggles[i].ShaderKeyword);
            }

            EditorUtility.SetDirty(targetMat);
        }
    }

    // This runs once for every property in our shader.
    private void ShaderPropertyImpl(MaterialProperty matProp, FeatureToggle currentToggle)
    {
        string propertyDescription = matProp.name;//ShaderUtil.GetPropertyDescription(shader, propertyIndex);

        // If current toggle is null, we only want to show properties that aren't already "owned" by a toggle,
        // so if it is owned by another toggle, then return.
        if (currentToggle == null)
        {
            for (int i = 0; i < Toggles.Count; i++)
            {
                if (Regex.IsMatch(propertyDescription, Toggles[i].InspectorPropertyHideTag, RegexOptions.IgnoreCase))
                {
                    return;
                }
            }
        }
        // Only draw if we the current property is owned by the current toggle.
        else if (!Regex.IsMatch(propertyDescription, currentToggle.InspectorPropertyHideTag, RegexOptions.IgnoreCase))
        {
            return;
        }

        //is it forced hidden
        if ( (matProp.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
            return;

        // If we've gotten to this point, draw the shader property regulairly.
        if(matProp.type == MaterialProperty.PropType.Range)
        {
            EditorGUIUtility.fieldWidth = previousFieldWidth;
            RangeProperty(matProp, matProp.displayName);
            //matProp.floatValue = EditorGUILayout.Slider(new GUIContent(matProp.displayName, matProp.name), matProp.floatValue, matProp.rangeLimits.x, matProp.rangeLimits.y);
            EditorGUIUtility.fieldWidth = MaterialFieldWidth;
        }
        else if (matProp.type == MaterialProperty.PropType.Color)
        {
            ColorProperty(matProp, matProp.displayName);
        }
        else
        {
            ShaderProperty(matProp, new GUIContent(matProp.displayName));
        }
    }

    //alpha use
    public void SetupMaterialWithBlendMode(BlendMode blendMode)
    {
        Material material = target as Material;
        switch (blendMode)
        {
            case BlendMode.Opaque:
                material.SetOverrideTag("RenderType", "");
                material.SetInt("__SrcBlend", 1);
                material.SetInt("__DstBlend", 0);
                material.SetInt("__ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                break;
            case BlendMode.Cutout:
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("__SrcBlend", 1);
                material.SetInt("__DstBlend", 0);
                material.SetInt("__ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 2450;
                break;
            case BlendMode.Fade:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("__SrcBlend", 5);
                material.SetInt("__DstBlend", 10);
                material.SetInt("__ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                break;
            case BlendMode.Transparent:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("__SrcBlend", 1);
                material.SetInt("__DstBlend", 10);
                material.SetInt("__ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                break;
        }
        EditorUtility.SetDirty(material);
    }

    private void BlendModePopup(MaterialProperty modeProp)
    {
        if (modeProp == null)
            return;

        EditorGUI.showMixedValue = modeProp.hasMixedValue;
        BlendMode blendMode = (BlendMode)modeProp.floatValue;
        EditorGUI.BeginChangeCheck();
        blendMode = (BlendMode)EditorGUILayout.Popup("Blend Mode", (int)blendMode, System.Enum.GetNames(typeof(BlendMode)));
        if (EditorGUI.EndChangeCheck())
        {
            modeProp.floatValue = (float)blendMode;
            SetupMaterialWithBlendMode(blendMode);
        }
        EditorGUI.showMixedValue = false;
    }
}