namespace Clinic.Domain.ClinicalRecords;

public sealed class PrescriptionItem : Entity
{
    private PrescriptionItem() { }

    internal PrescriptionItem(int sortOrder, string medicineName,
        decimal? doseAmount, string? doseUnit, int? timesPerDay,
        string? frequencyText, string? durationText, FoodTiming? foodTiming,
        string? instructions)
    {
        if (sortOrder <= 0 || string.IsNullOrWhiteSpace(medicineName) ||
            medicineName.Trim().Length > 200 || doseAmount <= 0 ||
            doseUnit?.Trim().Length > 50 || timesPerDay <= 0 ||
            frequencyText?.Trim().Length > 200 ||
            durationText?.Trim().Length > 200 ||
            instructions?.Trim().Length > 1000 ||
            foodTiming.HasValue && !Enum.IsDefined(foodTiming.Value))
        {
            throw new DomainException("بيانات الدواء غير صحيحة.");
        }

        SortOrder = sortOrder;
        MedicineName = medicineName.Trim();
        DoseAmount = doseAmount;
        DoseUnit = Normalize(doseUnit);
        TimesPerDay = timesPerDay;
        FrequencyText = Normalize(frequencyText);
        DurationText = Normalize(durationText);
        FoodTiming = foodTiming;
        Instructions = Normalize(instructions);
    }

    public long PrescriptionRevisionId { get; private set; }
    public PrescriptionRevision PrescriptionRevision { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public string MedicineName { get; private set; } = string.Empty;
    public decimal? DoseAmount { get; private set; }
    public string? DoseUnit { get; private set; }
    public int? TimesPerDay { get; private set; }
    public string? FrequencyText { get; private set; }
    public string? DurationText { get; private set; }
    public FoodTiming? FoodTiming { get; private set; }
    public string? Instructions { get; private set; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
