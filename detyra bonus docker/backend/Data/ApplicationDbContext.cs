using Microsoft.EntityFrameworkCore;
using CodeLabAPI.Models;

namespace CodeLabAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; } = default!;
        public DbSet<CodeSubmission> CodeSubmissions { get; set; } = default!;
        public DbSet<ExecutionResult> ExecutionResults { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<CodeSubmission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId);
            });

            modelBuilder.Entity<ExecutionResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Output).IsRequired();
                entity.HasOne(e => e.CodeSubmission)
                    .WithMany()
                    .HasForeignKey(e => e.CodeSubmissionId);
            });
        }
    }
}
