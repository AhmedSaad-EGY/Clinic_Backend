namespace Clinic.Infrastructure.Patients;

public sealed class PatientTimelineQueryService(
    ClinicDbContext dbContext,
    TimeProvider timeProvider) : IPatientTimelineQueryService
{
    public async Task<Result<PatientTimelinePage>> GetAsync(long patientId,
        bool includeArchivedPatient, bool includeAdminOnlyNotes,
        PatientTimelineFilter filter, CancellationToken cancellationToken)
    {
        Patient? patient = await dbContext.Patients.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == patientId && (includeArchivedPatient || !item.IsArchived),
            cancellationToken);
        if (patient is null)
        {
            return Result.Failure<PatientTimelinePage>(PatientErrors.PatientNotFound);
        }

        HashSet<PatientTimelineRecordType> selected = filter.RecordTypes.Count == 0
            ? Enum.GetValues<PatientTimelineRecordType>().ToHashSet()
            : filter.RecordTypes.ToHashSet();
        IQueryable<TimelineRow> query = Empty(patientId);

        if (selected.Contains(PatientTimelineRecordType.Appointment))
        {
            query = query.Concat(dbContext.Appointments.AsNoTracking()
                .Where(item => item.PatientId == patientId)
                .Select(item => new TimelineRow
                {
                    RecordId = item.Id,
                    ParentRecordId = null,
                    RecordType = (int)PatientTimelineRecordType.Appointment,
                    OccurredAt = item.StartAt,
                    Text = item.Room.Department.Name,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = (int)item.PaymentStatus,
                    Amount = item.NetAmount + item.PackageCoveredAmount,
                    RelatedDate = null,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.Payment))
        {
            query = query.Concat(dbContext.Payments.AsNoTracking()
                .Where(item => item.PatientId == patientId)
                .Select(item => new TimelineRow
                {
                    RecordId = item.Id,
                    ParentRecordId = null,
                    RecordType = (int)PatientTimelineRecordType.Payment,
                    OccurredAt = item.CollectedAt,
                    Text = item.TransactionNumber,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = null,
                    Amount = item.TotalAmount,
                    RelatedDate = null,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.Refund))
        {
            query = query.Concat(dbContext.Refunds.AsNoTracking()
                .Where(item => item.AppointmentAllocations.Any(allocation =>
                    allocation.OriginalAllocation.PatientId == patientId))
                .Select(item => new TimelineRow
                {
                    RecordId = item.Id,
                    ParentRecordId = item.OriginalPaymentId,
                    RecordType = (int)PatientTimelineRecordType.Refund,
                    OccurredAt = item.ExecutedAt,
                    Text = item.TransactionNumber,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = null,
                    Amount = item.Amount,
                    RelatedDate = null,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.PatientPackage))
        {
            query = query.Concat(dbContext.PatientPackages.AsNoTracking()
                .Where(item => item.PatientId == patientId)
                .Select(item => new TimelineRow
                {
                    RecordId = item.Id,
                    ParentRecordId = null,
                    RecordType = (int)PatientTimelineRecordType.PatientPackage,
                    OccurredAt = item.RegisteredAt,
                    Text = item.PackageNameSnapshot,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = (int)item.PaymentStatus,
                    Amount = item.NetPriceSnapshot,
                    RelatedDate = null,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.PackageSession))
        {
            query = query.Concat(dbContext.PackageSessionBookings.AsNoTracking()
                .Where(item => item.PatientId == patientId)
                .Select(item => new TimelineRow
                {
                    RecordId = item.PackageSessionId,
                    ParentRecordId = item.PatientPackageId,
                    RecordType = (int)PatientTimelineRecordType.PackageSession,
                    OccurredAt = item.ConsumedAt ?? item.ReleasedAt ?? item.ReservedAt,
                    Text = item.Session.PatientPackageService.ServiceNameSnapshot,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = null,
                    Amount = item.Session.UnitPriceSnapshot,
                    RelatedDate = null,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.Prescription))
        {
            query = query.Concat(dbContext.Prescriptions.AsNoTracking()
                .Where(item => item.AppointmentService.Appointment.PatientId == patientId)
                .Select(item => new TimelineRow
                {
                    RecordId = item.Id,
                    ParentRecordId = item.AppointmentService.AppointmentId,
                    RecordType = (int)PatientTimelineRecordType.Prescription,
                    OccurredAt = item.CreatedAt,
                    Text = item.AppointmentService.Service.Name,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = null,
                    Amount = null,
                    RelatedDate = null,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.FollowUp))
        {
            query = query.Concat(dbContext.FollowUps.AsNoTracking()
                .Where(item => item.PatientId == patientId)
                .Select(item => new TimelineRow
                {
                    RecordId = item.Id,
                    ParentRecordId = item.PrescriptionId,
                    RecordType = (int)PatientTimelineRecordType.FollowUp,
                    OccurredAt = item.CreatedAt,
                    Text = item.Prescription.AppointmentService.Service.Name,
                    PrimaryStatus = (int)item.Status,
                    SecondaryStatus = null,
                    Amount = null,
                    RelatedDate = item.ReturnDate,
                    IsArchived = false
                }));
        }

        if (selected.Contains(PatientTimelineRecordType.TreatmentHistory))
        {
            IQueryable<TreatmentHistory> history = dbContext.TreatmentHistory.AsNoTracking()
                .Where(item => item.PatientId == patientId);
            if (!filter.IncludeArchivedRecords)
            {
                history = history.Where(item => !item.IsArchived);
            }

            query = query.Concat(history.Select(item => new TimelineRow
            {
                RecordId = item.Id,
                ParentRecordId = null,
                RecordType = (int)PatientTimelineRecordType.TreatmentHistory,
                OccurredAt = item.CreatedAt,
                Text = item.Description,
                PrimaryStatus = null,
                SecondaryStatus = null,
                Amount = null,
                RelatedDate = item.EventDate,
                IsArchived = item.IsArchived
            }));
        }

        if (selected.Contains(PatientTimelineRecordType.Note))
        {
            IQueryable<PatientNote> notes = dbContext.PatientNotes.AsNoTracking()
                .Where(item => item.PatientId == patientId);
            if (!includeAdminOnlyNotes)
            {
                notes = notes.Where(item => item.Visibility == PatientNoteVisibility.Staff);
            }
            if (!filter.IncludeArchivedRecords)
            {
                notes = notes.Where(item => !item.IsArchived);
            }

            query = query.Concat(notes.Select(item => new TimelineRow
            {
                RecordId = item.Id,
                ParentRecordId = null,
                RecordType = (int)PatientTimelineRecordType.Note,
                OccurredAt = item.CreatedAt,
                Text = item.NoteText,
                PrimaryStatus = (int)item.Visibility,
                SecondaryStatus = null,
                Amount = null,
                RelatedDate = null,
                IsArchived = item.IsArchived
            }));
        }

        if (filter.From is DateOnly from)
        {
            DateTimeOffset fromUtc = PatientInfrastructureSupport.ClinicDayStartUtc(from);
            query = query.Where(item => item.OccurredAt >= fromUtc);
        }
        if (filter.To is DateOnly to)
        {
            DateTimeOffset toExclusiveUtc = PatientInfrastructureSupport
                .ClinicDayStartUtc(to.AddDays(1));
            query = query.Where(item => item.OccurredAt < toExclusiveUtc);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        TimelineRow[] rows = await query.OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.RecordType)
            .ThenByDescending(item => item.RecordId)
            .Skip(checked((filter.PageNumber - 1) * filter.PageSize))
            .Take(filter.PageSize).ToArrayAsync(cancellationToken);
        DateOnly today = PatientInfrastructureSupport.ClinicDate(timeProvider.GetUtcNow());

        return Result.Success(new PatientTimelinePage(PatientMapper.Details(patient, today),
            rows.Select(Map).ToArray(), filter.PageNumber, filter.PageSize, totalCount));
    }

    private IQueryable<TimelineRow> Empty(long patientId) => dbContext.Appointments
        .AsNoTracking().Where(item => item.PatientId == patientId && false)
        .Select(item => new TimelineRow
        {
            RecordId = item.Id,
            ParentRecordId = null,
            RecordType = 0,
            OccurredAt = item.StartAt,
            Text = string.Empty,
            PrimaryStatus = null,
            SecondaryStatus = null,
            Amount = null,
            RelatedDate = null,
            IsArchived = false
        });

    private static PatientTimelineItem Map(TimelineRow row)
    {
        PatientTimelineRecordType type = (PatientTimelineRecordType)row.RecordType;
        string? status = Status(type, row.PrimaryStatus, row.SecondaryStatus);
        return new PatientTimelineItem(row.RecordId, row.ParentRecordId, type,
            PatientInfrastructureSupport.ClinicDate(row.OccurredAt), row.OccurredAt,
            Title(type), Summary(type, row), status, row.Amount, row.IsArchived);
    }

    private static string Title(PatientTimelineRecordType type) => type switch
    {
        PatientTimelineRecordType.Appointment => "حجز",
        PatientTimelineRecordType.Payment => "تحصيل",
        PatientTimelineRecordType.Refund => "استرداد",
        PatientTimelineRecordType.PatientPackage => "باقة مريض",
        PatientTimelineRecordType.PackageSession => "جلسة باقة",
        PatientTimelineRecordType.Prescription => "روشتة",
        PatientTimelineRecordType.FollowUp => "متابعة",
        PatientTimelineRecordType.TreatmentHistory => "تاريخ علاجي",
        PatientTimelineRecordType.Note => "ملاحظة",
        _ => throw new InvalidOperationException("Unknown patient timeline record type.")
    };

    private static string Summary(PatientTimelineRecordType type, TimelineRow row) =>
        type switch
        {
            PatientTimelineRecordType.Appointment => $"القسم: {row.Text}",
            PatientTimelineRecordType.Payment => $"رقم العملية: {row.Text}",
            PatientTimelineRecordType.Refund => $"رقم العملية: {row.Text}",
            PatientTimelineRecordType.PatientPackage => row.Text,
            PatientTimelineRecordType.PackageSession => $"الخدمة: {row.Text}",
            PatientTimelineRecordType.Prescription => $"الخدمة: {row.Text}",
            PatientTimelineRecordType.FollowUp =>
                $"{row.Text} - موعد الإعادة: {row.RelatedDate:yyyy-MM-dd}",
            PatientTimelineRecordType.TreatmentHistory =>
                $"{row.RelatedDate:yyyy-MM-dd} - {Preview(row.Text)}",
            PatientTimelineRecordType.Note => Preview(row.Text),
            _ => row.Text
        };

    private static string Preview(string text) => text.Length <= 160
        ? text
        : $"{text[..160]}…";

    private static string? Status(PatientTimelineRecordType type, int? primary,
        int? secondary)
    {
        if (!primary.HasValue)
        {
            return null;
        }

        string first = type switch
        {
            PatientTimelineRecordType.Appointment =>
                Enum.GetName((AppointmentStatus)primary.Value)!,
            PatientTimelineRecordType.Payment =>
                Enum.GetName((PaymentRecordStatus)primary.Value)!,
            PatientTimelineRecordType.Refund =>
                Enum.GetName((RefundStatus)primary.Value)!,
            PatientTimelineRecordType.PatientPackage =>
                Enum.GetName((PatientPackageStatus)primary.Value)!,
            PatientTimelineRecordType.PackageSession =>
                Enum.GetName((PackageSessionBookingStatus)primary.Value)!,
            PatientTimelineRecordType.Prescription =>
                Enum.GetName((PrescriptionStatus)primary.Value)!,
            PatientTimelineRecordType.FollowUp =>
                Enum.GetName((FollowUpStatus)primary.Value)!,
            PatientTimelineRecordType.Note =>
                Enum.GetName((PatientNoteVisibility)primary.Value)!,
            _ => string.Empty
        };

        if (!secondary.HasValue)
        {
            return first;
        }

        string second = type switch
        {
            PatientTimelineRecordType.Appointment =>
                Enum.GetName((PaymentStatus)secondary.Value)!,
            PatientTimelineRecordType.PatientPackage =>
                Enum.GetName((PatientPackagePaymentStatus)secondary.Value)!,
            _ => string.Empty
        };
        return string.IsNullOrEmpty(second) ? first : $"{first} / {second}";
    }

    private sealed class TimelineRow
    {
        public long RecordId { get; init; }
        public long? ParentRecordId { get; init; }
        public int RecordType { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public string Text { get; init; } = string.Empty;
        public int? PrimaryStatus { get; init; }
        public int? SecondaryStatus { get; init; }
        public decimal? Amount { get; init; }
        public DateOnly? RelatedDate { get; init; }
        public bool IsArchived { get; init; }
    }
}
