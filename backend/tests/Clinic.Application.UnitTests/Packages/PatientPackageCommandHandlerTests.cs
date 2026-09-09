using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Features.Packages;
using Clinic.Domain.Packages;

namespace Clinic.Application.UnitTests.Packages;

public sealed class PatientPackageCommandHandlerTests
{
    [Fact]
    public async Task RegisterRejectsEmptyIdempotencyKeyBeforePersistence()
    {
        FakeCommandService service = new();
        RegisterPatientPackageCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientPackageRegistrationResult> result = await handler.Handle(
            new RegisterPatientPackageCommand(1, 2,
                Convert.ToBase64String(new byte[8]), Guid.Empty),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("packages.validation", result.Error.Code);
        Assert.Equal("بيانات تسجيل الباقة غير صحيحة.", result.Error.Description);
        Assert.False(service.RegisterCalled);
    }

    [Fact]
    public async Task RegisterRejectsInvalidPackageRowVersionBeforePersistence()
    {
        FakeCommandService service = new();
        RegisterPatientPackageCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientPackageRegistrationResult> result = await handler.Handle(
            new RegisterPatientPackageCommand(1, 2, "invalid", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.RegisterCalled);
    }

    [Fact]
    public async Task ExtendRejectsMissingReasonBeforePersistence()
    {
        FakeCommandService service = new();
        ExtendPatientPackageCommandHandler handler = new(new FakeCurrentUser(7), service);

        Result<PatientPackageModel> result = await handler.Handle(
            new ExtendPatientPackageCommand(1, PatientPackageExtensionType.ActivationDeadline,
                DateTimeOffset.UtcNow.AddDays(1), " ",
                Convert.ToBase64String(new byte[8])),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("packages.validation", result.Error.Code);
        Assert.Equal("نوع التمديد والموعد والسبب مطلوبة، والسبب لا يتجاوز 500 حرف.",
            result.Error.Description);
        Assert.False(service.ExtendCalled);
    }

    private sealed class FakeCurrentUser(long? userId) : ICurrentUser
    {
        public long? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeCommandService : IPatientPackageCommandService
    {
        public bool RegisterCalled { get; private set; }
        public bool ExtendCalled { get; private set; }

        public Task<Result<PatientPackageRegistrationResult>> RegisterAsync(long actorUserId,
            long patientId, long packageId, byte[] expectedPackageRowVersion,
            Guid idempotencyKey, CancellationToken cancellationToken)
        {
            RegisterCalled = true;
            return Task.FromResult(Result.Failure<PatientPackageRegistrationResult>(
                PackageErrors.NotFound));
        }

        public Task<Result<PatientPackageModel>> ExtendAsync(long adminUserId,
            long patientPackageId, PatientPackageExtensionType extensionType,
            DateTimeOffset newDeadline, string reason, byte[] expectedRowVersion,
            CancellationToken cancellationToken)
        {
            ExtendCalled = true;
            return Task.FromResult(Result.Failure<PatientPackageModel>(PackageErrors.NotFound));
        }
    }
}
