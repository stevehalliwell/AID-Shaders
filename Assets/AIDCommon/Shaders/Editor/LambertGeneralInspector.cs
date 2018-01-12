using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LambertGeneralInspector : CustomMaterialEditor
{
    protected override void CreateToggleList()
    {
        Toggles.Add(new FeatureToggle("Emissive", "Emiss", "EMISSIVE_ENABLED", true));
        Toggles.Add(new FeatureToggle("Normal Map", "normal", "NORMAL_ENABLED", true));
        Toggles.Add(new FeatureToggle("Rim", "rim", "RIM_ENABLED", true));
        Toggles.Add(new FeatureToggle("Environ Mapping", "worldRef", "WORLDREF_ENABLED", true));
        this.AddRenderOpsToToggleList();
    }
}
