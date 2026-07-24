using UnityEngine;

public class LockJellyBlock : MonoBehaviour
{
    [SerializeField] private bool _isLocked = true;
    [SerializeField] private Transform _keyHoleTransform;

    public bool IsLocked => _isLocked;
    public Vector3 KeyHolePosition => _keyHoleTransform != null ? _keyHoleTransform.position : transform.position;

    public void Unlock()
    {
        _isLocked = false;
    }
}
