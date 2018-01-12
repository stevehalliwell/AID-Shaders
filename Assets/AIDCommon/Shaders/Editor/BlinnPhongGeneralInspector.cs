using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//we only add a few more props that are always enabled so the LambertGeneralInspector is fine
public class BlinnPhongGeneralInspector : LambertGeneralInspector
{
    protected override void CreateToggleList()
    {
        Toggles.Add(new FeatureToggle("Spec Controls", "SpecCont", "SPEC_CONTROL_MAPS_ENABLED", true));
        base.CreateToggleList();
    }
}
