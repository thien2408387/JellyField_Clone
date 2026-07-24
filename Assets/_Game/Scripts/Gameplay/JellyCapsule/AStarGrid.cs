using UnityEngine;

public class AStarGrid : MonoBehaviour
{
    [SerializeField] private int _providerRegistrationPriority;
    [SerializeField] private Transform _pixelCubeRoot;
    [SerializeField] private bool _buildFromPixelCubeGrid;
    [SerializeField] private PixelCubeColor _blockedCubeColor = PixelCubeColor.Red;
    [SerializeField] private float _gridPositionMatchRadius = 0.1f;
    [SerializeField] private Vector2Int _gridMin = new Vector2Int(-50, -50);
    [SerializeField] private Vector2Int _gridSize = new Vector2Int(100, 100);
    [SerializeField] private Vector2 _cellSize = Vector2.one;
    [SerializeField] private Vector3 _worldOffset = new Vector3(-50f, -50f, 0f);

    public int ProviderRegistrationPriority => _providerRegistrationPriority;
    public bool HasPixelCubeRoot => _pixelCubeRoot != null;

    public bool TryAppendMappedPixelCubeCellsUnderAncestor(Transform ancestor, System.Collections.Generic.List<PixelCubeCell> buffer)
    {
        return false;
    }

    public void NotifyJellyColliderLayoutMayNeedPathRefresh()
    {
    }
}
