using System.Reflection;
using Clinic.Domain.ClinicalRecords;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.ClinicalRecords;

public sealed class ClinicalRecordDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DraftSavesImmutableNumberedRevisions()
    {
        Prescription prescription = CreatePrescription();

        PrescriptionRevision revision = prescription.SaveDraft(2, Now.AddMinutes(1),
            Content("دواء ثان"));

        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal(2, prescription.Revisions.Count);
        Assert.Equal("دواء أول", prescription.Revisions.First().Items.Single().MedicineName);
    }

    [Fact]
    public void FinalizeRequiresDoctorMatchConfirmation()
    {
        Prescription prescription = CreatePrescription();
        SelectFirstRevision(prescription);

        Assert.Throws<DomainException>(() => prescription.Finalize(2, Now, false));
    }

    [Fact]
    public void FinalizedPrescriptionCanOnlyBeChangedAsCorrection()
    {
        Prescription prescription = CreatePrescription();
        SelectFirstRevision(prescription);
        prescription.Finalize(2, Now, true);

        Assert.Throws<DomainException>(() => prescription.SaveDraft(2,
            Now.AddMinutes(1), Content("تعديل")));
        PrescriptionRevision correction = prescription.Correct(1,
            Now.AddMinutes(2), "تصحيح وصف الطبيب", Content("دواء مصحح"));

        Assert.Equal(2, correction.RevisionNumber);
        Assert.Equal("تصحيح وصف الطبيب", correction.ChangeReason);
    }

    [Fact]
    public void FinalizedPrescriptionRejectsCorrectionWithoutMedicines()
    {
        Prescription prescription = CreatePrescription();
        SelectFirstRevision(prescription);
        prescription.Finalize(2, Now, true);

        Assert.Throws<DomainException>(() => prescription.Correct(1,
            Now.AddMinutes(1), "تصحيح", new PrescriptionContent(null, [])));
    }

    [Fact]
    public void FollowUpHasOnlyDueCompletedAndCancelledTransitions()
    {
        FollowUp followUp = FollowUp.Create(1, 2, 3,
            new DateOnly(2026, 9, 20), 4, Now);

        followUp.Complete(5, 4, Now.AddDays(1));

        Assert.Equal(FollowUpStatus.Completed, followUp.Status);
        Assert.Equal(5, followUp.ResultAppointmentId);
        Assert.Throws<DomainException>(() => followUp.Cancel(4,
            Now.AddDays(2), "إلغاء"));
    }

    [Fact]
    public void PrescriptionItemRejectsInvalidDoseAndFrequency()
    {
        Assert.Throws<DomainException>(() => Prescription.Create(1, 2, Now,
            new PrescriptionContent(null,
            [("دواء", 0m, "مل", 0, null, null, null, null)])));
    }

    private static Prescription CreatePrescription() =>
        Prescription.Create(1, 2, Now, Content("دواء أول"));

    private static PrescriptionContent Content(string medicineName) =>
        new(new DateOnly(2026, 9, 20),
        [(medicineName, 1m, "قرص", 2, null, "خمسة أيام",
            FoodTiming.AfterMeal, "بعد الطعام")]);

    private static void SelectFirstRevision(Prescription prescription)
    {
        PrescriptionRevision revision = prescription.Revisions.Single();
        typeof(PrescriptionRevision).GetProperty(nameof(PrescriptionRevision.Id),
            BindingFlags.Instance | BindingFlags.Public)!.SetValue(revision, 1L);
        prescription.SelectCurrentRevision(revision);
    }
}
