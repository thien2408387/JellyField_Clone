using System;
using System.Collections.Generic;
using KingCat.Base.Assets;
using UnityEngine;

public partial class PixelLevelQuotaComposerWindow
{
    private const int MinJellyGridSize = 3;
    private const int MaxJellyGridWidth = 50;
    private const int MaxJellyGridHeight = 30;
    private const int MaxFreezeValue = 4;

    private int _jellyGridWidth = 10;
    private int _jellyGridHeight = 10;
    private float _jellyDisplaySize = 34f;
    private float _jellyCellSize = 1f;
    private CellConfig[] _jellyCellConfigs;
    private int _selectedJellyCellIndex = -1;
    private readonly Dictionary<int, Rect> _jellyCellRects = new Dictionary<int, Rect>();

    private void InitializeJellyGridIfNeeded()
    {
        if (_jellyCellConfigs == null || _jellyCellConfigs.Length != _jellyGridWidth * _jellyGridHeight)
        {
            _jellyCellConfigs = new CellConfig[_jellyGridWidth * _jellyGridHeight];
        }
    }

    private void InitializeJellyGrid()
    {
        _jellyCellConfigs = new CellConfig[_jellyGridWidth * _jellyGridHeight];
        _selectedJellyCellIndex = -1;
        Repaint();
    }

    private void ResizeJellyGrid(int newWidth, int newHeight)
    {
        CellConfig[] newConfigs = new CellConfig[newWidth * newHeight];
        if (_jellyCellConfigs != null)
        {
            int copyWidth = Mathf.Min(_jellyGridWidth, newWidth);
            int copyHeight = Mathf.Min(_jellyGridHeight, newHeight);
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    newConfigs[y * newWidth + x] = _jellyCellConfigs[y * _jellyGridWidth + x];
                }
            }
        }

        _jellyGridWidth = newWidth;
        _jellyGridHeight = newHeight;
        _jellyCellConfigs = newConfigs;
        if (_selectedJellyCellIndex >= _jellyCellConfigs.Length)
        {
            _selectedJellyCellIndex = -1;
        }
    }

    private bool HasValidSelectedJellyCell()
    {
        return _jellyCellConfigs != null &&
               _selectedJellyCellIndex >= 0 &&
               _selectedJellyCellIndex < _jellyCellConfigs.Length &&
               _jellyCellConfigs[_selectedJellyCellIndex].IsPlayable;
    }

    private void OnJellyCellClicked(int index)
    {
        if (_jellyCellConfigs == null || index < 0 || index >= _jellyCellConfigs.Length)
        {
            return;
        }

        // Reroute clicks on a reserved helper cell to its owning Stack anchor.
        Dictionary<int, int> helperOwners = ComputeStackHelperOwners();
        if (helperOwners.TryGetValue(index, out int ownerAnchorIndex))
        {
            _selectedJellyCellIndex = ownerAnchorIndex;
            Repaint();
            return;
        }

        if (_jellyIsPainting)
        {
            // Already-painted cell: just select it for editing instead of overwriting the brush on top.
            if (_jellyCellConfigs[index].IsPlayable)
            {
                _selectedJellyCellIndex = index;
                Repaint();
                return;
            }

            if (_colorDatabase == null)
            {
                ShowNotification(new GUIContent("Assign Material Color Database (project) to paint Jelly cells."));
                return;
            }

            if (!TryGetPrimaryColorTypeForBrush(_selectedJellyBrushColor, out ColorType brushColorType))
            {
                ShowNotification(new GUIContent($"No material entry maps to {_selectedJellyBrushColor} in the database."));
                return;
            }

            FreeStackHelperIfAnchor(index);

            CellConfig config = _jellyCellConfigs[index];
            config.IsPlayable = true;
            config.CellColor = brushColorType;
            config.Type = PlayableCellType.Normal;
            config.LinkGroupId = -1;
            config.FreezeValue = 0;
            config.StackItems = null;
            config.HelperDx = 0;
            config.HelperDy = 0;

            int desiredBullets = 1;
            int clamped = ClampBulletCountForCell(index, brushColorType, desiredBullets);
            if (clamped <= 0)
            {
                ShowNotification(new GUIContent($"No Jelly quota left for {_selectedJellyBrushColor}."));
                return;
            }

            config.BulletNum = clamped;
            _jellyCellConfigs[index] = config;
            _selectedJellyCellIndex = index;
        }
        else
        {
            FreeStackHelperIfAnchor(index);
            _jellyCellConfigs[index] = default;
            if (_selectedJellyCellIndex == index)
            {
                _selectedJellyCellIndex = -1;
            }
        }

        Repaint();
    }

    private int CountLockedJellyCells()
    {
        if (_jellyCellConfigs == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig config = _jellyCellConfigs[i];
            if (config.IsPlayable && config.Type == PlayableCellType.Locked)
            {
                count++;
            }
        }
        return count;
    }

    private Dictionary<int, int> ComputeStackHelperOwners()
    {
        Dictionary<int, int> owners = new Dictionary<int, int>();
        if (_jellyCellConfigs == null)
        {
            return owners;
        }

        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig c = _jellyCellConfigs[i];
            if (!c.IsPlayable || c.Type != PlayableCellType.Stack)
            {
                continue;
            }

            if (TryGetStackHelperIndex(i, c, out int helperIndex))
            {
                if (!owners.ContainsKey(helperIndex))
                {
                    owners[helperIndex] = i;
                }
            }
        }

        return owners;
    }

    private bool TryGetStackHelperIndex(int anchorIndex, CellConfig anchor, out int helperIndex)
    {
        helperIndex = -1;
        if (anchor.Type != PlayableCellType.Stack)
        {
            return false;
        }

        if (Mathf.Abs(anchor.HelperDx) + Mathf.Abs(anchor.HelperDy) != 1)
        {
            return false;
        }

        int ax = anchorIndex % _jellyGridWidth;
        int ay = anchorIndex / _jellyGridWidth;
        int hx = ax + anchor.HelperDx;
        int hy = ay + anchor.HelperDy;
        if (hx < 0 || hx >= _jellyGridWidth || hy < 0 || hy >= _jellyGridHeight)
        {
            return false;
        }

        helperIndex = hy * _jellyGridWidth + hx;
        return true;
    }

    private bool TryPickDefaultHelperDirection(int anchorIndex, out int dx, out int dy)
    {
        Dictionary<int, int> owners = ComputeStackHelperOwners();
        int ax = anchorIndex % _jellyGridWidth;
        int ay = anchorIndex / _jellyGridWidth;

        // Clockwise: right, down, left, up.
        int[] candidatesDx = { 1, 0, -1, 0 };
        int[] candidatesDy = { 0, -1, 0, 1 };
        for (int i = 0; i < 4; i++)
        {
            int cdx = candidatesDx[i];
            int cdy = candidatesDy[i];
            if (IsHelperDirectionValid(anchorIndex, cdx, cdy, owners))
            {
                dx = cdx;
                dy = cdy;
                return true;
            }
        }

        dx = 0;
        dy = 0;
        return false;
    }

    private bool IsHelperDirectionValid(int anchorIndex, int dx, int dy, Dictionary<int, int> owners)
    {
        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1)
        {
            return false;
        }

        int ax = anchorIndex % _jellyGridWidth;
        int ay = anchorIndex / _jellyGridWidth;
        int nx = ax + dx;
        int ny = ay + dy;
        if (nx < 0 || nx >= _jellyGridWidth || ny < 0 || ny >= _jellyGridHeight)
        {
            return false;
        }

        int nIdx = ny * _jellyGridWidth + nx;
        if (nIdx == anchorIndex)
        {
            return false;
        }

        CellConfig n = _jellyCellConfigs[nIdx];
        if (n.IsPlayable && n.Type == PlayableCellType.Stack)
        {
            return false;
        }

        if (owners != null && owners.TryGetValue(nIdx, out int owner) && owner != anchorIndex)
        {
            return false;
        }

        return true;
    }

    private void ReserveHelperCell(int helperIndex)
    {
        if (_jellyCellConfigs == null || helperIndex < 0 || helperIndex >= _jellyCellConfigs.Length)
        {
            return;
        }

        CellConfig c = _jellyCellConfigs[helperIndex];
        c.IsPlayable = true;
        c.Type = PlayableCellType.Normal;
        c.LinkGroupId = -1;
        c.FreezeValue = 0;
        c.StackItems = null;
        c.BulletNum = 0;
        c.HelperDx = 0;
        c.HelperDy = 0;
        _jellyCellConfigs[helperIndex] = c;
    }

    private void FreeHelperCell(int helperIndex)
    {
        if (_jellyCellConfigs == null || helperIndex < 0 || helperIndex >= _jellyCellConfigs.Length)
        {
            return;
        }

        _jellyCellConfigs[helperIndex] = default;
    }

    private void FreeStackHelperIfAnchor(int anchorIndex)
    {
        if (_jellyCellConfigs == null || anchorIndex < 0 || anchorIndex >= _jellyCellConfigs.Length)
        {
            return;
        }

        CellConfig anchor = _jellyCellConfigs[anchorIndex];
        if (!anchor.IsPlayable || anchor.Type != PlayableCellType.Stack)
        {
            return;
        }

        if (TryGetStackHelperIndex(anchorIndex, anchor, out int helperIndex))
        {
            FreeHelperCell(helperIndex);
        }
    }

    private void ReserveAllStackHelpers()
    {
        if (_jellyCellConfigs == null)
        {
            return;
        }

        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig anchor = _jellyCellConfigs[i];
            if (!anchor.IsPlayable || anchor.Type != PlayableCellType.Stack)
            {
                continue;
            }

            if (TryGetStackHelperIndex(i, anchor, out int helperIndex) && helperIndex != i)
            {
                ReserveHelperCell(helperIndex);
            }
        }
    }

    private Dictionary<int, List<int>> BuildLinkGroups()
    {
        Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
        if (_jellyCellConfigs == null)
        {
            return groups;
        }

        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig config = _jellyCellConfigs[i];
            if (!config.IsPlayable || config.Type != PlayableCellType.Link || config.LinkGroupId < 0)
            {
                continue;
            }

            if (!groups.TryGetValue(config.LinkGroupId, out List<int> list))
            {
                list = new List<int>();
                groups.Add(config.LinkGroupId, list);
            }

            list.Add(i);
        }

        return groups;
    }

    private List<string> CollectLinkValidationIssues()
    {
        List<string> issues = new List<string>();
        if (_jellyCellConfigs == null)
        {
            return issues;
        }

        Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig config = _jellyCellConfigs[i];
            if (!config.IsPlayable || config.Type != PlayableCellType.Link)
            {
                continue;
            }

            if (config.LinkGroupId < 0)
            {
                int x = i % _jellyGridWidth;
                int y = i / _jellyGridWidth;
                issues.Add($"Link cell ({x}, {y}) has invalid LinkGroupId {config.LinkGroupId}.");
                continue;
            }

            if (!groups.TryGetValue(config.LinkGroupId, out List<int> cells))
            {
                cells = new List<int>();
                groups.Add(config.LinkGroupId, cells);
            }

            cells.Add(i);
        }

        foreach (KeyValuePair<int, List<int>> group in groups)
        {
            if (group.Value.Count != 2)
            {
                issues.Add($"LinkGroupId {group.Key} has {group.Value.Count} cells (expected 2).");
            }
        }

        return issues;
    }

    private void SanitizeJellyCellConfigs()
    {
        if (_jellyCellConfigs == null)
        {
            return;
        }

        Dictionary<int, List<int>> originalLinkGroups = BuildLinkGroups();
        int normalizedLinkGroupId = 0;

        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig config = _jellyCellConfigs[i];
            if (config.Type != PlayableCellType.Link)
            {
                config.LinkGroupId = -1;
            }

            if (config.Type == PlayableCellType.Freeze)
            {
                config.FreezeValue = Mathf.Clamp(config.FreezeValue, 1, MaxFreezeValue);
            }
            else
            {
                config.FreezeValue = 0;
            }

            config.TimingSeconds = Mathf.Max(0, config.TimingSeconds);

            if (config.Type == PlayableCellType.Stack)
            {
                EnsureStackItemsInitialized(ref config, i);
                config.BulletNum = GetStackTotalBulletCount(config);
                if (config.StackItems.Length > 0)
                {
                    config.CellColor = config.StackItems[0].CellColor;
                }

                if (config.HelperDx == 0 && config.HelperDy == 0)
                {
                    config.HelperDx = 1;
                    config.HelperDy = 0;
                }
            }
            else
            {
                config.StackItems = null;
                config.HelperDx = 0;
                config.HelperDy = 0;
            }

            _jellyCellConfigs[i] = config;
        }

        foreach (KeyValuePair<int, List<int>> group in originalLinkGroups)
        {
            if (group.Value == null || group.Value.Count != 2)
            {
                continue;
            }

            for (int i = 0; i < group.Value.Count; i++)
            {
                int index = group.Value[i];
                CellConfig config = _jellyCellConfigs[index];
                config.LinkGroupId = normalizedLinkGroupId;
                _jellyCellConfigs[index] = config;
            }

            normalizedLinkGroupId++;
        }

        ReserveAllStackHelpers();
    }

    private string BuildCellTypeSaveSummary()
    {
        if (_jellyCellConfigs == null)
        {
            return "Cell Type Summary: no Jelly grid.";
        }

        int normal = 0;
        int hidden = 0;
        int link = 0;
        int freeze = 0;
        int locked = 0;
        int stack = 0;

        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig config = _jellyCellConfigs[i];
            if (!config.IsPlayable)
            {
                continue;
            }

            switch (config.Type)
            {
                case PlayableCellType.Hidden:
                    hidden++;
                    break;
                case PlayableCellType.Link:
                    link++;
                    break;
                case PlayableCellType.Freeze:
                    freeze++;
                    break;
                case PlayableCellType.Locked:
                    locked++;
                    break;
                case PlayableCellType.Stack:
                    stack++;
                    break;
                default:
                    normal++;
                    break;
            }
        }

        return $"Cell Type Summary: Normal={normal}, Hidden={hidden}, Link={link}, Freeze={freeze}, Locked={locked}, Stack={stack}";
    }

    private int CountJellyAmmoForColor(PixelCubeColor color)
    {
        int count = 0;
        if (_jellyCellConfigs == null)
        {
            return 0;
        }

        for (int i = 0; i < _jellyCellConfigs.Length; i++)
        {
            CellConfig config = _jellyCellConfigs[i];
            if (!config.IsPlayable)
            {
                continue;
            }

            if (config.Type == PlayableCellType.Stack)
            {
                count += GetStackAmmoContribution(config, color);
                continue;
            }

            if (config.BulletNum <= 0)
            {
                continue;
            }

            if (!TryMapColorTypeToPixelColor(config.CellColor, out PixelCubeColor mappedColor))
            {
                continue;
            }

            if (mappedColor == color)
            {
                count += config.BulletNum;
            }
        }

        return count;
    }

    private int GetJellyRemaining(PixelCubeColor color)
    {
        return GetTargetCount(color) - CountJellyAmmoForColor(color);
    }

    private int GetJellyRemainingExcludingCell(int index, PixelCubeColor color)
    {
        int usedByOthers = CountJellyAmmoForColor(color) - GetJellyAmmoContribution(index, color);
        return Mathf.Max(0, GetTargetCount(color) - usedByOthers);
    }

    private static int GetStackAmmoContribution(CellConfig config, PixelCubeColor mappedColor)
    {
        int count = 0;
        if (config.StackItems == null)
        {
            return 0;
        }

        for (int i = 0; i < config.StackItems.Length; i++)
        {
            JellyStackItem item = config.StackItems[i];
            if (item.BulletNum <= 0)
            {
                continue;
            }

            if (TryMapColorTypeToPixelColor(item.CellColor, out PixelCubeColor itemMappedColor) &&
                itemMappedColor == mappedColor)
            {
                count += item.BulletNum;
            }
        }

        return count;
    }

    private static int GetStackTotalBulletCount(CellConfig config)
    {
        int count = 0;
        if (config.StackItems == null)
        {
            return Mathf.Max(0, config.BulletNum);
        }

        for (int i = 0; i < config.StackItems.Length; i++)
        {
            count += Mathf.Max(0, config.StackItems[i].BulletNum);
        }

        return count;
    }

    private int ClampBulletCountForCell(int index, ColorType colorType, int requestedValue)
    {
        int maxAllowed = GetMaxBulletCountForCell(index, colorType);
        return Mathf.Clamp(requestedValue, 0, Mathf.Max(0, maxAllowed));
    }

    private int GetMaxBulletCountForCell(int index, ColorType colorType)
    {
        if (!TryMapColorTypeToPixelColor(colorType, out PixelCubeColor mappedColor))
        {
            return 0;
        }

        int usedByOthers = CountJellyAmmoForColor(mappedColor) - GetJellyAmmoContribution(index, mappedColor);
        return Mathf.Max(0, GetTargetCount(mappedColor) - usedByOthers);
    }

    private int GetJellyAmmoContribution(int index, PixelCubeColor mappedColor)
    {
        if (_jellyCellConfigs == null || index < 0 || index >= _jellyCellConfigs.Length)
        {
            return 0;
        }

        CellConfig config = _jellyCellConfigs[index];
        if (!config.IsPlayable)
        {
            return 0;
        }

        if (config.Type == PlayableCellType.Stack)
        {
            return GetStackAmmoContribution(config, mappedColor);
        }

        if (config.BulletNum <= 0)
        {
            return 0;
        }

        if (!TryMapColorTypeToPixelColor(config.CellColor, out PixelCubeColor cellMappedColor))
        {
            return 0;
        }

        return cellMappedColor == mappedColor ? config.BulletNum : 0;
    }

    private void EnsureStackItemsInitialized(ref CellConfig config, int cellIndex)
    {
        if (config.StackItems != null && config.StackItems.Length > 0)
        {
            return;
        }

        config.StackItems = BuildStackItemsFromRemainingColorQuota(cellIndex, config);
        config.BulletNum = GetStackTotalBulletCount(config);
    }

    private JellyStackItem[] BuildStackItemsFromRemainingColorQuota(int cellIndex, CellConfig fallbackConfig)
    {
        List<JellyStackItem> items = new List<JellyStackItem>();
        for (int i = 0; i < ManagedColors.Length; i++)
        {
            PixelCubeColor color = ManagedColors[i];
            int remaining = GetJellyRemainingExcludingCell(cellIndex, color);
            if (remaining <= 0 || !TryGetPrimaryColorTypeForBrush(color, out ColorType colorType))
            {
                continue;
            }

            items.Add(new JellyStackItem
            {
                CellColor = colorType,
                BulletNum = remaining,
            });
        }

        if (items.Count > 0)
        {
            return items.ToArray();
        }

        return new[]
        {
            new JellyStackItem
            {
                CellColor = fallbackConfig.CellColor,
                BulletNum = Mathf.Max(1, fallbackConfig.BulletNum),
            }
        };
    }

    private static void AddStackItem(ref CellConfig config)
    {
        JellyStackItem[] source = config.StackItems ?? new JellyStackItem[0];
        JellyStackItem[] next = new JellyStackItem[source.Length + 1];
        Array.Copy(source, next, source.Length);
        next[next.Length - 1] = new JellyStackItem
        {
            CellColor = source.Length > 0 ? source[source.Length - 1].CellColor : config.CellColor,
            BulletNum = 1,
        };
        config.StackItems = next;
    }

    private static void RemoveStackItem(ref CellConfig config, int index)
    {
        JellyStackItem[] source = config.StackItems;
        if (source == null || source.Length <= 1 || index < 0 || index >= source.Length)
        {
            return;
        }

        JellyStackItem[] next = new JellyStackItem[source.Length - 1];
        int writeIndex = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (i == index)
            {
                continue;
            }

            next[writeIndex] = source[i];
            writeIndex++;
        }

        config.StackItems = next;
    }

    private static void SwapStackItems(JellyStackItem[] items, int a, int b)
    {
        if (items == null || a < 0 || b < 0 || a >= items.Length || b >= items.Length)
        {
            return;
        }

        JellyStackItem temp = items[a];
        items[a] = items[b];
        items[b] = temp;
    }
}
