using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, Role, long>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Activity tracking tables
        public DbSet<Activity> Activities { get; set; }
        public DbSet<SensorDataPacket> SensorDataPackets { get; set; }
        public DbSet<ActivityTag> ActivityTags { get; set; }
        public DbSet<ActivityTagAssignment> ActivityTagAssignments { get; set; }
        public DbSet<ActivitySummary> ActivitySummaries { get; set; }
        public DbSet<Device> Devices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Activity entity
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.StartTime).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();

                // Foreign key relationship with User
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Index for performance
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.Status);
            });

            // Configure SensorDataPacket entity
            modelBuilder.Entity<SensorDataPacket>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.TimeSinceStart).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CurrentTemperature).IsRequired();
                entity.Property(e => e.CurrentSpeed).IsRequired();
                entity.Property(e => e.Latitude).IsRequired().HasPrecision(18, 10);
                entity.Property(e => e.Longitude).IsRequired().HasPrecision(18, 10);
                entity.Property(e => e.Checksum).IsRequired();
                entity.Property(e => e.DeviceId).HasMaxLength(50);

                // Foreign key relationship with Activity
                entity.HasOne(e => e.Activity)
                      .WithMany(a => a.SensorDataPackets)
                      .HasForeignKey(e => e.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                // Indexes for performance
                entity.HasIndex(e => e.ActivityId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.ActivityId, e.Timestamp });
                entity.HasIndex(e => e.DeviceId);
            });

            // Configure ActivityTag entity
            modelBuilder.Entity<ActivityTag>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Color).HasMaxLength(7);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
            });

            // Configure ActivityTagAssignment entity
            modelBuilder.Entity<ActivityTagAssignment>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Activity)
                      .WithMany(a => a.ActivityTagAssignments)
                      .HasForeignKey(e => e.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tag)
                      .WithMany(t => t.ActivityTagAssignments)
                      .HasForeignKey(e => e.TagId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ActivityId, e.TagId }).IsUnique();
            });

            // Configure ActivitySummary entity
            modelBuilder.Entity<ActivitySummary>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Activity)
                      .WithOne(a => a.Summary)
                      .HasForeignKey<ActivitySummary>(e => e.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ActivityId).IsUnique();
            });

            // Configure Device entity
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.DeviceId);
                entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(100);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId);
            });
        }
    }
}
