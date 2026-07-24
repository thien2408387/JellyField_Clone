using KingCat.Base;
using Sirenix.OdinInspector;
using UnityEngine;

public class AStarGridProvider : MonoSingleton<AStarGridProvider>
{
    [FoldoutGroup("REFERENCES"), SerializeField] private AStarGrid _aStarGrid = null;

    private static AStarGrid _activeGrid;

    public AStarGrid Grid => _activeGrid != null ? _activeGrid : _aStarGrid;

    public static void RegisterActiveGrid(AStarGrid candidate)
    {
        if (candidate == null)
        {
            return;
        }

        int incomingPriority = candidate.ProviderRegistrationPriority;

        if (_activeGrid != null && !ReferenceEquals(_activeGrid, candidate))
        {
            int currentPriority = _activeGrid.ProviderRegistrationPriority;

            if (incomingPriority < currentPriority)
            {
                return;
            }

            if (incomingPriority == currentPriority)
            {
                bool currentHasRoot = _activeGrid.HasPixelCubeRoot;
                bool incomingHasRoot = candidate.HasPixelCubeRoot;

                if (currentHasRoot && !incomingHasRoot)
                {
                    return;
                }

                if (currentHasRoot == incomingHasRoot)
                {
                    int currentDepth = GetHierarchyDepth(_activeGrid.transform);
                    int incomingDepth = GetHierarchyDepth(candidate.transform);
                    if (incomingDepth >= currentDepth)
                    {
                        return;
                    }
                }
            }
        }

        _activeGrid = candidate;
    }

    public static void UnregisterActiveGrid(AStarGrid aStarGrid)
    {
        if (_activeGrid != aStarGrid)
        {
            return;
        }

        _activeGrid = null;

        AStarGrid[] candidates = Object.FindObjectsByType<AStarGrid>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AStarGrid best = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            AStarGrid grid = candidates[i];
            if (grid == null || !grid.isActiveAndEnabled || ReferenceEquals(grid, aStarGrid))
            {
                continue;
            }

            int priority = grid.ProviderRegistrationPriority;
            if (priority > bestPriority)
            {
                bestPriority = priority;
                best = grid;
                continue;
            }

            if (priority == bestPriority && best != null && grid.HasPixelCubeRoot && !best.HasPixelCubeRoot)
            {
                best = grid;
                continue;
            }

            if (priority == bestPriority &&
                best != null &&
                grid.HasPixelCubeRoot == best.HasPixelCubeRoot &&
                GetHierarchyDepth(grid.transform) < GetHierarchyDepth(best.transform))
            {
                best = grid;
            }
        }

        _activeGrid = best;
    }

    private static int GetHierarchyDepth(Transform transform)
    {
        int depth = 0;
        Transform current = transform;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    public static bool TryResolve(out AStarGrid aStarGrid)
    {
        if (_activeGrid != null)
        {
            aStarGrid = _activeGrid;
            return true;
        }

        if (TryGetInstance(out AStarGridProvider provider) && provider != null && provider._aStarGrid != null)
        {
            aStarGrid = provider._aStarGrid;
            return true;
        }

        aStarGrid = null;
        return false;
    }

    public static void MarkActiveGridDirty()
    {
        if (TryResolve(out AStarGrid aStarGrid) && aStarGrid != null)
        {
            aStarGrid.NotifyJellyColliderLayoutMayNeedPathRefresh();
        }
    }
}
