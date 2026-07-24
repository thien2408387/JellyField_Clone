using System;
using KingCat.Base.Assets;
using UnityEngine;

[Serializable]
public struct CellConfig
{
    public bool IsPlayable;
    public int BulletNum;
    public ColorType CellColor; //my custom class for assigning color of a cell, so just ignore it
    public PlayableCellType Type;
    public int FreezeValue;
    public int LinkGroupId;
    public int TimingSeconds;
    public JellyStackItem[] StackItems;

    // Stack-only: offset (in grid cells) from the anchor to its helper neighbor.
    // Valid combinations are unit vectors: (+1,0), (-1,0), (0,+1), (0,-1).
    public int HelperDx;
    public int HelperDy;
}

[Serializable]
public struct JellyStackItem
{
    public ColorType CellColor;
    public int BulletNum;
}

public enum PlayableCellType : byte
{
   Normal = 0,
   Hidden = 1,
   Link = 2,
   Freeze = 3,
   Locked = 4,
   Stack = 5
}
