using UnityEngine;

public static class AbilityAimUtility
{
    public static Transform ResolveAimTransform(GameObject owner)
    {
        if (owner == null)
            return null;

        PlayerMovement playerMovement = owner.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            Transform modelPivot = playerMovement.GetModelPivot();
            if (modelPivot != null)
                return modelPivot;
        }

        PlayerShooting shooting = owner.GetComponent<PlayerShooting>();
        if (shooting != null && shooting.firePoint != null)
            return shooting.firePoint;

        return owner.transform;
    }

    public static Vector3 ResolveAimForward(GameObject owner)
    {
        return ResolveFlatForward(ResolveAimTransform(owner));
    }

    public static Quaternion ResolveAimRotation(GameObject owner)
    {
        Vector3 forward = ResolveAimForward(owner);
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
    }

    public static Vector3 ResolveFlatForward(Transform source)
    {
        if (source == null)
            return Vector3.forward;

        Vector3 forward = source.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }
}
