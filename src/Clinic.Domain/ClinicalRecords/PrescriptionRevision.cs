using Clinic.Domain.Common;

namespace Clinic.Domain.ClinicalRecords;

public sealed class PrescriptionRevision : Entity
{
    private readonly List<PrescriptionItem> _items = [];
    private PrescriptionRevision() { }

    internal PrescriptionRevision(Prescription prescription, int revisionNumber,
        long createdByUserId, DateTimeOffset createdAt, string? changeReason,
        DateOnly? returnDate,
        IReadOnlyCollection<(string MedicineName, decimal? DoseAmount,
            string? DoseUnit, int? TimesPerDay, string? FrequencyText,
            string? DurationText, FoodTiming? FoodTiming,
            string? Instructions)> items)
    {
        ArgumentNullException.ThrowIfNull(prescription);
        if (revisionNumber <= 0 || createdByUserId <= 0 || items.Count > 50 ||
            changeReason?.Trim().Length > 500)
        {
            throw new DomainException("بيانات نسخة الروشتة غير صحيحة.");
        }

        Prescription = prescription;
        RevisionNumber = revisionNumber;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        ChangeReason = Normalize(changeReason);
        ReturnDate = returnDate;
        _items.AddRange(items.Select((item, index) => new PrescriptionItem(
            index + 1, item.MedicineName, item.DoseAmount, item.DoseUnit,
            item.TimesPerDay, item.FrequencyText, item.DurationText,
            item.FoodTiming, item.Instructions)));
    }

    public long PrescriptionId { get; private set; }
    public Prescription Prescription { get; private set; } = null!;
    public int RevisionNumber { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ChangeReason { get; private set; }
    public DateOnly? ReturnDate { get; private set; }
    public IReadOnlyCollection<PrescriptionItem> Items => _items;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
