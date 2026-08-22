using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Tests.Security;

public sealed class ProductionSecurityReadinessFoundationTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 8, 30, 0, TimeSpan.Zero);
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly CanonicalVendorAuthorizationPayloadSerializer _serializer = new();

    [Fact]
    public void Canonical_Payload_Bytes_Are_Stable_Ordered_And_Utf8()
    {
        byte[] first = _serializer.SerializeCanonical(Payload(12.50m));
        byte[] second = _serializer.SerializeCanonical(Payload(12.5000m));
        const string expected = "{\"payloadVersion\":\"1\",\"deviceId\":\"device-01\",\"requestId\":\"request-01\",\"action\":\"ChangeEsdAdjustment\",\"proposedEsdAdjustment\":\"12.5\",\"issuedAtUtc\":\"2026-08-24T08:29:00.0000000Z\",\"expiresAtUtc\":\"2026-08-24T08:35:00.0000000Z\"}";
        Assert.Equal(first, second);
        Assert.Equal(expected, Encoding.UTF8.GetString(first));
        Assert.True(_serializer.TryDeserializeCanonical(first, out VendorAuthorizationPayload? roundTrip));
        Assert.Equal(Payload(12.5m), roundTrip);
    }

    [Fact]
    public void Canonical_Decimal_Is_Culture_Invariant()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new("fa-IR");
            byte[] persian = _serializer.SerializeCanonical(Payload(1234.50m));
            CultureInfo.CurrentCulture = new("de-DE");
            byte[] german = _serializer.SerializeCanonical(Payload(1234.50m));
            Assert.Equal(persian, german);
            Assert.Contains("\"1234.5\"", Encoding.UTF8.GetString(persian), StringComparison.Ordinal);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Noncanonical_Property_Order_Is_Rejected()
    {
        byte[] reordered = Encoding.UTF8.GetBytes("{\"deviceId\":\"device-01\",\"payloadVersion\":\"1\",\"requestId\":\"request-01\",\"action\":\"ChangeEsdAdjustment\",\"proposedEsdAdjustment\":\"12.5\",\"issuedAtUtc\":\"2026-08-24T08:29:00.0000000Z\",\"expiresAtUtc\":\"2026-08-24T08:35:00.0000000Z\"}");
        Assert.False(_serializer.TryDeserializeCanonical(reordered, out _));
    }

    [Fact]
    public async Task Valid_Ecdsa_P256_Signature_Is_Accepted()
    {
        Fixture f = FixtureWithKey();
        Assert.IsAssignableFrom<IVendorAuthorizationVerifier>(f.Verifier);
        VendorAuthorizationVerificationResult result = await f.Verifier.VerifyAsync(Payload(), Sign(Payload(), "key-1").AsMemory(), Now);
        Assert.True(result.IsValid);
        Assert.Equal("key-1", result.KeyId);
    }

    [Theory]
    [InlineData("payload", VendorAuthorizationVerificationFailure.UnsupportedPayloadVersion)]
    [InlineData("envelope", VendorAuthorizationVerificationFailure.UnsupportedEnvelopeVersion)]
    public async Task Unsupported_Versions_Fail_Explicitly(string kind, VendorAuthorizationVerificationFailure expected)
    {
        Fixture f = FixtureWithKey();
        VendorAuthorizationPayload payload = kind == "payload" ? Payload() with { PayloadVersion = "99" } : Payload();
        string envelope = Sign(payload, "key-1", kind == "envelope" ? "99" : "1");
        Assert.Equal(expected, (await f.Verifier.VerifyAsync(payload, envelope.AsMemory(), Now)).Failure);
    }

    [Fact]
    public async Task Expected_Payload_Version_Mismatch_Fails_Closed()
    {
        Fixture f = FixtureWithKey();
        VendorAuthorizationPayload expected = Payload() with { PayloadVersion = "99" };
        Assert.Equal(VendorAuthorizationVerificationFailure.UnsupportedPayloadVersion,
            (await f.Verifier.VerifyAsync(expected, Sign(Payload(), "key-1").AsMemory(), Now)).Failure);
    }

    [Fact]
    public async Task Non_Esd_Action_Fails_Even_When_Expected_And_Signed_Action_Match()
    {
        Fixture f = FixtureWithKey();
        VendorAuthorizationPayload wrongAction = Payload() with { Action = VendorSupportAction.Unspecified };
        Assert.Equal(VendorAuthorizationVerificationFailure.WrongAction,
            (await f.Verifier.VerifyAsync(wrongAction, Sign(wrongAction, "key-1").AsMemory(), Now)).Failure);
    }

    [Fact]
    public async Task Invalid_Signature_And_Wrong_Public_Key_Fail()
    {
        Fixture f = FixtureWithKey();
        string valid = Sign(Payload(), "key-1");
        VendorSignedAuthorizationEnvelopeCodec.TryDecode(valid.AsMemory(), out VendorSignedAuthorizationEnvelope? decoded);
        decoded!.Signature[0] ^= 0x01;
        Assert.Equal(VendorAuthorizationVerificationFailure.InvalidSignature,
            (await f.Verifier.VerifyAsync(Payload(), VendorSignedAuthorizationEnvelopeCodec.Encode(decoded).AsMemory(), Now)).Failure);

        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        f.Keys.Key = new("key-1", other.ExportSubjectPublicKeyInfo(), Now.AddDays(-1), null);
        Assert.Equal(VendorAuthorizationVerificationFailure.InvalidSignature,
            (await f.Verifier.VerifyAsync(Payload(), valid.AsMemory(), Now)).Failure);
    }

    [Fact]
    public async Task Unknown_And_Retired_Keys_Fail()
    {
        Fixture unknown = FixtureWithKey();
        unknown.Keys.Key = null;
        Assert.Equal(VendorAuthorizationVerificationFailure.UnknownKeyId,
            (await unknown.Verifier.VerifyAsync(Payload(), Sign(Payload(), "missing").AsMemory(), Now)).Failure);

        Fixture retired = FixtureWithKey();
        retired.Keys.Key = new("key-1", _signingKey.ExportSubjectPublicKeyInfo(), Now.AddDays(-1), Now);
        Assert.Equal(VendorAuthorizationVerificationFailure.KeyNotActive,
            (await retired.Verifier.VerifyAsync(Payload(), Sign(Payload(), "key-1").AsMemory(), Now)).Failure);
    }

    [Fact]
    public void Trusted_Key_Lifecycle_Is_Validated_And_Public_Bytes_Are_Defensively_Copied()
    {
        byte[] publicBytes = _signingKey.ExportSubjectPublicKeyInfo();
        var trusted = new TrustedVendorPublicKey("key-1", publicBytes, Now.AddDays(-1), Now.AddDays(1));
        publicBytes[0] ^= 0x01;
        byte[] exported = trusted.SubjectPublicKeyInfo.ToArray();
        Assert.NotEqual(publicBytes, exported);
        exported[0] ^= 0x01;
        Assert.NotEqual(exported, trusted.SubjectPublicKeyInfo.ToArray());
        Assert.Throws<ArgumentException>(() => new TrustedVendorPublicKey(
            "key-1", publicBytes, Now, Now.AddMinutes(-1)));
    }

    [Fact]
    public async Task Malformed_Envelope_And_Payload_Fail_Without_Exception_Text()
    {
        Fixture f = FixtureWithKey();
        Assert.Equal(VendorAuthorizationVerificationFailure.MalformedEnvelope,
            (await f.Verifier.VerifyAsync(Payload(), "not-json".AsMemory(), Now)).Failure);
        var malformed = new VendorSignedAuthorizationEnvelope("key-1", "1", Encoding.UTF8.GetBytes("{}"), [1, 2, 3]);
        VendorAuthorizationVerificationResult result = await f.Verifier.VerifyAsync(Payload(),
            VendorSignedAuthorizationEnvelopeCodec.Encode(malformed).AsMemory(), Now);
        Assert.Equal(VendorAuthorizationVerificationFailure.MalformedPayload, result.Failure);
        Assert.DoesNotContain("Exception", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> BindingFailures()
    {
        yield return [new Func<VendorAuthorizationPayload, VendorAuthorizationPayload>(p => p with { DeviceId = "wrong" }), VendorAuthorizationVerificationFailure.WrongDevice];
        yield return [new Func<VendorAuthorizationPayload, VendorAuthorizationPayload>(p => p with { RequestId = "wrong" }), VendorAuthorizationVerificationFailure.WrongRequest];
        yield return [new Func<VendorAuthorizationPayload, VendorAuthorizationPayload>(p => p with { Action = VendorSupportAction.Unspecified }), VendorAuthorizationVerificationFailure.WrongAction];
        yield return [new Func<VendorAuthorizationPayload, VendorAuthorizationPayload>(p => p with { ProposedEsdAdjustment = 99m }), VendorAuthorizationVerificationFailure.WrongProposedValue];
        yield return [new Func<VendorAuthorizationPayload, VendorAuthorizationPayload>(p => p with { ExpiresAtUtc = p.ExpiresAtUtc.AddMinutes(1) }), VendorAuthorizationVerificationFailure.WrongExpiry];
    }

    [Theory]
    [MemberData(nameof(BindingFailures))]
    public async Task Signed_Payload_Binding_Mismatches_Fail(Func<VendorAuthorizationPayload, VendorAuthorizationPayload> mutate,
        VendorAuthorizationVerificationFailure expectedFailure)
    {
        Fixture f = FixtureWithKey();
        VendorAuthorizationPayload expected = Payload();
        VendorAuthorizationPayload signed = mutate(expected);
        Assert.Equal(expectedFailure, (await f.Verifier.VerifyAsync(expected, Sign(signed, "key-1").AsMemory(), Now)).Failure);
    }

    [Fact]
    public async Task Expired_And_Future_Issued_Authorizations_Fail()
    {
        Fixture f = FixtureWithKey();
        VendorAuthorizationPayload expired = Payload() with { ExpiresAtUtc = Now };
        Assert.Equal(VendorAuthorizationVerificationFailure.Expired,
            (await f.Verifier.VerifyAsync(expired, Sign(expired, "key-1").AsMemory(), Now)).Failure);
        VendorAuthorizationPayload future = Payload() with { IssuedAtUtc = Now.AddSeconds(1), ExpiresAtUtc = Now.AddMinutes(5) };
        Assert.Equal(VendorAuthorizationVerificationFailure.IssuedInFuture,
            (await f.Verifier.VerifyAsync(future, Sign(future, "key-1").AsMemory(), Now)).Failure);
    }

    [Fact]
    public async Task Request_Factory_Uses_Stable_Device_And_Cryptographically_Unique_Nonces()
    {
        var factory = new VendorAuthorizationRequestFactory(new Device("deployment-opaque-id"), new Clock(Now), TimeSpan.FromMinutes(5));
        VendorAuthorizationRequestContext[] requests = await Task.WhenAll(Enumerable.Range(0, 128)
            .Select(i => factory.CreateEsdAdjustmentRequestAsync("shift-1", $"correlation-{i}", 1.25m)));
        Assert.Equal(128, requests.Select(x => x.Payload.RequestId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(requests, x => Assert.Equal("deployment-opaque-id", x.Payload.DeviceId));
        Assert.All(requests, x => Assert.Equal(64, x.Payload.RequestId.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VendorAuthorizationRequestFactory(new Device("d"), new Clock(Now), TimeSpan.Zero));
    }

    [Fact]
    public void Management_Proof_Issuer_Binds_All_Required_Evidence()
    {
        var issuer = new ManagementAuthorizationProofIssuer(new Clock(Now), TimeSpan.FromMinutes(3));
        ManagementAuthorizationProof proof = issuer.Issue("shift-1", ProtectedAction.Restore,
            "backup/restore", "correlation-9", new(11));
        Assert.Equal("shift-1", proof.InitiatingShiftProfileId);
        Assert.Equal(ProtectedAction.Restore, proof.Action);
        Assert.Equal("backup/restore", proof.ActionScope);
        Assert.Equal(11, proof.CredentialVersion);
        Assert.Equal(Now, proof.IssuedAt);
        Assert.Equal(Now.AddMinutes(3), proof.ExpiresAt);
        Assert.Equal("correlation-9", proof.CorrelationId);
        Assert.Throws<ArgumentOutOfRangeException>(() => issuer.Issue("shift-1", (ProtectedAction)999,
            "backup/restore", "correlation-9", new(11)));
    }

    [Theory]
    [InlineData("actor", ManagementProofFailure.WrongActor)]
    [InlineData("action", ManagementProofFailure.WrongAction)]
    [InlineData("scope", ManagementProofFailure.WrongScope)]
    [InlineData("correlation", ManagementProofFailure.WrongCorrelation)]
    [InlineData("version", ManagementProofFailure.CredentialVersionMismatch)]
    [InlineData("future", ManagementProofFailure.NotYetValid)]
    [InlineData("expired", ManagementProofFailure.Expired)]
    public void Management_Proof_Validation_Fails_Closed(string mismatch, ManagementProofFailure expected)
    {
        ManagementAuthorizationProof proof = Proof() with
        {
            InitiatingShiftProfileId = mismatch == "actor" ? "other" : "shift-1",
            Action = mismatch == "action" ? ProtectedAction.Restore : ProtectedAction.ChangeEsdAdjustment,
            ActionScope = mismatch == "scope" ? "other" : "station-1",
            CorrelationId = mismatch == "correlation" ? "other" : "correlation-1",
            CredentialVersion = mismatch == "version" ? 8 : 7,
            IssuedAt = mismatch == "future" ? Now.AddSeconds(1) : Now.AddMinutes(-1),
            ExpiresAt = mismatch == "expired" ? Now : Now.AddMinutes(4)
        };
        Assert.Equal(expected, ManagementAuthorizationProofValidator.Validate(proof, "shift-1",
            ProtectedAction.ChangeEsdAdjustment, "station-1", "correlation-1", 7, Now).Failure);
    }

    [Fact]
    public void Audit_Metadata_AllowList_Accepts_Safe_And_Rejects_Secrets()
    {
        IReadOnlyDictionary<string, string> safe = SecurityAuditMetadataBuilder.Create([
            new("DeviceId", "device"), new("RequestId", "request"), new("KeyId", "key")]);
        Assert.Equal(3, safe.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)safe).Add("Password", "sensitive"));
        string[] forbidden = ["Password", "PasswordHash", "Salt", "SignedAuthorization",
            "PrivateKey", "RecoveryCode", "CredentialSecret", "VerifierMaterial"];
        foreach (string key in forbidden)
            Assert.Throws<ArgumentException>(() => SecurityAuditMetadataBuilder.Create([new(key, "sensitive")]));
    }

    [Fact]
    public async Task Protected_Esd_Executes_Exactly_Once_And_Replay_Is_Rejected()
    {
        ExecutionFixture f = ExecutionFixture.Create(this);
        int mutations = 0;
        ProtectedEsdExecutionResult first = await f.Execute(() => mutations++);
        ProtectedEsdExecutionResult replay = await f.Execute(() => mutations++);
        Assert.True(first.Succeeded);
        Assert.False(replay.Succeeded);
        Assert.Equal(ProtectedEsdExecutionFailure.ReplayRejected, replay.Failure);
        Assert.Equal(1, mutations);
    }

    [Fact]
    public async Task Concurrent_Replay_Executes_Mutation_Exactly_Once()
    {
        ExecutionFixture f = ExecutionFixture.Create(this);
        int mutations = 0;
        ProtectedEsdExecutionResult[] results = await Task.WhenAll(
            f.Execute(() => Interlocked.Increment(ref mutations)),
            f.Execute(() => Interlocked.Increment(ref mutations)));
        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => x.Failure == ProtectedEsdExecutionFailure.ReplayRejected);
        Assert.Equal(1, mutations);
    }

    [Fact]
    public async Task Audit_Failure_And_Replay_Store_Failure_Suppress_Mutation()
    {
        ExecutionFixture auditFailure = ExecutionFixture.Create(this);
        auditFailure.Audit.Throw = true;
        int calls = 0;
        Assert.Equal(ProtectedEsdExecutionFailure.AuditFailed, (await auditFailure.Execute(() => calls++)).Failure);

        ExecutionFixture storeFailure = ExecutionFixture.Create(this);
        storeFailure.Atomic.Throw = true;
        Assert.Equal(ProtectedEsdExecutionFailure.ReplayStoreFailed, (await storeFailure.Execute(() => calls++)).Failure);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Vendor_Verification_Failure_Suppresses_Audit_Atomic_Boundary_And_Mutation()
    {
        ExecutionFixture f = ExecutionFixture.Create(this) with { Envelope = "malformed" };
        int calls = 0;
        ProtectedEsdExecutionResult result = await f.Execute(() => calls++);
        Assert.Equal(ProtectedEsdExecutionFailure.VendorAuthorizationRejected, result.Failure);
        Assert.Equal(0, calls);
        Assert.Equal(0, f.Audit.Calls);
        Assert.Equal(0, f.Atomic.Calls);
    }

    [Fact]
    public async Task Vendor_Verifier_Exception_Is_Converted_To_Safe_Closed_Failure()
    {
        var domain = new Domain();
        var audit = new Audit();
        var atomic = new Atomic();
        var service = new ProtectedEsdAdjustmentExecutionService(new ThrowingVerifier(), domain, audit, atomic);
        int calls = 0;
        ProtectedEsdExecutionResult result = await service.ExecuteAsync(Profile(), "station-1", 7,
            Proof(), new("shift-1", "correlation-1", Payload()), "opaque".AsMemory(), Now,
            _ => { calls++; return Task.CompletedTask; });
        Assert.Equal(ProtectedEsdExecutionFailure.VendorAuthorizationRejected, result.Failure);
        Assert.Equal(0, calls);
        Assert.Equal(0, audit.Calls);
        Assert.Equal(0, atomic.Calls);
    }

    [Fact]
    public async Task Test_InMemory_Consumed_Store_Preserves_Correlation_Time_And_Receipt()
    {
        var store = new MemoryConsumedStore();
        var consumption = new VendorAuthorizationConsumption("request", "correlation", Now, "receipt");
        Assert.False(await store.IsConsumedAsync("request", "correlation"));
        Assert.True(await store.TryConsumeAsync(consumption));
        Assert.True(await store.IsConsumedAsync("request", "correlation"));
        Assert.True(await store.IsConsumedAsync("request", "different-correlation"));
        Assert.False(await store.TryConsumeAsync(consumption));
        Assert.False(await store.TryConsumeAsync(consumption with { CorrelationId = "different-correlation" }));
        Assert.Equal(consumption, store.Last);
    }

    [Fact]
    public async Task Inactive_Profile_Invalid_Management_And_Domain_Failure_Suppress_Mutation()
    {
        int calls = 0;
        ExecutionFixture inactive = ExecutionFixture.Create(this) with { Profile = Profile() with { IsActive = false } };
        Assert.Equal(ProtectedEsdExecutionFailure.InactiveShiftProfile, (await inactive.Execute(() => calls++)).Failure);
        ExecutionFixture wrongStation = ExecutionFixture.Create(this) with
        {
            Profile = Profile() with { StationId = "station-2" }
        };
        Assert.Equal(ProtectedEsdExecutionFailure.ShiftProfileScopeMismatch,
            (await wrongStation.Execute(() => calls++)).Failure);
        ExecutionFixture management = ExecutionFixture.Create(this) with { Proof = Proof() with { CredentialVersion = 99 } };
        Assert.Equal(ProtectedEsdExecutionFailure.InvalidManagementProof, (await management.Execute(() => calls++)).Failure);
        ExecutionFixture domain = ExecutionFixture.Create(this);
        domain.Domain.Valid = false;
        Assert.Equal(ProtectedEsdExecutionFailure.DomainValidationRejected, (await domain.Execute(() => calls++)).Failure);
        ExecutionFixture unavailableDomain = ExecutionFixture.Create(this);
        unavailableDomain.Domain.Throw = true;
        Assert.Equal(ProtectedEsdExecutionFailure.DomainValidationRejected,
            (await unavailableDomain.Execute(() => calls++)).Failure);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Production_Contracts_Have_No_Private_Key_Or_Station_Specific_Identity()
    {
        Type[] securityTypes = typeof(VendorAuthorizationPayload).Assembly.GetTypes()
            .Where(x => x.Namespace?.Contains("Application.Security", StringComparison.Ordinal) == true).ToArray();
        string names = string.Join('|', securityTypes.Select(x => x.FullName)
            .Concat(securityTypes.SelectMany(x => x.GetProperties()).Select(x => x.Name)));
        Assert.DoesNotContain("PrivateKey", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rasht", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ramsar", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(securityTypes, x => x.Name.Contains("SupportLogin", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Contains("SupportProfile", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
    }

    private VendorAuthorizationPayload Payload(decimal value = 2.5m) => new("device-01", "request-01",
        VendorSupportAction.ChangeEsdAdjustment, value, Now.AddMinutes(-1), Now.AddMinutes(5));

    private string Sign(VendorAuthorizationPayload payload, string keyId, string envelopeVersion = "1")
    {
        byte[] bytes = _serializer.SerializeCanonical(payload);
        byte[] signature = _signingKey.SignData(bytes, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return VendorSignedAuthorizationEnvelopeCodec.Encode(new(keyId, envelopeVersion, bytes, signature));
    }

    private Fixture FixtureWithKey()
    {
        var keys = new Keys { Key = new("key-1", _signingKey.ExportSubjectPublicKeyInfo(), Now.AddDays(-1), null) };
        return new(keys, new(keys, _serializer));
    }

    private static ShiftProfile Profile() => new("shift-1", "station-1", 1, "Shift 1", "First", "Last",
        "1001", true, Now, Now, 1);
    private static ManagementAuthorizationProof Proof() => new("shift-1", ProtectedAction.ChangeEsdAdjustment,
        "station-1", 7, Now.AddMinutes(-1), Now.AddMinutes(4), "correlation-1");

    public void Dispose() => _signingKey.Dispose();

    private sealed record Fixture(Keys Keys, EcdsaP256VendorAuthorizationVerifier Verifier);
    private sealed class Keys : ITrustedVendorPublicKeyProvider
    {
        public TrustedVendorPublicKey? Key { get; set; }
        public Task<TrustedVendorPublicKey?> FindByKeyIdAsync(string keyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Key is not null && StringComparer.Ordinal.Equals(Key.KeyId, keyId) ? Key : null);
    }
    private sealed record Device(string Id) : IDeviceIdentityProvider
    {
        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(Id);
    }
    private sealed record Clock(DateTimeOffset UtcNow) : IClock { public DateTimeOffset LocalNow => UtcNow; }
    private sealed class Domain : IEsdAdjustmentDomainValidator
    {
        public bool Valid { get; set; } = true;
        public bool Throw { get; set; }
        public Task<bool> IsValidAsync(decimal proposedEsdAdjustment, CancellationToken cancellationToken = default) =>
            Throw ? throw new InvalidOperationException("domain unavailable") : Task.FromResult(Valid);
    }
    private sealed class Audit : ISecurityAuditSink
    {
        public bool Throw { get; set; }
        public int Calls { get; private set; }
        public Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default) =>
            Throw ? throw new InvalidOperationException("audit unavailable") : Count();
        private Task Count() { Calls++; return Task.CompletedTask; }
    }
    private sealed class ThrowingVerifier : IVendorAuthorizationVerifier
    {
        public Task<VendorAuthorizationVerificationResult> VerifyAsync(VendorAuthorizationPayload expected,
            ReadOnlyMemory<char> signedEnvelope, DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sensitive infrastructure detail");
    }
    private sealed class Atomic : IAtomicEsdAdjustmentExecutionBoundary
    {
        private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
        private readonly object _sync = new();
        public bool Throw { get; set; }
        public int Calls { get; private set; }
        public async Task<AtomicEsdExecutionResult> ExecuteOnceAsync(VendorAuthorizationConsumption consumption,
            decimal proposedEsdAdjustment, Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("store unavailable");
            lock (_sync)
            {
                if (!_consumed.Add(consumption.RequestId))
                    return new(AtomicEsdExecutionStatus.AlreadyConsumed, null);
            }
            await mutation(cancellationToken);
            return new(AtomicEsdExecutionStatus.Executed, consumption.ExecutionReceiptId);
        }
    }

    /// <summary>Test-only durable-store double; no production registration exists.</summary>
    private sealed class MemoryConsumedStore : IConsumedVendorAuthorizationStore
    {
        private readonly Dictionary<string, VendorAuthorizationConsumption> _items = new(StringComparer.Ordinal);
        public VendorAuthorizationConsumption? Last { get; private set; }
        public Task<bool> IsConsumedAsync(string requestId, string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.ContainsKey(requestId));
        public Task<bool> TryConsumeAsync(VendorAuthorizationConsumption consumption, CancellationToken cancellationToken = default)
        {
            bool added = _items.TryAdd(consumption.RequestId, consumption);
            if (added) Last = consumption;
            return Task.FromResult(added);
        }
    }

    private sealed record ExecutionFixture(
        ProductionSecurityReadinessFoundationTests Owner,
        ProtectedEsdAdjustmentExecutionService Service,
        Domain Domain,
        Audit Audit,
        Atomic Atomic,
        ShiftProfile Profile,
        ManagementAuthorizationProof Proof,
        VendorAuthorizationRequestContext Request,
        string Envelope)
    {
        public static ExecutionFixture Create(ProductionSecurityReadinessFoundationTests owner)
        {
            Fixture crypto = owner.FixtureWithKey();
            var domain = new Domain(); var audit = new Audit(); var atomic = new Atomic();
            var service = new ProtectedEsdAdjustmentExecutionService(crypto.Verifier, domain, audit, atomic);
            VendorAuthorizationPayload payload = owner.Payload();
            return new(owner, service, domain, audit, atomic, ProductionSecurityReadinessFoundationTests.Profile(),
                ProductionSecurityReadinessFoundationTests.Proof(), new("shift-1", "correlation-1", payload), owner.Sign(payload, "key-1"));
        }

        public Task<ProtectedEsdExecutionResult> Execute(Action mutation) => Service.ExecuteAsync(Profile,
            "station-1", 7, Proof, Request, Envelope.AsMemory(), Now,
            _ => { mutation(); return Task.CompletedTask; });
    }
}
