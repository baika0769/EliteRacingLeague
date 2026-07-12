using System;
using System.Collections.Generic;
using Eliteracingleague.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Data;

public partial class EliteRacingLeagueContext : DbContext
{
    public EliteRacingLeagueContext()
    {
    }

    public EliteRacingLeagueContext(DbContextOptions<EliteRacingLeagueContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Horse> Horses { get; set; }

    public virtual DbSet<HorseBreed> HorseBreeds { get; set; }

    public virtual DbSet<HorseOwner> HorseOwners { get; set; }

    public virtual DbSet<Jockey> Jockeys { get; set; }

    public virtual DbSet<JockeyAvailability> JockeyAvailabilities { get; set; }

    public virtual DbSet<JockeyBreedExperience> JockeyBreedExperiences { get; set; }

    public virtual DbSet<JockeyDistanceExperience> JockeyDistanceExperiences { get; set; }

    public virtual DbSet<JockeyInvitation> JockeyInvitations { get; set; }

    public virtual DbSet<JockeyRecommendation> JockeyRecommendations { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PreRaceInspection> PreRaceInspections { get; set; }

    public virtual DbSet<PrizeAward> PrizeAwards { get; set; }

    public virtual DbSet<PrizeRule> PrizeRules { get; set; }

    public virtual DbSet<Race> Races { get; set; }

    public virtual DbSet<RacePrediction> RacePredictions { get; set; }

    public virtual DbSet<RaceReferee> RaceReferees { get; set; }

    public virtual DbSet<RaceRegistration> RaceRegistrations { get; set; }

    public virtual DbSet<RaceResult> RaceResults { get; set; }

    public virtual DbSet<RaceViolation> RaceViolations { get; set; }

    public virtual DbSet<RefereeAssignment> RefereeAssignments { get; set; }

    public virtual DbSet<RefereeReport> RefereeReports { get; set; }

    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonRewardRule> SeasonRewardRules { get; set; }

    public virtual DbSet<SeasonReward> SeasonRewards { get; set; }

    public virtual DbSet<Tournament> Tournaments { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<EmailVerificationOtp> EmailVerificationOtps { get; set; }









    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Horse>(entity =>
        {
            entity.HasKey(e => e.HorseId).HasName("PK__horses__DE0BA8A246C3391C");

            entity.ToTable("horses");

            entity.Property(e => e.HorseId).HasColumnName("horse_id");
            entity.Property(e => e.AchievementSummary)
                .HasMaxLength(1000)
                .HasColumnName("achievement_summary");
            entity.Property(e => e.Age).HasColumnName("age");
            entity.Property(e => e.BreedId).HasColumnName("breed_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.HealthStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("health_status");
            entity.Property(e => e.HeightCm)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("height_cm");
            entity.Property(e => e.HorseName)
                .HasMaxLength(150)
                .HasColumnName("horse_name");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasColumnName("image_url");
            entity.Property(e => e.HealthCertificateImageUrl)
                .HasMaxLength(500)
                .HasColumnName("health_certificate_image_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.WeightKg)
                .HasColumnType("decimal(6, 2)")
                .HasColumnName("weight_kg");

            entity.HasOne(d => d.Breed).WithMany(p => p.Horses)
                .HasForeignKey(d => d.BreedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_horses_horse_breeds");

            entity.HasOne(d => d.Owner).WithMany(p => p.Horses)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_horses_horse_owners");
        });

        modelBuilder.Entity<EmailVerificationOtp>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_email_verification_otps");

            entity.ToTable("email_verification_otps");

            entity.HasIndex(e => new { e.UserId, e.IsUsed, e.ExpiresAt }, "IX_email_verification_otps_user_active");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_email_verification_otps_user_created_at");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.Property(e => e.OtpHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("otp_hash");

            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");

            entity.Property(e => e.IsUsed)
                .HasDefaultValue(false)
                .HasColumnName("is_used");

            entity.Property(e => e.FailedAttempts)
                .HasDefaultValue(0)
                .HasColumnName("failed_attempts");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");

            entity.Property(e => e.UsedAt).HasColumnName("used_at");

            entity.HasOne(d => d.User).WithMany(p => p.EmailVerificationOtps)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_email_verification_otps_users");
        });

        modelBuilder.Entity<HorseBreed>(entity =>
        {
            entity.HasKey(e => e.BreedId).HasName("PK__horse_br__9C02143508F51A03");

            entity.ToTable("horse_breeds");

            entity.HasIndex(e => e.BreedName, "UQ__horse_br__1026DC5C67469E99").IsUnique();

            entity.Property(e => e.BreedId).HasColumnName("breed_id");
            entity.Property(e => e.BreedName)
                .HasMaxLength(150)
                .HasColumnName("breed_name");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<HorseOwner>(entity =>
        {
            entity.HasKey(e => e.OwnerId).HasName("PK__horse_ow__3C4FBEE407468D3B");

            entity.ToTable("horse_owners");

            entity.Property(e => e.OwnerId)
                .ValueGeneratedNever()
                .HasColumnName("owner_id");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.Owner).WithOne(p => p.HorseOwner)
                .HasForeignKey<HorseOwner>(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_horse_owners_users");
        });

        modelBuilder.Entity<Jockey>(entity =>
        {
            entity.HasKey(e => e.JockeyId).HasName("PK__jockeys__A37CBB381F9D6473");

            entity.ToTable("jockeys");

            entity.Property(e => e.JockeyId)
                .ValueGeneratedNever()
                .HasColumnName("jockey_id");
            entity.Property(e => e.CertificateFileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("certificate_file_url");
            entity.Property(e => e.ProfileImageUrl)
                .HasMaxLength(500)
                .HasColumnName("profile_image_url");

            entity.Property(e => e.IdCardFrontUrl)
                .HasMaxLength(500)
                .HasColumnName("id_card_front_url");

            entity.Property(e => e.IdCardBackUrl)
                .HasMaxLength(500)
                .HasColumnName("id_card_back_url");

            entity.Property(e => e.HealthCertificateUrl)
                .HasMaxLength(500)
                .HasColumnName("health_certificate_url");
            entity.Property(e => e.CertificateNo)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("certificate_no");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.HealthStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("health_status");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.WeightKg)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("weight_kg");
            entity.Property(e => e.YearsOfExperience).HasColumnName("years_of_experience");

            entity.HasOne(d => d.JockeyNavigation).WithOne(p => p.Jockey)
                .HasForeignKey<Jockey>(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockeys_users");
        });

        modelBuilder.Entity<JockeyAvailability>(entity =>
        {
            entity.HasKey(e => e.AvailabilityId).HasName("PK__jockey_a__86E3A8015CD4B330");

            entity.ToTable("jockey_availabilities");

            entity.HasIndex(e => new { e.JockeyId, e.AvailableDate }, "UQ_jockey_availabilities_jockey_date").IsUnique();

            entity.Property(e => e.AvailabilityId).HasColumnName("availability_id");
            entity.Property(e => e.AvailableDate).HasColumnName("available_date");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.HasOne(d => d.Jockey).WithMany(p => p.JockeyAvailabilities)
                .HasForeignKey(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_availabilities_jockeys");
        });

        modelBuilder.Entity<JockeyBreedExperience>(entity =>
        {
            entity.HasKey(e => e.JockeyBreedExperienceId).HasName("PK__jockey_b__62791554F51F3E30");

            entity.ToTable("jockey_breed_experiences");

            entity.HasIndex(e => new { e.JockeyId, e.BreedId }, "UQ_jockey_breed_experiences_jockey_breed").IsUnique();

            entity.Property(e => e.JockeyBreedExperienceId).HasColumnName("jockey_breed_experience_id");
            entity.Property(e => e.BreedId).HasColumnName("breed_id");
            entity.Property(e => e.ExperienceLevel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("experience_level");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");

            entity.HasOne(d => d.Breed).WithMany(p => p.JockeyBreedExperiences)
                .HasForeignKey(d => d.BreedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_breed_experiences_breeds");

            entity.HasOne(d => d.Jockey).WithMany(p => p.JockeyBreedExperiences)
                .HasForeignKey(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_breed_experiences_jockeys");
        });

        modelBuilder.Entity<JockeyDistanceExperience>(entity =>
        {
            entity.HasKey(e => e.JockeyDistanceExperienceId).HasName("PK__jockey_d__3E2AD9BCB9A2A8CD");

            entity.ToTable("jockey_distance_experiences");

            entity.HasIndex(e => new { e.JockeyId, e.DistanceMeters }, "UQ_jockey_distance_experiences_jockey_distance").IsUnique();

            entity.Property(e => e.JockeyDistanceExperienceId).HasColumnName("jockey_distance_experience_id");
            entity.Property(e => e.DistanceMeters).HasColumnName("distance_meters");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");
            entity.Property(e => e.SkillLevel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("skill_level");

            entity.HasOne(d => d.Jockey).WithMany(p => p.JockeyDistanceExperiences)
                .HasForeignKey(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_distance_experiences_jockeys");
        });

        modelBuilder.Entity<JockeyInvitation>(entity =>
        {
            entity.HasKey(e => e.InvitationId).HasName("PK__jockey_i__94B74D7C5D75ED0F");

            entity.ToTable("jockey_invitations");

            entity.HasIndex(e => new { e.RegistrationId, e.JockeyId }, "UQ_jockey_invitations_registration_jockey").IsUnique();

            entity.Property(e => e.InvitationId).HasColumnName("invitation_id");
            entity.Property(e => e.FeeAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("fee_amount");
            entity.Property(e => e.InvitedByOwnerId).HasColumnName("invited_by_owner_id");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");
            entity.Property(e => e.Message)
                .HasMaxLength(500)
                .HasColumnName("message");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("sent_at");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.HasOne(d => d.InvitedByOwner).WithMany(p => p.JockeyInvitations)
                .HasForeignKey(d => d.InvitedByOwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_invitations_owners");

            entity.HasOne(d => d.Jockey).WithMany(p => p.JockeyInvitations)
                .HasForeignKey(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_invitations_jockeys");

            entity.HasOne(d => d.Registration).WithMany(p => p.JockeyInvitations)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_invitations_registrations");
        });



        modelBuilder.Entity<JockeyRecommendation>(entity =>
        {
            entity.HasKey(e => e.RecommendationId).HasName("PK__jockey_r__BCB11F4FDE99F9DB");

            entity.ToTable("jockey_recommendations");

            entity.HasIndex(e => new { e.RegistrationId, e.JockeyId }, "UQ_jockey_recommendations_registration_jockey").IsUnique();

            entity.HasIndex(e => new { e.RegistrationId, e.RankNo }, "UQ_jockey_recommendations_registration_rank").IsUnique();

            entity.Property(e => e.RecommendationId).HasColumnName("recommendation_id");
            entity.Property(e => e.AvailabilityScore).HasColumnName("availability_score");
            entity.Property(e => e.BreedExperienceScore).HasColumnName("breed_experience_score");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DistanceScore).HasColumnName("distance_score");
            entity.Property(e => e.ExperienceScore).HasColumnName("experience_score");
            entity.Property(e => e.HorseId).HasColumnName("horse_id");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");
            entity.Property(e => e.OwnerDecision)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("owner_decision");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RankNo).HasColumnName("rank_no");
            entity.Property(e => e.RecommendationLevel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("recommendation_level");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.TotalScore).HasColumnName("total_score");
            entity.Property(e => e.WeightScore).HasColumnName("weight_score");

            entity.HasOne(d => d.Horse).WithMany(p => p.JockeyRecommendations)
                .HasForeignKey(d => d.HorseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_recommendations_horses");

            entity.HasOne(d => d.Jockey).WithMany(p => p.JockeyRecommendations)
                .HasForeignKey(d => d.JockeyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_recommendations_jockeys");

            entity.HasOne(d => d.Race).WithMany(p => p.JockeyRecommendations)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_recommendations_races");

            entity.HasOne(d => d.Registration).WithMany(p => p.JockeyRecommendations)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jockey_recommendations_registrations");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__notifica__E059842F608095F5");

            entity.ToTable("notifications");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.Message)
                .HasMaxLength(1000)
                .HasColumnName("message");
            entity.Property(e => e.RelatedType)
                .HasMaxLength(50)
                .HasColumnName("related_type");
            entity.Property(e => e.RelatedId)
                .HasColumnName("related_id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.ActionUrl)
                .HasMaxLength(300)
                .HasColumnName("action_url");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_notifications_users");
        });

        modelBuilder.Entity<PreRaceInspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId).HasName("PK__pre_race__C3C4E743F6FE7C6D");

            entity.ToTable("pre_race_inspections");

            entity.HasIndex(e => new { e.RaceId, e.RegistrationId }, "UQ_pre_race_inspections_race_registration").IsUnique();

            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.InspectedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("inspected_at");
            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RefereeId).HasColumnName("referee_id");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.HasOne(d => d.Race).WithMany(p => p.PreRaceInspections)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_pre_race_inspections_races");

            entity.HasOne(d => d.Referee).WithMany(p => p.PreRaceInspections)
                .HasForeignKey(d => d.RefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_pre_race_inspections_referees");

            entity.HasOne(d => d.Registration).WithMany(p => p.PreRaceInspections)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_pre_race_inspections_registrations");
        });

        modelBuilder.Entity<PrizeAward>(entity =>
        {
            entity.HasKey(e => e.PrizeAwardId).HasName("PK__prize_aw__14E95D184DD587F4");

            entity.ToTable("prize_awards");

            entity.HasIndex(e => new { e.RaceId, e.RankPosition }, "UQ_prize_awards_race_rank").IsUnique();

            entity.Property(e => e.PrizeAwardId).HasColumnName("prize_award_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.PrizeAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("prize_amount");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RankPosition).HasColumnName("rank_position");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.HasOne(d => d.Jockey).WithMany(p => p.PrizeAwards)
                .HasForeignKey(d => d.JockeyId)
                .HasConstraintName("FK_prize_awards_jockeys");

            entity.HasOne(d => d.Owner).WithMany(p => p.PrizeAwards)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_prize_awards_owners");

            entity.HasOne(d => d.Race).WithMany(p => p.PrizeAwards)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_prize_awards_races");

            entity.HasOne(d => d.Registration).WithMany(p => p.PrizeAwards)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_prize_awards_registrations");
        });

        modelBuilder.Entity<PrizeRule>(entity =>
        {
            entity.HasKey(e => e.PrizeRuleId).HasName("PK__prize_ru__0BF66D14DD02975C");

            entity.ToTable("prize_rules");

            entity.HasIndex(e => new { e.RaceId, e.RankPosition }, "UQ_prize_rules_race_rank").IsUnique();

            entity.Property(e => e.PrizeRuleId).HasColumnName("prize_rule_id");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasColumnName("note");
            entity.Property(e => e.PrizeAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("prize_amount");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RankPosition).HasColumnName("rank_position");

            entity.HasOne(d => d.Race).WithMany(p => p.PrizeRules)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_prize_rules_races");
        });

        modelBuilder.Entity<Race>(entity =>
        {
            entity.HasKey(e => e.RaceId).HasName("PK__races__1C8FE2F9C56EC77A");

            entity.ToTable("races");

            entity.HasIndex(e => e.TournamentId, "UQ__races__B93AA09C53DC9C5A").IsUnique();

            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DistanceMeters).HasColumnName("distance_meters");
            entity.Property(e => e.JockeySelectionDeadline).HasColumnName("jockey_selection_deadline");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.MaxHorses)
                .HasDefaultValue(10)
                .HasColumnName("max_horses");
            entity.Property(e => e.PredictionDeadline).HasColumnName("prediction_deadline");
            entity.Property(e => e.RaceDate).HasColumnName("race_date");
            entity.Property(e => e.RaceName)
                .HasMaxLength(200)
                .HasColumnName("race_name");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Tournament).WithOne(p => p.Race)
                .HasForeignKey<Race>(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_races_tournaments");
        });

        modelBuilder.Entity<RacePrediction>(entity =>
        {
            entity.HasKey(e => e.PredictionId).HasName("PK__race_pre__F1AE77BF0CDAA820");

            entity.ToTable("race_predictions");

            entity.HasIndex(e => new { e.RaceId, e.PredictedRegistrationId }, "IX_race_predictions_race_predicted_registration");

            entity.HasIndex(e => new { e.RaceId, e.SpectatorId }, "UQ_race_predictions_race_spectator").IsUnique();

            entity.Property(e => e.PredictionId).HasColumnName("prediction_id");
            entity.Property(e => e.ActualWinnerRegistrationId).HasColumnName("actual_winner_registration_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EvaluatedAt).HasColumnName("evaluated_at");
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.LockedAt).HasColumnName("locked_at");
            entity.Property(e => e.PointsAwarded).HasColumnName("points_awarded");
            entity.Property(e => e.StakePoints).HasColumnName("stake_points");
            entity.Property(e => e.PredictedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("predicted_at");
            entity.Property(e => e.PredictedRegistrationId).HasColumnName("predicted_registration_id");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RewardAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("reward_amount");
            entity.Property(e => e.RewardStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("reward_status");
            entity.Property(e => e.SpectatorId).HasColumnName("spectator_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.ActualWinnerRegistration).WithMany(p => p.RacePredictionActualWinnerRegistrations)
                .HasForeignKey(d => d.ActualWinnerRegistrationId)
                .HasConstraintName("FK_race_predictions_actual_winner");

            entity.HasOne(d => d.PredictedRegistration).WithMany(p => p.RacePredictionPredictedRegistrations)
                .HasForeignKey(d => d.PredictedRegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_predictions_predicted_registration");

            entity.HasOne(d => d.Race).WithMany(p => p.RacePredictions)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_predictions_races");

            entity.HasOne(d => d.Spectator).WithMany(p => p.RacePredictions)
                .HasForeignKey(d => d.SpectatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_predictions_spectators");
        });

        modelBuilder.Entity<RaceReferee>(entity =>
        {
            entity.HasKey(e => e.RefereeId).HasName("PK__race_ref__28102F7A95FFDADE");

            entity.ToTable("race_referees");

            entity.Property(e => e.RefereeId)
                .ValueGeneratedNever()
                .HasColumnName("referee_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.ExperienceYears).HasColumnName("experience_years");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LicenseNo)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("license_no");

            entity.HasOne(d => d.Referee).WithOne(p => p.RaceReferee)
                .HasForeignKey<RaceReferee>(d => d.RefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_referees_users");
        });

        modelBuilder.Entity<RaceRegistration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("PK__race_reg__22A298F69B7624C6");

            entity.ToTable("race_registrations");

            entity.HasIndex(e => new { e.RaceId, e.HorseId }, "UQ_race_registrations_race_horse").IsUnique();

            entity.HasIndex(e => new { e.RaceId, e.JockeyId }, "UX_race_registrations_race_jockey")
                .IsUnique()
                .HasFilter("([jockey_id] IS NOT NULL)");

            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.AdminNote)
                .HasMaxLength(500)
                .HasColumnName("admin_note");
            entity.Property(e => e.HorseId).HasColumnName("horse_id");
            entity.Property(e => e.JockeyConfirmedAt).HasColumnName("jockey_confirmed_at");
            entity.Property(e => e.JockeyId).HasColumnName("jockey_id");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("submitted_at");

            entity.HasOne(d => d.Horse).WithMany(p => p.RaceRegistrations)
                .HasForeignKey(d => d.HorseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_registrations_horses");

            entity.HasOne(d => d.Jockey).WithMany(p => p.RaceRegistrations)
                .HasForeignKey(d => d.JockeyId)
                .HasConstraintName("FK_race_registrations_jockeys");

            entity.HasOne(d => d.Owner).WithMany(p => p.RaceRegistrations)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_registrations_owners");

            entity.HasOne(d => d.Race).WithMany(p => p.RaceRegistrations)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_registrations_races");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.RaceRegistrations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_race_registrations_reviewed_by");
        });

        modelBuilder.Entity<RaceResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("PK__race_res__AFB3C316BEFC476E");

            entity.ToTable("race_results");

            entity.HasIndex(e => e.RegistrationId, "UQ__race_res__22A298F7BC9E1690").IsUnique();

            entity.HasIndex(e => new { e.RaceId, e.FinishPosition },"UQ_race_results_race_position").IsUnique().HasFilter("[finish_position] IS NOT NULL");

            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.AdminConfirmedBy).HasColumnName("admin_confirmed_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EnteredByRefereeId).HasColumnName("entered_by_referee_id");
            entity.Property(e => e.FinishPosition).HasColumnName("finish_position");
            entity.Property(e => e.FinishTimeSeconds)
                .HasColumnType("decimal(10, 3)")
                .HasColumnName("finish_time_seconds");
            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.Score)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("score");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.AdminConfirmedByNavigation).WithMany(p => p.RaceResults)
                .HasForeignKey(d => d.AdminConfirmedBy)
                .HasConstraintName("FK_race_results_admin_confirmed_by");

            entity.HasOne(d => d.EnteredByReferee).WithMany(p => p.RaceResults)
                .HasForeignKey(d => d.EnteredByRefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_results_referees");

            entity.HasOne(d => d.Race).WithMany(p => p.RaceResults)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_results_races");

            entity.HasOne(d => d.Registration).WithOne(p => p.RaceResult)
                .HasForeignKey<RaceResult>(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_results_registrations");
        });

        modelBuilder.Entity<RaceViolation>(entity =>
        {
            entity.HasKey(e => e.ViolationId).HasName("PK__race_vio__8A989363B6FEFAEF");

            entity.ToTable("race_violations");

            entity.Property(e => e.ViolationId).HasColumnName("violation_id");
            entity.Property(e => e.Action)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.PenaltyPoints)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("penalty_points");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RefereeId).HasColumnName("referee_id");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.ViolationType)
                .HasMaxLength(150)
                .HasColumnName("violation_type");

            entity.HasOne(d => d.Race).WithMany(p => p.RaceViolations)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_violations_races");

            entity.HasOne(d => d.Referee).WithMany(p => p.RaceViolations)
                .HasForeignKey(d => d.RefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_violations_referees");

            entity.HasOne(d => d.Registration).WithMany(p => p.RaceViolations)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_race_violations_registrations");
        });

        modelBuilder.Entity<RefereeAssignment>(entity =>
        {
            entity.HasKey(e => e.RefereeAssignmentId).HasName("PK__referee___F075D2954BB95A24");

            entity.ToTable("referee_assignments");

            entity.HasIndex(e => new { e.RaceId, e.RefereeId }, "UQ_referee_assignments_race_referee").IsUnique();

            entity.Property(e => e.RefereeAssignmentId).HasColumnName("referee_assignment_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RefereeId).HasColumnName("referee_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.RefereeAssignments)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_referee_assignments_assigned_by");

            entity.HasOne(d => d.Race).WithMany(p => p.RefereeAssignments)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_referee_assignments_races");

            entity.HasOne(d => d.Referee).WithMany(p => p.RefereeAssignments)
                .HasForeignKey(d => d.RefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_referee_assignments_referees");
        });

        modelBuilder.Entity<RefereeReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__referee___779B7C58B711E616");

            entity.ToTable("referee_reports");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.RaceId).HasColumnName("race_id");
            entity.Property(e => e.RefereeId).HasColumnName("referee_id");
            entity.Property(e => e.ReportContent).HasColumnName("report_content");
            entity.Property(e => e.ReportType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("report_type");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("submitted_at");
            entity.Property(e => e.ReportType).HasColumnName("report_type");

            entity.HasOne(d => d.Race).WithMany(p => p.RefereeReports)
                .HasForeignKey(d => d.RaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_referee_reports_races");

            entity.HasOne(d => d.Referee).WithMany(p => p.RefereeReports)
                .HasForeignKey(d => d.RefereeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_referee_reports_referees");
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.SeasonId).HasName("PK_seasons");

            entity.ToTable("seasons");

            entity.Property(e => e.SeasonId).HasColumnName("season_id");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");

            entity.Property(e => e.EndDate).HasColumnName("end_date");

            entity.Property(e => e.PointsPerCorrectPrediction)
                .HasDefaultValue(100)
                .HasColumnName("points_per_correct_prediction");

            entity.Property(e => e.SeasonName)
                .HasMaxLength(200)
                .HasColumnName("season_name");

            entity.Property(e => e.StartDate).HasColumnName("start_date");

            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SeasonRewardRule>(entity =>
        {
            entity.HasKey(e => e.SeasonRewardRuleId);

            entity.ToTable("season_reward_rules");

            entity.Property(e => e.SeasonRewardRuleId).HasColumnName("season_reward_rule_id");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.RankPosition).HasColumnName("rank_position");
            entity.Property(e => e.RewardName).HasMaxLength(200).HasColumnName("reward_name");
            entity.Property(e => e.RewardDescription).HasColumnName("reward_description");
            entity.Property(e => e.BonusPoints).HasColumnName("bonus_points");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => new { e.SeasonId, e.RankPosition }).IsUnique();

            entity.HasOne(e => e.Season)
                .WithMany(s => s.SeasonRewardRules)
                .HasForeignKey(e => e.SeasonId);
        });

        modelBuilder.Entity<SeasonReward>(entity =>
        {
            entity.HasKey(e => e.SeasonRewardId);

            entity.ToTable("season_rewards");

            entity.Property(e => e.SeasonRewardId).HasColumnName("season_reward_id");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.SpectatorId).HasColumnName("spectator_id");
            entity.Property(e => e.RankPosition).HasColumnName("rank_position");
            entity.Property(e => e.FinalPoints).HasColumnName("final_points");
            entity.Property(e => e.RewardName).HasMaxLength(200).HasColumnName("reward_name");
            entity.Property(e => e.RewardDescription).HasColumnName("reward_description");
            entity.Property(e => e.BonusPoints).HasColumnName("bonus_points");
            entity.Property(e => e.IsBonusApplied).HasColumnName("is_bonus_applied");
            entity.Property(e => e.AppliedToSeasonId).HasColumnName("applied_to_season_id");
            entity.Property(e => e.AppliedAt).HasColumnName("applied_at");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.AwardedAt).HasColumnName("awarded_at");

            entity.HasIndex(e => new { e.SeasonId, e.SpectatorId }).IsUnique();

            entity.HasOne(e => e.Season)
                .WithMany(s => s.SeasonRewards)
                .HasForeignKey(e => e.SeasonId);

            entity.HasOne(e => e.Spectator)
                .WithMany(u => u.SeasonRewards)
                .HasForeignKey(e => e.SpectatorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });


        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.TournamentId).HasName("PK__tourname__B93AA09D92CAF19D");

            entity.ToTable("tournaments");

            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");

            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");

            entity.Property(e => e.EndDate).HasColumnName("end_date");

            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");

            entity.Property(e => e.MaxHorses)
                .HasDefaultValue(10)
                .HasColumnName("max_horses");

            entity.Property(e => e.PrizePool)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("prize_pool");

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasColumnName("image_url");

            entity.Property(e => e.Rules)
                .HasColumnName("rules");

            entity.Property(e => e.SeasonId)
                .HasColumnName("season_id");

            entity.Property(e => e.StartDate)
                .HasColumnName("start_date");

            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.Property(e => e.TournamentName)
                .HasMaxLength(200)
                .HasColumnName("tournament_name");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Tournaments)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tournaments_users");

            entity.HasOne(d => d.Season).WithMany(p => p.Tournaments)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tournaments_seasons");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370F8BD4C6E0");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E6164C799B617").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.Role)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("role");
            entity.Property(e => e.BettingPoints).HasColumnName("betting_points");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
