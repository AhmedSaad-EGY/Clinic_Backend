using Clinic.Domain.ClinicalRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.ClinicalRecords;

public sealed class PrescriptionItemConfiguration
    : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems", "clinical", table =>
        {
            table.HasCheckConstraint("CK_PrescriptionItems_Order", "[SortOrder] > 0");
            table.HasCheckConstraint("CK_PrescriptionItems_Dose",
                "[DoseAmount] IS NULL OR [DoseAmount] > 0");
            table.HasCheckConstraint("CK_PrescriptionItems_Times",
                "[TimesPerDay] IS NULL OR [TimesPerDay] > 0");
            table.HasCheckConstraint("CK_PrescriptionItems_FoodTiming",
                "[FoodTiming] IS NULL OR [FoodTiming] IN (1, 2)");
        });
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.PrescriptionRevisionId, item.SortOrder })
            .IsUnique();
        builder.Property(item => item.MedicineName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.DoseAmount).HasPrecision(18, 4);
        builder.Property(item => item.DoseUnit).HasMaxLength(50);
        builder.Property(item => item.FrequencyText).HasMaxLength(200);
        builder.Property(item => item.DurationText).HasMaxLength(200);
        builder.Property(item => item.FoodTiming).HasConversion<int?>();
        builder.Property(item => item.Instructions).HasMaxLength(1000);
    }
}
