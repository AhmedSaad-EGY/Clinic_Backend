using Clinic.Domain.Appointments;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Appointments;

public sealed class AppointmentDomainTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MultipleServicesAreSequentialAndTotalsAreSnapshotted()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, false, 4, Start.AddDays(-1));

        appointment.AddService(10, 20, 30, 1, 200m, [(30, 40)]);
        appointment.AddService(11, 21, 45, 3, 25m, []);

        Assert.Equal(Start.AddMinutes(75), appointment.EndAt);
        Assert.Equal(275m, appointment.SubtotalAmount);
        Assert.Equal(275m, appointment.NetAmount);
        Assert.Equal([1, 2], appointment.Services.Select(item => item.SequenceNumber));
        Assert.Equal(Start.AddMinutes(30), appointment.Services.Last().SegmentStartAt);
    }

    [Fact]
    public void TerminalAppointmentCannotBeEdited()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, false, 4, Start.AddDays(-1));
        appointment.AddService(10, 20, 30, 1, 200m, []);
        appointment.Complete(4, Start);

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.All(appointment.Services,
            item => Assert.Equal(AppointmentServiceStatus.Completed, item.Status));
        Assert.Throws<DomainException>(() => appointment.AddService(11, 21, 30, 1, 100m, []));
    }

    [Fact]
    public void SuspendedAppointmentDoesNotReserveResourcesAndCanReactivate()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, true, 4, Start.AddDays(-1));
        appointment.AddService(10, 20, 30, 1, 200m, []);

        Assert.False(appointment.ReservesResources);
        appointment.Reactivate(4, Start);
        Assert.True(appointment.ReservesResources);
    }

    [Fact]
    public void DeviceAssignmentCannotBeDuplicated()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, false, 4, Start.AddDays(-1));
        Assert.Throws<DomainException>(() => appointment.AddService(
            10, 20, 30, 1, 200m, [(30, 40), (30, 40)]));
    }

    [Fact]
    public void ReplacingScheduleSupersedesHistoryButCancellationKeepsCurrentLines()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, false, 4,
            Start.AddDays(-1));
        AppointmentService original = appointment.AddService(10, 20, 30, 1, 200m, []);

        appointment.ReplaceSchedule(Start.AddHours(1), 4, Start);
        AppointmentService current = appointment.AddService(11, 21, 45, 1, 300m, []);
        appointment.Cancel(4, Start.AddMinutes(1), null);

        Assert.Equal(AppointmentServiceStatus.Superseded, original.Status);
        Assert.Equal(AppointmentServiceStatus.Cancelled, current.Status);
    }

    [Fact]
    public void FullPaymentConfirmsBookedAppointment()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, false, 4,
            Start.AddDays(-1));
        appointment.AddService(10, 20, 30, 1, 200m, []);

        appointment.RecordFullPayment(4, Start);

        Assert.Equal(PaymentStatus.Paid, appointment.PaymentStatus);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Throws<DomainException>(() => appointment.RecordFullPayment(4, Start));
    }

    [Fact]
    public void FullPaymentKeepsCompletedAppointmentCompleted()
    {
        Appointment appointment = Appointment.Create(1, 2, 3, Start, false, 4,
            Start.AddDays(-1));
        appointment.AddService(10, 20, 30, 1, 200m, []);
        appointment.Complete(4, Start);

        appointment.RecordFullPayment(4, Start);

        Assert.Equal(PaymentStatus.Paid, appointment.PaymentStatus);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }
}
