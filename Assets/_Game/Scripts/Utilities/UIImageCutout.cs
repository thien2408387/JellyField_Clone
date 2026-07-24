using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class UIImageCutout : Image
{
    private Material _materialInstance;

    public override Material materialForRendering
    {
        get
        {
            if (_materialInstance == null)
            {
                _materialInstance = new Material(base.materialForRendering);
                _materialInstance.SetInt("_StencilComp", (int)CompareFunction.NotEqual);
            }
            return _materialInstance;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_materialInstance != null)
        {
            Destroy(_materialInstance);
            _materialInstance = null;
        }
    }
}