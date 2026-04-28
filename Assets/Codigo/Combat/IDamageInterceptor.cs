public interface IDamageInterceptor
{
    bool TryIntercept(PlayerHealthSystem target, ref DamageRequest request, ref DamageResponse response);
}
