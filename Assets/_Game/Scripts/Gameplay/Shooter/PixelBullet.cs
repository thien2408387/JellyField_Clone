using UnityEngine;

public class PixelBullet : MonoBehaviour
{
    public void AbortFlightForLostTarget()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}
