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

    public static bool TryResolveAimPose3D(GameObject owner, out Vector3 origin, out Vector3 direction)
    {
        origin = owner != null ? owner.transform.position : Vector3.zero;
        direction = owner != null ? owner.transform.forward : Vector3.forward;

        if (owner == null)
            return false;

        PlayerShooting shooting = owner.GetComponent<PlayerShooting>();
        if (shooting != null && (!shooting.IsSpawned || shooting.IsOwner) &&
            shooting.TryGetShotPose(out origin, out direction))
        {
            direction = direction.normalized;
            return true;
        }

        CameraController cameraController = owner.GetComponentInChildren<CameraController>(true);
        if (cameraController != null && (!cameraController.IsSpawned || cameraController.IsOwner))
        {
            direction = cameraController.GetAimDirection();
            if (direction.sqrMagnitude > 0.0001f)
            {
                Transform source = ResolveAimTransform(owner);
                origin = source != null ? source.position : owner.transform.position;
                direction.Normalize();
                return true;
            }
        }

        Transform aimTransform = ResolveAimTransform(owner);
        if (aimTransform != null)
        {
            origin = aimTransform.position;
            direction = aimTransform.forward;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = owner.transform.forward;

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    public static Vector3 ResolveAimDirection3D(GameObject owner)
    {
        return TryResolveAimPose3D(owner, out _, out Vector3 direction) ? direction : Vector3.forward;
    }

    public static Quaternion ResolveAimRotation3D(GameObject owner)
    {
        Vector3 forward = ResolveAimDirection3D(owner);
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
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
