using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;

public static class ProgressiveDeliveryDbContextModelCreatingExtensions
{
    /// <summary>
    /// Maps the module entities. Call from the host DbContext's <c>OnModelCreating</c> and then add a migration
    /// in the host; ABP modules do not ship migrations because the host owns the database.
    /// </summary>
    public static void ConfigureProgressiveDelivery(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        var prefix = ProgressiveDeliveryDbProperties.DbTablePrefix;
        var schema = ProgressiveDeliveryDbProperties.DbSchema;

        builder.Entity<FeatureTrack>(b =>
        {
            b.ToTable(prefix + "FeatureTracks", schema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(ProgressiveDeliveryConsts.MaxTrackNameLength);
            b.Property(x => x.DisplayName).HasMaxLength(ProgressiveDeliveryConsts.MaxDisplayNameLength);
            b.Property(x => x.Description).HasMaxLength(ProgressiveDeliveryConsts.MaxDescriptionLength);
            b.Property(x => x.OfficialLevel).IsRequired();
            b.Property(x => x.HighestAvailableLevel).IsRequired();
            b.Property(x => x.IsEnabled).IsRequired();

            b.HasMany(x => x.Levels).WithOne().HasForeignKey(x => x.FeatureTrackId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Rollouts).WithOne().HasForeignKey(x => x.FeatureTrackId).IsRequired().OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.Name).IsUnique();

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<FeatureLevel>(b =>
        {
            b.ToTable(prefix + "FeatureLevels", schema);
            b.ConfigureByConvention();

            b.Property(x => x.Level).IsRequired();
            b.Property(x => x.Description).HasMaxLength(ProgressiveDeliveryConsts.MaxDescriptionLength);
            b.Property(x => x.SupportDescription).HasMaxLength(ProgressiveDeliveryConsts.MaxSupportDescriptionLength);
            b.Property(x => x.FallbackPolicy).IsRequired();

            b.HasIndex(x => new { x.FeatureTrackId, x.Level }).IsUnique();
        });

        builder.Entity<FeatureRollout>(b =>
        {
            b.ToTable(prefix + "FeatureRollouts", schema);
            b.ConfigureByConvention();

            b.Property(x => x.TargetLevel).IsRequired();
            b.Property(x => x.PercentageBasisPoints).IsRequired();
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => new { x.FeatureTrackId, x.TargetLevel }).IsUnique();
        });

        builder.Entity<FeatureAssignment>(b =>
        {
            b.ToTable(prefix + "FeatureAssignments", schema);
            b.ConfigureByConvention();

            b.Property(x => x.SubjectType).IsRequired().HasMaxLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength);
            b.Property(x => x.SubjectId).IsRequired().HasMaxLength(ProgressiveDeliveryConsts.MaxSubjectIdLength);
            b.Property(x => x.AssignedLevel).IsRequired();
            b.Property(x => x.AssignmentReason).HasMaxLength(ProgressiveDeliveryConsts.MaxReasonLength);

            b.HasOne<FeatureTrack>().WithMany().HasForeignKey(x => x.FeatureTrackId).IsRequired().OnDelete(DeleteBehavior.Cascade);

            // The hot-path lookup: subject + track within a tenant. Unique so concurrent writers cannot double-insert.
            b.HasIndex(x => new { x.TenantId, x.FeatureTrackId, x.SubjectType, x.SubjectId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId });

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<FeatureTransition>(b =>
        {
            b.ToTable(prefix + "FeatureTransitions", schema);
            b.ConfigureByConvention();

            b.Property(x => x.TrackName).IsRequired().HasMaxLength(ProgressiveDeliveryConsts.MaxTrackNameLength);
            b.Property(x => x.SubjectType).HasMaxLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength);
            b.Property(x => x.SubjectId).HasMaxLength(ProgressiveDeliveryConsts.MaxSubjectIdLength);
            b.Property(x => x.ToLevel).IsRequired();
            b.Property(x => x.TransitionType).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(ProgressiveDeliveryConsts.MaxReasonLength);
            b.Property(x => x.CorrelationId).HasMaxLength(ProgressiveDeliveryConsts.MaxCorrelationIdLength);
            b.Property(x => x.TraceId).HasMaxLength(ProgressiveDeliveryConsts.MaxTraceIdLength);
            b.Property(x => x.Metadata).HasMaxLength(ProgressiveDeliveryConsts.MaxMetadataLength);

            // No FK to the track: history must survive track deletion.
            b.HasIndex(x => new { x.FeatureTrackId, x.CreationTime });
            b.HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId, x.CreationTime });
            b.HasIndex(x => new { x.TenantId, x.TrackName, x.CreationTime });

            b.ApplyObjectExtensionMappings();
        });

        builder.TryConfigureObjectExtensions<ProgressiveDeliveryDbContext>();
    }
}
