using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Contracts;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RotateKey;

public class RotateKeyHandler(
    IApiKeyRepository keyRepository,
    IAccessPolicyService accessPolicyService,
    IApiKeyHasher keyHasher,
    TimeProvider clock
) : IRotateKeyHandler
{
    // api-spec.md §3.2.4: "未提供則使用系統預設值（24 小時）" (2026-07-12 使用者裁決).
    private static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromHours(24);

    public async Task<Result<RotateKeyResponse, Failure>> HandleAsync(
        RotateKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: key exists (tenantId + keyId)
        var keyA = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (keyA is null)
            return FailureProvider.CreateFailure(RotateKeyFailureCodes.KeyNotFound);

        // 2. Guard: status = Active
        //    detailed-design §6.2 guard order also has "尚未到期" (KEY_ALREADY_EXPIRED) and
        //    INV-4 "無其他 Rotating" (ROTATION_IN_PROGRESS) between here and step 3 below — both
        //    deferred to scenarios 36/37 (no red to drive them in this scenario), to be inserted
        //    at this position when their scenarios land.
        if (keyA.Status != ApiKeyStatus.Active)
            return FailureProvider.CreateFailure(RotateKeyFailureCodes.InvalidStateTransition);

        // 3. Create AccessPolicy (I2) for Key B — gets policyId before creating the key, same
        //    call-time sequencing as CreateApiKeyHandler (same DI scope / DbContext = same
        //    transaction).
        var policyId = await accessPolicyService.CreateDefaultPolicyAsync(
            Guid.NewGuid(), command.TenantId, cancel);

        // 4. Create Key B — inherits name/scopes/environment/expiresAt from Key A (rotation only
        //    replaces credential material, not the validity window — 2026-07-12 使用者裁決).
        var (keyB, rawKey) = ApiKey.Create(
            keyA.ConsumerId,
            command.TenantId,
            keyA.Name,
            keyA.Environment,
            keyA.Scopes,
            keyA.ExpiresAt,
            policyId,
            keyHasher);

        // 5. Transition Key A → Rotating + link successor; link Key B's predecessor.
        var now = clock.GetUtcNow();
        keyA.InitiateRotation(keyB.Id, command.GracePeriod ?? DefaultGracePeriod, now);
        keyB.SetPredecessor(keyA.Id);

        // 6. Persist both aggregates in the same transaction.
        await keyRepository.UpdateAsync(keyA, cancel);
        await keyRepository.SaveAsync(keyB, cancel);

        return new RotateKeyResponse(
            OldKey: new RotateKeyResponse.OldKeyInfo(
                KeyId: keyA.Id,
                LifecycleStatus: keyA.Status,
                GraceDeadline: keyA.GraceDeadline!.Value,
                SuccessorKeyId: keyA.SuccessorKeyId!.Value),
            NewKey: new RotateKeyResponse.NewKeyInfo(
                KeyId: keyB.Id,
                Name: keyB.Name,
                KeyPrefix: keyB.KeyPrefix,
                // Display-safe suffix for identifying the key after rawKey is shown once (api-spec.md §2.2).
                TruncatedKey: "..." + rawKey[^4..],
                Environment: keyB.Environment,
                Scopes: keyB.Scopes,
                LifecycleStatus: keyB.Status,
                PolicyId: keyB.PolicyId,
                CreatedAt: keyB.CreatedAt,
                ExpiresAt: keyB.ExpiresAt,
                PredecessorKeyId: keyB.PredecessorKeyId!.Value,
                RawKey: rawKey));
    }
}
